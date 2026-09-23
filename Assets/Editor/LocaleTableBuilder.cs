// ============================================================
// LocaleTableBuilder — writes the pt-BR and en LocaleTable assets from LocaleSource.
// WHAT & WHY: Every string lives in reviewable C# (LocaleSource.*.cs, one partial
//   per area of the game). This tool turns those entries into the two assets the
//   game actually reads (Assets/Data/Localization/pt-BR.asset and en.asset), so a
//   translator edits text, not Inspector rows, and every key exists in BOTH
//   languages by construction.
// KEY DECISIONS:
//   - UPSERT ONLY: a key is added or its value overwritten; nothing is ever
//     deleted. The "New Shop Item" window writes item names straight into the
//     tables with Upsert(), and a later full rebuild must not wipe them. A key
//     removed from code therefore lingers harmlessly until someone deletes the
//     row by hand — the safe direction to fail.
//   - The existing pt-BR.asset is updated IN PLACE (never recreated), so its GUID
//     and the LocalizationService reference to it survive. en.asset is created
//     next to it the first time.
//   - A key defined twice across the partial files is reported; the LATER
//     definition wins (LocaleSource.All() lists Legacy first, so an area file can
//     deliberately override an MVP line).
//   - An empty value in one language is reported: it would show a blank label.
//   - Upsert(code, key, value) is public and cheap enough to call many times from
//     another editor tool; it marks the table dirty and saves it.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach: this is an Editor-only tool (it lives in Assets/Editor).
// [ ] After editing any LocaleSource.*.cs file, run
//     Restorium -> Localization -> Rebuild Locale Tables
//     (or Restorium -> Rebuild Catalogs & Locale Tables, which calls this too).
// [ ] Check the Console: it prints how many keys were added/updated per language
//     and lists any duplicate keys or blank translations to fix.
// [ ] Make sure both assets are in LocalizationService -> Tables on the Systems
//     object in the Title scene (en first, then pt-BR).
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Localization;

    public static class LocaleTableBuilder
    {
        public const string Folder = "Assets/Data/Localization";

        /// <summary>Rebuilds (upserts) every entry of LocaleSource.All() into both tables.</summary>
        [MenuItem("Restorium/Localization/Rebuild Locale Tables", false, 150)]
        public static void RebuildAll()
        {
            EnsureFolder(Folder);

            var pt = LoadOrCreateTable(LocaleSource.PtBR);
            var en = LoadOrCreateTable(LocaleSource.En);

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            var duplicates = new List<string>();
            var blanks = new List<string>();
            int ptChanged = 0, enChanged = 0, total = 0;

            foreach (var entry in LocaleSource.All())
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    continue;
                }

                total++;

                if (seen.ContainsKey(entry.Key))
                {
                    duplicates.Add(entry.Key);
                }

                seen[entry.Key] = total;

                if (string.IsNullOrEmpty(entry.PtBR)) blanks.Add(entry.Key + " (pt-BR)");
                if (string.IsNullOrEmpty(entry.En)) blanks.Add(entry.Key + " (en)");

                if (UpsertInto(pt, entry.Key, entry.PtBR)) ptChanged++;
                if (UpsertInto(en, entry.Key, entry.En)) enChanged++;
            }

            Finish(pt);
            Finish(en);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LocaleTableBuilder] {seen.Count} key(s) from LocaleSource. " +
                      $"pt-BR: {ptChanged} added/updated, en: {enChanged} added/updated. " +
                      "Nothing was deleted (upsert-only).");

            if (duplicates.Count > 0)
            {
                Debug.LogWarning("[LocaleTableBuilder] These keys are defined more than once across " +
                                 "LocaleSource.*.cs; the LAST definition won. Remove the extra one:\n  " +
                                 string.Join("\n  ", duplicates));
            }

            if (blanks.Count > 0)
            {
                Debug.LogWarning("[LocaleTableBuilder] These entries have an EMPTY translation and " +
                                 "will show a blank label:\n  " + string.Join("\n  ", blanks));
            }
        }

        /// <summary>
        /// Adds or overwrites one row in the table for <paramref name="localeCode"/>
        /// (creating the table if needed) and saves it. Never deletes anything.
        /// </summary>
        public static void Upsert(string localeCode, string key, string value)
        {
            if (string.IsNullOrEmpty(localeCode) || string.IsNullOrEmpty(key))
            {
                Debug.LogWarning($"[LocaleTableBuilder] Upsert ignored: locale '{localeCode}', key '{key}'.");
                return;
            }

            EnsureFolder(Folder);
            var table = LoadOrCreateTable(localeCode);

            if (UpsertInto(table, key, value ?? string.Empty))
            {
                Finish(table);
                AssetDatabase.SaveAssetIfDirty(table);
            }
        }

        /// <summary>The asset path of the table for <paramref name="localeCode"/>.</summary>
        public static string PathFor(string localeCode) => $"{Folder}/{localeCode}.asset";

        /// <summary>Loads both tables in picker order (en, pt-BR). Missing ones are skipped.</summary>
        public static LocaleTable[] LoadTablesInPickerOrder()
        {
            var list = new List<LocaleTable>();

            foreach (var code in new[] { LocaleSource.En, LocaleSource.PtBR })
            {
                var table = AssetDatabase.LoadAssetAtPath<LocaleTable>(PathFor(code));

                if (table != null)
                {
                    list.Add(table);
                }
            }

            return list.ToArray();
        }

        private static bool UpsertInto(LocaleTable table, string key, string value)
        {
            table.entries ??= new List<LocaleTable.Entry>();

            for (var i = 0; i < table.entries.Count; i++)
            {
                if (!string.Equals(table.entries[i].key, key, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(table.entries[i].value, value, StringComparison.Ordinal))
                {
                    return false;
                }

                table.entries[i] = new LocaleTable.Entry { key = key, value = value };
                return true;
            }

            table.entries.Add(new LocaleTable.Entry { key = key, value = value });
            return true;
        }

        private static void Finish(LocaleTable table)
        {
            table.Invalidate();
            EditorUtility.SetDirty(table);
        }

        private static LocaleTable LoadOrCreateTable(string localeCode)
        {
            var path = PathFor(localeCode);
            var table = AssetDatabase.LoadAssetAtPath<LocaleTable>(path);

            if (table == null)
            {
                table = ScriptableObject.CreateInstance<LocaleTable>();
                table.entries = new List<LocaleTable.Entry>();
                AssetDatabase.CreateAsset(table, path);
            }

            // Identity is (re)asserted every run, so a hand-edited code cannot drift.
            table.localeCode = localeCode;
            table.displayName = DisplayName(localeCode, table.displayName);
            EditorUtility.SetDirty(table);
            return table;
        }

        private static string DisplayName(string localeCode, string current)
        {
            switch (localeCode)
            {
                case LocaleSource.PtBR:
                    return "Português (Brasil)";
                case LocaleSource.En:
                    return "English";
                default:
                    return string.IsNullOrEmpty(current) ? localeCode : current;
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
