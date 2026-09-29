// ============================================================
// SceneBuilderManifest — the static list of assets/keys the Build*.cs files
// reference by literal string, kept here ONCE so SceneValidationTests.cs (an
// EditMode test that must run WITHOUT the scene open or even Unity's own Test
// Runner needing a scene) can check them all with plain AssetDatabase/LocaleSource
// lookups.
// WHAT & WHY: Every Build*.cs file writes `"Assets/Art/.../foo.png"` and
// `"ui.some.key"` as inline literals — that is the simplest, most readable code
// for a human reviewing a Build*.cs file next to the handoff doc it implements.
// The cost of that simplicity is that a typo in one of those literals would
// otherwise only surface the moment a human actually runs the builder in
// Unity. This manifest mirrors the literals (kept in sync by hand — see below)
// so a plain `dotnet test` / EditMode run catches the typo long before that.
// KEY DECISIONS:
//   - MAINTENANCE: if you add a new sprite path or LocalizedText key literal to
//     any Build*.cs file, add it here too. SceneValidationTests fails loudly
//     (missing sprite / missing key) if this list drifts stale in the
//     "reference something real" direction; it CANNOT catch a literal that was
//     added to a Build*.cs file but never mirrored here — that is the one gap
//     of a hand-maintained manifest instead of a source-scanning test, and it
//     is the deliberate, documented trade-off (source-scanning C# with regex
//     is not what "simple and robust" means here).
//   - Sprite paths only (not videos/fonts/ScriptableObject assets): those other
//     asset kinds either don't exist until an earlier Restorium/* menu creates
//     them (PosterCatalog.asset, TutorialSequenceData) or aren't meaningfully
//     "exists via AssetDatabase" checks worth duplicating here (MainMixer.mixer
//     already has its own graceful missing-asset handling in BuildCutscenePlayer).
//   - Locale keys only include ones a Build*.cs file assigns to a LocalizedText
//     component (the `localizedKey` argument of SceneBuilderCore.SetupText).
//     Keys a screen's own runtime code formats itself (e.g. DeskHubScreen's
//     "ui.desk.selected") are not assigned by the scene builder and so are not
//     this file's concern — LocalizationTests.cs / the shipped LocaleTable
//     assets are what exercise those.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Pure data, read by SceneValidationTests.cs only.
// ---------------------------------------------------------------

namespace RestoriumEmporium.EditorTools
{
    public static class SceneBuilderManifest
    {
        /// <summary>Every sprite path a Build*.cs file passes to SceneBuilderCore.SetImage.</summary>
        public static readonly string[] SpritePaths =
        {
            "Assets/Art/DeskHub/book.png",
            "Assets/Art/DeskHub/catBoard.png",
            "Assets/Art/DeskHub/tracyStill.png",
            "Assets/Art/DeskHub/tracyTalkingEmbarassed.png",
            "Assets/Art/DeskHub/tracyTalkingHappyNormal.png",
            "Assets/Art/DeskHub/zoomoutBG.png",
            "Assets/Art/FinishedRepairBG.png",
            "Assets/Art/GamePausedOverlay/gamePausedBG.png",
            "Assets/Art/journalAssets/arrow.png",
            "Assets/Art/journalAssets/bg.png",
            "Assets/Art/journalAssets/buttonBase.png",
            "Assets/Art/journalAssets/paper 1.png",
            "Assets/Art/LinnenAssets/LinnenBackingBG(all).png",
            "Assets/Art/Monetization/bookshelfBG.png",
            "Assets/Art/Monetization/tracyCoins.png",
            "Assets/Art/newGameButton.png",
            "Assets/Art/Posters/poster2/close-upForStickerRemoval.png",
            "Assets/Art/Settings/bookshelfBG.png",
            "Assets/Art/ShopItems/plant/placed.png",
            "Assets/Art/Settings/tracyPortrait.png",
            "Assets/Art/Shop/categoryTab.png",
            "Assets/Art/Shop/leatherBG.png",
            "Assets/Art/Shop/shopBookshelfBG.png",
            "Assets/Art/Shop/shopHeaderPanel.png",
            "Assets/Art/Shop/tracyShop.png",
            "Assets/Art/UI/backArrowIcon.png",
            "Assets/Art/UI/bagButton.png",
            "Assets/Art/UI/coinIcon.png",
            "Assets/Art/UI/coinShowerBG.png",
            "Assets/Art/UI/dialogueRect.png",
            "Assets/Art/UI/editModeButtonBase.png",
            "Assets/Art/UI/hamburgerIcon.png",
            "Assets/Art/UI/moveIcon.png",
            "Assets/Art/UI/moveIconLight.png",
            "Assets/Art/UI/redButton.png",
            "Assets/Art/UI/starGold.png",
        };

        /// <summary>Every key a Build*.cs file assigns via SceneBuilderCore.SetupText's localizedKey.</summary>
        public static readonly string[] LocalizedKeys =
        {
            "ui.cutscene.skipHint",
            "ui.edit.backToWorkshop",
            "ui.edit.dragHint",
            "ui.edit.hint",
            "ui.edit.place",
            "ui.edit.title",
            "ui.edit.undo",
            "ui.finishedRepair.continue",
            "ui.finishedRepair.double",
            "ui.finishedRepair.or",
            "ui.finishedRepair.title",
            "ui.journal.back",
            "ui.journal.lockedHint",
            "ui.pause.flavour",
            "ui.pause.journal",
            "ui.pause.continue",
            "ui.pause.settings",
            "ui.pause.title",
            "ui.preview.buy",
            "ui.preview.dragHint",
            "ui.preview.giveUp",
            "ui.preview.title",
            "ui.settings.language",
            "ui.settings.music",
            "ui.settings.sfx",
            "ui.settings.title",
            "ui.settings.tracyLine",
            "ui.shop.back",
            "ui.shop.buyMoreCoins",
            "ui.shop.empty",
            "ui.shop.owned",
            "ui.shop.removeAds",
            "ui.shop.tabDecor",
            "ui.shop.tabMisc",
            "ui.shop.titleMain",
            "ui.shop.titleTop",
            "ui.coins.title",
            "ui.coins.finePrint",
            "ui.coins.terms",
            "ui.coins.privacy",
            "ui.coins.removeAds",
        };
    }
}
