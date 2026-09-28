// ============================================================
// LocaleSource.Restoration — strings owned by the restoration/sticker agent.
// WHAT & WHY: The NEW text the poster recipes (Editor/Posters/PosterRecipes.cs)
//   and the sticker removal stage reference: the sticker stage header and prompt,
//   the poster chapter lines shown on Finished Repair, and poster 2's title. The
//   existing MVP stage titles (stage.dust.title, ...) live in LocaleSource.Legacy
//   (UI agent) and are reused by poster 2's matching stages, so they are not
//   repeated here.
// KEY DECISIONS:
//   - English follows the Figma copy style ("Time to clean!": short, warm,
//     exclamation). pt-BR is a natural translation, not a literal one.
//   - The poster 2 art is the "Eternal Theatre" weekly cinema programme; its
//     title and chapter are drafted from the art and flagged for review.
//   See LocaleSource.cs for the rules and key conventions.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Run Restorium -> Rebuild Catalogs & Locale Tables after editing.
// ---------------------------------------------------------------

namespace RestoriumEmporium.EditorTools
{
    public static partial class LocaleSource
    {
        internal static readonly LocaleEntry[] Restoration =
        {
            // ---- Sticker removal stage (poster02.stickers) ----
            E("stage.stickers.title", "Hora de descolar!", "Peel them off!"),
            E("stage.stickers.prompt",
                "Parece que alguém já tentou consertar este cartaz... Toque em cada fita para descolá-la.",
                "Looks like someone tried to fix this poster before... Tap each piece of tape to peel it off."),

            // ---- Posters (Finished Repair chapter line, journal title) ----
            E("poster.poster01.chapter", "Cap. 1 - Cartaz de turismo de Lethe Falls", "Ch1 - Lethe Falls Tourism Poster"),
            E("poster.poster02.title", "Cine Eternal", "Eternal Theatre"),
            E("poster.poster02.chapter", "Cap. 2 - Programação do Cine Eternal", "Ch2 - Eternal Theatre Cinema Program"),
        };
    }
}
