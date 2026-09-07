// ============================================================
// LocaleTable — every player-facing string for one language, as one asset.
// WHAT & WHY: The MVP ships Portuguese, but the Figma SettingsScene already
//   has a language picker. Keeping each language in its own asset means adding
//   English in v2 is authoring one more file, with no code change anywhere.
// KEY DECISIONS:
//   - A serialised List of key/value pairs plus a Dictionary built once on
//     demand. Unity cannot serialise a Dictionary, and a list alone would make
//     every lookup an O(n) scan; building the index lazily gives both an
//     Inspector-editable asset and O(1) lookups.
//   - Duplicate keys are reported once at build-index time rather than silently
//     letting the last one win. A duplicated key is always an authoring mistake
//     and is very hard to spot by eye in a long list.
//   - No plural or gender rules. Portuguese needs them eventually, but adding a
//     rule engine before there is a second language would be guessing at a
//     shape we cannot yet see.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Right-click in Project -> Create -> Restorium -> Locale Table.
// [x] Name it "pt-BR" and put it in Assets/Data/Localization/.
// [x] Set Locale Code to "pt-BR" and Display Name to "Portugues (Brasil)".
// [x] Menu Restorium -> Create Poster 1 Data fills in every entry for you;
//     run that instead of typing the rows by hand.
// [x] Drag the asset into LocalizationService.tables on the Systems prefab.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Localization
{
    [CreateAssetMenu(menuName = "Restorium/Locale Table", fileName = "LocaleTable")]
    public class LocaleTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Lookup key, e.g. \"ui.journal.restore\".")]
            public string key;

            [TextArea(1, 6)]
            [Tooltip("The translated text shown to the player.")]
            public string value;
        }

        [Header("Identity")]
        [Tooltip("BCP-47-ish code, e.g. \"pt-BR\" or \"en\".")]
        public string localeCode = "pt-BR";

        [Tooltip("Shown in the language picker, in its own language.")]
        public string displayName = "Portugues (Brasil)";

        [Header("Strings")]
        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, string> _index;

        /// <summary>Looks up a key. Returns false when the key is not in this table.</summary>
        public bool TryGet(string key, out string value)
        {
            if (string.IsNullOrEmpty(key))
            {
                value = null;
                return false;
            }

            EnsureIndex();
            return _index.TryGetValue(key, out value);
        }

        /// <summary>Rebuilds the lookup index. Call after editing entries at runtime.</summary>
        public void Invalidate() => _index = null;

        private void EnsureIndex()
        {
            if (_index != null)
            {
                return;
            }

            _index = new Dictionary<string, string>(entries.Count, StringComparer.Ordinal);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                if (_index.ContainsKey(entry.key))
                {
                    Debug.LogWarning(
                        $"[LocaleTable:{localeCode}] Duplicate key '{entry.key}' at row {i}. " +
                        "The first occurrence wins; remove the duplicate.", this);
                    continue;
                }

                _index.Add(entry.key, entry.value);
            }
        }

        private void OnDisable()
        {
            // Domain reloads and asset re-imports must not leave a stale index behind.
            _index = null;
        }
    }
}
