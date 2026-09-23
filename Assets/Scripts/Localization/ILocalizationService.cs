// ============================================================
// ILocalizationService — key-to-string lookup for every player-facing word.
// WHAT & WHY: The MVP ships Portuguese only, but v2 adds a language picker
//   (already drawn in the Figma SettingsScene). Routing every string through a
//   key means adding English is authoring one more LocaleTable asset, never a
//   code change.
// KEY DECISIONS:
//   - Get() returns the key itself when a translation is missing, instead of
//     null or an exception. A missing string then shows up on screen as
//     "ui.journal.restore" — obvious during testing, harmless in the player's
//     hands, and it can never crash a screen mid-flow.
//   - LocaleChanged is an event so open UI re-resolves live; the language can
//     be switched without reloading the scene.
//   - Not built on com.unity.localization: that package pulls in Addressables
//     and a lot of setup for what is, at MVP scale, a dictionary lookup.
//   - Batch 2: AvailableLocales / CurrentLocale drive the settings language
//     picker; DisplayNameFor gives the picker each language's own name
//     ("English", "Português (Brasil)") without the UI knowing about tables.
//     CurrentLocaleCode is kept as an alias so MVP callers keep compiling.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by LocalizationService on the Systems prefab.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Localization
{
    public interface ILocalizationService
    {
        /// <summary>BCP-47-ish code of the active locale, e.g. "pt-BR". Same as CurrentLocale.</summary>
        string CurrentLocaleCode { get; }

        /// <summary>Code of the active locale, e.g. "en".</summary>
        string CurrentLocale { get; }

        /// <summary>Codes of every shipped language, in the order the picker cycles them.</summary>
        IReadOnlyList<string> AvailableLocales { get; }

        /// <summary>The language's own name for the picker ("English"). Falls back to the code.</summary>
        string DisplayNameFor(string localeCode);

        /// <summary>Resolves a key, falling back to the key itself when missing.</summary>
        string Get(string key);

        /// <summary>Resolves a key and reports whether it actually existed.</summary>
        bool TryGet(string key, out string value);

        /// <summary>
        /// Resolves a key and substitutes {0}, {1}, ... with the supplied values.
        /// </summary>
        string Format(string key, params object[] args);

        /// <summary>
        /// Switches locale, persists the choice in SaveData.localeCode, and raises
        /// LocaleChanged. An unknown code is a no-op; an EMPTY code means "pick from
        /// the device language" (first launch).
        /// </summary>
        void SetLocale(string localeCode);

        event Action LocaleChanged;
    }
}
