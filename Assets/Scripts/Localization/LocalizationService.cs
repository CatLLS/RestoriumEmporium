// ============================================================
// LocalizationService — resolves every player-facing key to a string.
// WHAT & WHY: The game ships English (the language of the Figma copy) and
//   Brazilian Portuguese. This is the ILocalizationService that turns a key into
//   text by asking the active LocaleTable, decides WHICH table is active (the
//   player's saved choice, else the phone's language), and lets the Settings
//   overlay switch language live.
// KEY DECISIONS:
//   - Two-step fallback (active table, then fallback table, then the key
//     itself). A half-translated language then shows the fallback language for
//     the rows nobody got to, instead of blank labels; and a key that exists in
//     no table at all shows up on screen as "ui.journal.restore", which is
//     obvious in a screenshot and harmless in a player's hands.
//   - Get() can never fail and never throws. Text resolution happens inside
//     OnEnable of UI objects, and an exception there leaves a half-built screen.
//   - Format() catches FormatException instead of validating the format string.
//     The braces come from a translator's spreadsheet, so a malformed one is a
//     content bug, not a code bug, and it must degrade to the raw string rather
//     than take the screen down. It is reported once per key.
//   - WHICH LANGUAGE: SaveData.localeCode wins. When it is empty (first launch)
//     the device language decides (Portuguese -> pt-BR, anything else -> en; see
//     LocalePicker, which is unit-tested) and the pick is written back, so the
//     language never changes under the player's feet later. SetLocale(code)
//     persists the player's choice immediately; SetLocale("") re-runs the guess.
//   - The save is read LAZILY (on the first lookup after ISaveService is
//     registered), not in Awake: GameBootstrap registers the save service in its
//     own Awake and this component must not depend on Awake order. Until then the
//     device guess is active, so a new player never sees a frame of the wrong
//     language. After that the check costs one bool test per lookup.
//   - SetLocale on an unknown code warns and keeps the current language rather
//     than switching to nothing.
//   - The service does not register itself in ServiceLocator; GameBootstrap
//     does, so there is exactly one place that decides what is registered and
//     in what order.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Run the menu Restorium -> Localization -> Rebuild Locale Tables once. It
//     creates / updates Assets/Data/Localization/pt-BR.asset AND en.asset from
//     the LocaleSource.*.cs files. (Restorium -> Rebuild Catalogs & Locale
//     Tables does the same and more.)
// [x] Select the "Systems" object in the Title scene. It already has a
//     LocalizationService component (Add Component -> LocalizationService if not).
// [ ] On the LocalizationService component set "Tables" Size to 2, then drag
//     "en" into Element 0 and "pt-BR" into Element 1 from
//     Assets/Data/Localization/. This order is the order the Settings language
//     picker cycles through.
// [x] Set "Fallback Locale Code" to exactly pt-BR. It must match the "Locale
//     Code" on one of the tables, character for character. It is only used for
//     keys missing from the active language and when the device guess is not
//     shipped; both tables are complete, so it rarely matters.
// [ ] Leave "Auto Detect From Device" ticked.
// [x] Nothing to do about startup ordering: GameBootstrap carries a
//     [DefaultExecutionOrder(-100)] attribute, so it registers this service
//     before any screen asks it for text.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Localization
{
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    public class LocalizationService : MonoBehaviour, ILocalizationService
    {
        [Header("Tables")]
        [Tooltip("One LocaleTable asset per supported language (en, pt-BR). The order " +
                 "is the order the Settings language picker cycles through.")]
        [SerializeField] private LocaleTable[] tables = Array.Empty<LocaleTable>();

        [Tooltip("Consulted whenever the active language is missing a key, and used when " +
                 "the device-language guess is not shipped. Must match a Locale Code above.")]
        [SerializeField] private string fallbackLocaleCode = "pt-BR";

        [Tooltip("When the save has no language yet, pick from the phone's language " +
                 "(Portuguese -> pt-BR, anything else -> en).")]
        [SerializeField] private bool autoDetectFromDevice = true;

        private LocaleTable _active;
        private LocaleTable _fallback;
        private readonly List<string> _codes = new List<string>();

        /// <summary>Keys already reported as having a malformed format string.</summary>
        private HashSet<string> _brokenFormatKeys;

        private bool _initialised;

        /// <summary>True once the active language has been reconciled with SaveData.</summary>
        private bool _savedChoiceApplied;

        /// <inheritdoc />
        public event Action LocaleChanged;

        /// <inheritdoc />
        public string CurrentLocaleCode
        {
            get
            {
                EnsureChosen();
                return _active != null ? _active.localeCode : fallbackLocaleCode;
            }
        }

        /// <inheritdoc />
        public string CurrentLocale => CurrentLocaleCode;

        /// <inheritdoc />
        public IReadOnlyList<string> AvailableLocales
        {
            get
            {
                Initialise();
                return _codes;
            }
        }

        /// <summary>The tables this service can switch between. Read-only view.</summary>
        public IReadOnlyList<LocaleTable> Tables => tables;

        private void Awake()
        {
            Initialise();
        }

        /// <inheritdoc />
        public string DisplayNameFor(string localeCode)
        {
            Initialise();
            var table = FindTable(localeCode);

            if (table == null)
            {
                return localeCode ?? string.Empty;
            }

            return string.IsNullOrEmpty(table.displayName) ? table.localeCode : table.displayName;
        }

        /// <inheritdoc />
        public string Get(string key)
        {
            TryGet(key, out var value);
            return value;
        }

        /// <inheritdoc />
        public bool TryGet(string key, out string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = string.Empty;
                return false;
            }

            EnsureChosen();

            if (_active != null && _active.TryGet(key, out value))
            {
                return true;
            }

            if (_fallback != null && _fallback != _active && _fallback.TryGet(key, out value))
            {
                return true;
            }

            // Showing the key is deliberate: a missing string is loud in testing
            // and inert in production.
            value = key;
            return false;
        }

        /// <inheritdoc />
        public string Format(string key, params object[] args)
        {
            var raw = Get(key);

            if (args == null || args.Length == 0)
            {
                return raw;
            }

            try
            {
                return string.Format(raw, args);
            }
            catch (FormatException e)
            {
                ReportBrokenFormatOnce(key, raw, e);
                return raw;
            }
            catch (ArgumentNullException e)
            {
                ReportBrokenFormatOnce(key, raw, e);
                return raw;
            }
        }

        /// <inheritdoc />
        public void SetLocale(string localeCode)
        {
            Initialise();

            if (string.IsNullOrEmpty(localeCode))
            {
                // "No choice yet" (GameBootstrap passes the empty saved code on a
                // first launch): the device decides and the result is persisted.
                var picked = LocalePicker.Pick(string.Empty, DeviceLanguageName(), _codes, fallbackLocaleCode);

                if (TryPersist(picked))
                {
                    _savedChoiceApplied = true;
                }

                Activate(FindTable(picked));
                return;
            }

            var table = FindTable(localeCode);

            if (table == null)
            {
                Debug.LogWarning($"[LocalizationService] No LocaleTable with Locale Code " +
                                 $"'{localeCode}'. Staying on '{CurrentLocaleCode}'. Add the asset " +
                                 "to the 'Tables' list on this component.", this);
                return;
            }

            // An explicit choice is final: a later lazy save-read must not undo it.
            if (TryPersist(table.localeCode))
            {
                _savedChoiceApplied = true;
            }

            Activate(table);
        }

        private void Activate(LocaleTable table)
        {
            if (table == null || table == _active)
            {
                return;
            }

            _active = table;
            LocaleChanged?.Invoke();
        }

        /// <summary>
        /// Reconciles the active language with SaveData the first time a save service
        /// is available. One bool test on every later call.
        /// </summary>
        private void EnsureChosen()
        {
            Initialise();

            if (_savedChoiceApplied)
            {
                return;
            }

            var save = ServiceLocator.Get<ISaveService>();

            if (save == null || save.Data == null)
            {
                return;
            }

            _savedChoiceApplied = true;

            var saved = save.Data.localeCode;
            var picked = LocalePicker.Pick(saved, DeviceLanguageName(), _codes, fallbackLocaleCode);

            // First launch (or a language that is no longer shipped): remember the
            // pick so the game does not switch language if the phone's changes later.
            if (LocalePicker.Match(_codes, saved) == null)
            {
                TryPersist(picked);
            }

            Activate(FindTable(picked));
        }

        /// <summary>Writes <paramref name="code"/> into SaveData.localeCode. True when a save service exists.</summary>
        private static bool TryPersist(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            var save = ServiceLocator.Get<ISaveService>();

            if (save == null || save.Data == null)
            {
                return false;
            }

            if (!string.Equals(save.Data.localeCode, code, StringComparison.Ordinal))
            {
                save.Data.localeCode = code;
                save.Save();
            }

            return true;
        }

        private string DeviceLanguageName() =>
            autoDetectFromDevice ? Application.systemLanguage.ToString() : string.Empty;

        private void Initialise()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;

            if (tables == null || tables.Length == 0)
            {
                Debug.LogError("[LocalizationService] No LocaleTable assets assigned. Every label " +
                               "will show its key instead of text. Fill in the 'Tables' list on " +
                               "this component.", this);
                return;
            }

            _codes.Clear();

            for (var i = 0; i < tables.Length; i++)
            {
                var table = tables[i];

                if (table != null && !string.IsNullOrEmpty(table.localeCode) &&
                    LocalePicker.Match(_codes, table.localeCode) == null)
                {
                    _codes.Add(table.localeCode);
                }
            }

            _fallback = FindTable(fallbackLocaleCode);

            if (_fallback == null)
            {
                // Better a wrong-but-present language than no text at all.
                _fallback = FirstNonNullTable();

                Debug.LogWarning($"[LocalizationService] Fallback Locale Code " +
                                 $"'{fallbackLocaleCode}' matches none of the assigned tables. " +
                                 $"Using '{(_fallback != null ? _fallback.localeCode : "none")}' " +
                                 "instead.", this);
            }

            // Until the save is readable, use the device guess (see KEY DECISIONS).
            var guess = LocalePicker.Pick(string.Empty, DeviceLanguageName(), _codes, fallbackLocaleCode);
            _active = FindTable(guess) ?? _fallback;
        }

        private LocaleTable FindTable(string localeCode)
        {
            if (tables == null || string.IsNullOrEmpty(localeCode))
            {
                return null;
            }

            for (var i = 0; i < tables.Length; i++)
            {
                var table = tables[i];

                if (table == null)
                {
                    continue;
                }

                // Ordinal-ignore-case so "PT-br" from a save file still matches.
                if (string.Equals(table.localeCode, localeCode, StringComparison.OrdinalIgnoreCase))
                {
                    return table;
                }
            }

            return null;
        }

        private LocaleTable FirstNonNullTable()
        {
            for (var i = 0; i < tables.Length; i++)
            {
                if (tables[i] != null)
                {
                    return tables[i];
                }
            }

            return null;
        }

        private void ReportBrokenFormatOnce(string key, string raw, Exception e)
        {
            _brokenFormatKeys ??= new HashSet<string>(StringComparer.Ordinal);

            if (!_brokenFormatKeys.Add(key))
            {
                return;
            }

            Debug.LogError($"[LocalizationService] The text for '{key}' has a malformed " +
                           $"placeholder and was shown unformatted. Value: \"{raw}\". " +
                           $"({e.Message})", this);
        }
    }
}
