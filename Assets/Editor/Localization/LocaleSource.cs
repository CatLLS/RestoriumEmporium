// ============================================================
// LocaleSource — every UI/tutorial string in the game, as code, per language.
// WHAT & WHY: The locale tables (Assets/Data/Localization/*.asset) are generated
//   from this source so that strings live in reviewable, diffable text and every
//   key exists in EVERY language. Each area of the game owns one partial file
//   (LocaleSource.<Area>.cs) so several people can add strings without editing
//   the same file.
// KEY DECISIONS:
//   - One entry = key + Portuguese + English. Adding a language later means
//     adding a column here and a table code in LocaleTableBuilder.
//   - LocaleTableBuilder UPSERTS these into the tables and never deletes keys it
//     does not know. That is what lets the "New Shop Item" window write item
//     names straight into the tables without being wiped by the next rebuild.
//   - Key convention: ui.<screen>.<element>, tutorial.<sequence>.<step>,
//     stage.<stageId>.<title|hint>, poster.<posterId>.<title|chapter>,
//     shop.item.<itemId>.<name|desc>.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Editor-only. Run Restorium -> Rebuild Catalogs & Locale Tables after
//     editing any LocaleSource.*.cs file.
// ---------------------------------------------------------------

using System.Collections.Generic;

namespace RestoriumEmporium.EditorTools
{
    public readonly struct LocaleEntry
    {
        public readonly string Key;
        public readonly string PtBR;
        public readonly string En;

        public LocaleEntry(string key, string ptBR, string en)
        {
            Key = key;
            PtBR = ptBR;
            En = en;
        }
    }

    public static partial class LocaleSource
    {
        public const string PtBR = "pt-BR";
        public const string En = "en";

        /// <summary>Every entry from every area file, in a stable order.</summary>
        public static IEnumerable<LocaleEntry> All()
        {
            foreach (var group in new[] { Legacy, Flow, Restoration, Shop, Store, UI, Tutorial })
            {
                foreach (var entry in group)
                {
                    yield return entry;
                }
            }
        }

        private static LocaleEntry E(string key, string ptBR, string en) => new LocaleEntry(key, ptBR, en);
    }
}
