// ============================================================
// LocalePicker — chooses the language to use, in plain C#.
// WHAT & WHY: On first launch nobody has chosen a language yet (SaveData.localeCode
//   is empty), so the game must guess from the device: Portuguese phones get
//   pt-BR, everyone else English (the language of the Figma copy). After that the
//   player's choice wins. The settings screen also cycles through the shipped
//   languages. Both rules are tiny, but a wrong answer means a player stuck in a
//   language they cannot read, so they are unit-tested here with no UnityEngine.
// KEY DECISIONS:
//   - The device language is passed in as the NAME of UnityEngine.SystemLanguage
//     ("Portuguese", "English", ...) so this file does not reference UnityEngine.
//   - Codes are compared case-insensitively ("PT-br" from a hand-edited save still
//     matches), but the value returned is always the table's own spelling.
//   - A saved code for a language that is no longer shipped falls back to the
//     device guess rather than to nothing.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# used by LocalizationService.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Localization
{
    public static class LocalePicker
    {
        public const string PortugueseBrazil = "pt-BR";
        public const string English = "en";

        /// <summary>
        /// The locale to activate. Returns null only when <paramref name="available"/> is empty.
        /// </summary>
        /// <param name="savedCode">SaveData.localeCode. Empty = not chosen yet.</param>
        /// <param name="systemLanguageName">UnityEngine.SystemLanguage.ToString().</param>
        /// <param name="available">Codes of the tables actually shipped.</param>
        /// <param name="fallbackCode">Used when the device guess is not shipped.</param>
        public static string Pick(
            string savedCode, string systemLanguageName, IReadOnlyList<string> available, string fallbackCode)
        {
            if (available == null || available.Count == 0)
            {
                return null;
            }

            var saved = Match(available, savedCode);

            if (saved != null)
            {
                return saved;
            }

            var guess = GuessFromSystemLanguage(systemLanguageName);
            var guessed = Match(available, guess);

            if (guessed != null)
            {
                return guessed;
            }

            return Match(available, fallbackCode) ?? FirstNonEmpty(available);
        }

        /// <summary>"Portuguese" -> pt-BR, anything else -> en.</summary>
        public static string GuessFromSystemLanguage(string systemLanguageName)
        {
            return string.Equals(systemLanguageName, "Portuguese", StringComparison.OrdinalIgnoreCase)
                ? PortugueseBrazil
                : English;
        }

        /// <summary>The locale after <paramref name="current"/> in <paramref name="available"/>, wrapping round.</summary>
        public static string Next(string current, IReadOnlyList<string> available, int direction = 1)
        {
            if (available == null || available.Count == 0)
            {
                return current;
            }

            var index = IndexOf(available, current);

            if (index < 0)
            {
                return FirstNonEmpty(available);
            }

            var step = direction >= 0 ? 1 : -1;
            var count = available.Count;

            for (var n = 1; n <= count; n++)
            {
                var candidate = available[((index + step * n) % count + count) % count];

                if (!string.IsNullOrEmpty(candidate))
                {
                    return candidate;
                }
            }

            return current;
        }

        /// <summary>The entry of <paramref name="available"/> equal to <paramref name="code"/> ignoring case, or null.</summary>
        public static string Match(IReadOnlyList<string> available, string code)
        {
            var index = IndexOf(available, code);
            return index >= 0 ? available[index] : null;
        }

        private static int IndexOf(IReadOnlyList<string> available, string code)
        {
            if (available == null || string.IsNullOrEmpty(code))
            {
                return -1;
            }

            for (var i = 0; i < available.Count; i++)
            {
                if (string.Equals(available[i], code, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string FirstNonEmpty(IReadOnlyList<string> available)
        {
            for (var i = 0; i < available.Count; i++)
            {
                if (!string.IsNullOrEmpty(available[i]))
                {
                    return available[i];
                }
            }

            return null;
        }
    }
}
