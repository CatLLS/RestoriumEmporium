// ============================================================
// LocalizationService — resolves every player-facing key to a string.
// WHAT & WHY: The MVP ships Portuguese only, but the Figma settings screen
//   already has a language picker. This is the ILocalizationService that turns
//   a key into text by asking the active LocaleTable, so adding English in v2 is
//   authoring one more asset and nothing else.
// KEY DECISIONS:
//   - Two-step fallback (active table, then fallback table, then the key
//     itself). A half-translated v2 language then shows Portuguese for the rows
//     nobody got to, instead of blank labels; and a key that exists in no table
//     at all shows up on screen as "ui.journal.restore", which is obvious in a
//     screenshot and harmless in a player's hands.
//   - Get() can never fail and never throws. Text resolution happens inside
//     OnEnable of UI objects, and an exception there leaves a half-built screen.
//   - Format() catches FormatException instead of validating the format string.
//     The braces come from a translator's spreadsheet, so a malformed one is a
//     content bug, not a code bug, and it must degrade to the raw string rather
//     than take the screen down. It is reported once per key: this can be called
//     from a progress readout every frame, and a log line per frame would bury
//     the console and cost real time on device.
//   - SetLocale on an unknown code warns and keeps the current language rather
//     than switching to nothing. A settings screen offering a language whose
//     table was not built should look broken in the console, not on screen.
//   - The service does not register itself in ServiceLocator; GameBootstrap
//     does, so there is exactly one place that decides what is registered and
//     in what order.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Create the Portuguese table first if it does not exist: right-click in
//     the Project window -> Create -> Restorium -> Locale Table. Name the new
//     asset "pt-BR" and move it into Assets/Data/Localization/ (create that
//     folder with right-click -> Create -> Folder if needed).
// [ ] Select the "pt-BR" asset and set "Locale Code" to exactly pt-BR and
//     "Display Name" to "Portugues (Brasil)".
// [ ] Select the "Systems" object in the Title scene. Click "Add Component",
//     type "LocalizationService", press Enter.
// [ ] On the LocalizationService component set "Tables" Size to 1, then drag
//     the "pt-BR" asset from the Project window into the "Element 0" slot.
// [ ] Set "Fallback Locale Code" to exactly pt-BR. It must match the "Locale
//     Code" on one of the assets in the Tables list, character for character.
// [ ] Nothing to do about startup ordering: GameBootstrap already carries a
//     [DefaultExecutionOrder(-100)] attribute, so it registers this service
//     before any screen asks it for text. If you ever open Edit -> Project
//     Settings -> Script Execution Order, do not give GameBootstrap a number
//     larger than 0 there — that would override the attribute and every label
//     would come up showing its key.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Localization
{
    [DisallowMultipleComponent]
    public class LocalizationService : MonoBehaviour, ILocalizationService
    {
        [Header("Tables")]
        [Tooltip("One LocaleTable asset per supported language. " +
                 "The MVP ships a single pt-BR table.")]
        [SerializeField] private LocaleTable[] tables = Array.Empty<LocaleTable>();

        [Tooltip("Locale used at startup, and consulted whenever the active language " +
                 "is missing a key. Must match a Locale Code on one of the tables above.")]
        [SerializeField] private string fallbackLocaleCode = "pt-BR";

        private LocaleTable _active;
        private LocaleTable _fallback;

        /// <summary>Keys already reported as having a malformed format string.</summary>
        private HashSet<string> _brokenFormatKeys;

        private bool _initialised;

        /// <inheritdoc />
        public event Action LocaleChanged;

        /// <inheritdoc />
        public string CurrentLocaleCode =>
            _active != null ? _active.localeCode : fallbackLocaleCode;

        /// <summary>The tables this service can switch between. Read-only view.</summary>
        public IReadOnlyList<LocaleTable> Tables => tables;

        private void Awake()
        {
            Initialise();
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

            Initialise();

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
                Debug.LogWarning("[LocalizationService] SetLocale called with no code. " +
                                 $"Staying on '{CurrentLocaleCode}'.", this);
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

            if (table == _active)
            {
                return;
            }

            _active = table;
            LocaleChanged?.Invoke();
        }

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

            _active = _fallback;
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
