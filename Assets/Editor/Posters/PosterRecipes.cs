// ============================================================
// PosterRecipes — the authored content of every poster, as data in one file.
// WHAT & WHY: A poster is "which sprites, in which order, with which tool, on
//   which screen". PosterAuthoringTool turns a recipe into PosterData and
//   RestorationStageData assets. Keeping the recipes here, separate from the code
//   that writes assets, means adding poster 3 is: copy the Poster2 block, change
//   the paths, add one menu line — no new asset-writing code.
// KEY DECISIONS:
//   - Poster 1 keeps the ORIGINAL asset names and folders (Assets/Data/Poster1/
//     Stages/01_Dust ... 06_Mend, Poster01) so their GUIDs, and every scene
//     reference to them, survive re-running the tool. Its values mirror what is
//     in those assets today (e.g. the adhesive stage reveals PosterBackAfterRoller
//     with a white tint, as the human set it by hand).
//   - Stage ids are unique per poster ("poster02.dust") because they are save and
//     tutorial identities, but stages doing the same job SHARE their title/prompt
//     keys ("stage.dust.title"): the header text is identical, so one string.
//   - Sticker positions come from the Figma "Poster2" section (close-up node
//     574:27, stickers 574:28 / 574:34), converted by
//     StickerLayoutMath.DesignRectToNormalized so the numbers stay traceable.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Editor-only data read by PosterAuthoringTool
//     (menu Restorium -> Posters -> ...).
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Restoration;

    internal sealed class StickerRecipe
    {
        public string spritePath;
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public float rotation;
    }

    internal sealed class StageRecipe
    {
        public string assetName;
        public string stageId;
        public string titleKey;
        public string tutorialKey;
        public GameScreen screen;
        public ToolId tool;
        public StageKind kind = StageKind.Scrub;
        public string fromPath;
        public string toPath;
        public Color tint = Color.white;
        public bool invert;
        public float coverage = 0.85f;
        public StageTransition onComplete = StageTransition.None;
        public string closeUpPath;
        public StickerRecipe[] stickers;
    }

    internal sealed class PosterRecipe
    {
        public string displayName;
        public string folder;
        public string assetName;
        public string posterId;
        public string titleKey;
        public string chapterKey;
        public int journalOrder;
        public int coinReward;
        public string journalThumbnailPath;
        public string beforePath;
        public string finalPath;
        public string backPath;
        public string linenPath;
        public string completionCutscenePath;
        public StageRecipe[] stages;

        public string StagesFolder => folder + "/Stages";
        public string AssetPath => folder + "/" + assetName + ".asset";
    }

    internal static class PosterRecipes
    {
        public const string LinenArt = "Assets/Art/LinnenAssets";
        public const string JournalArt = "Assets/Art/journalAssets";
        public const string Poster1Art = "Assets/Art/Posters/poster1";
        public const string Poster2Art = "Assets/Art/Posters/poster2";
        public const string VideosFolder = "Assets/videos";

        private const string PosterBack = LinenArt + "/PosterBack.png";
        private const string PosterBackAfterRoller = LinenArt + "/PosterBackAfterRoller.png";

        public static PosterRecipe Poster1 => new PosterRecipe
        {
            displayName = "Poster 1",
            folder = "Assets/Data/Poster1",
            assetName = "Poster01",
            posterId = "poster01",
            titleKey = "poster.poster01.title",
            chapterKey = "poster.poster01.chapter",
            journalOrder = 1,
            coinReward = 100,
            journalThumbnailPath = JournalArt + "/posterBeforeDusting(30opacity,beforeRestoring).png",
            beforePath = Poster1Art + "/posterBeforeDusting.png",
            finalPath = Poster1Art + "/posterFinal.png",
            backPath = PosterBack,
            linenPath = LinenArt + "/linnenBacking.png",
            completionCutscenePath = VideosFolder + "/tracysc2.mp4",
            stages = new[]
            {
                Scrub("01_Dust", "dust", "dust", GameScreen.Cleaning, ToolId.DustRemover,
                    Poster1Art + "/posterBeforeDusting.png", Poster1Art + "/posterNoDust.png"),
                Scrub("02_Wash", "wash", "wash", GameScreen.Cleaning, ToolId.WaterSpray,
                    Poster1Art + "/posterNoDust.png", Poster1Art + "/posterYellowWet.png"),
                Scrub("03_Deacidify", "deacidify", "deacidify", GameScreen.Cleaning, ToolId.Deacidifier,
                    Poster1Art + "/posterYellowWet.png", Poster1Art + "/posterWhiteWet.png",
                    onComplete: StageTransition.SwapToLinenTools),
                Scrub("04_Squeegee", "squeegee", "squeegee", GameScreen.LinenBackingFront, ToolId.Squeegee,
                    Poster1Art + "/posterWhiteWet.png", Poster1Art + "/posterDry.png",
                    onComplete: StageTransition.FlipToBack),
                Scrub("05_Adhesive", "adhesive", "adhesive", GameScreen.LinenBackingBack, ToolId.Roller,
                    PosterBack, PosterBackAfterRoller,
                    coverage: 0.80f, onComplete: StageTransition.FlipToFrontAndMount),
                Scrub("06_Mend", "mend", "mend", GameScreen.LinenBackingFinal, ToolId.Pencil,
                    Poster1Art + "/posterDry.png", Poster1Art + "/posterFinal.png",
                    coverage: 0.60f, invert: true, onComplete: StageTransition.GoToFinishedRepair)
            }
        };

        public static PosterRecipe Poster2 => new PosterRecipe
        {
            displayName = "Poster 2",
            folder = "Assets/Data/Poster2",
            assetName = "Poster02",
            posterId = "poster02",
            titleKey = "poster.poster02.title",
            chapterKey = "poster.poster02.chapter",
            journalOrder = 2,
            coinReward = 100,
            journalThumbnailPath = null, // journal draws beforeSprite at ~30 % alpha
            beforePath = Poster2Art + "/posterBeforeDusting.png",
            finalPath = Poster2Art + "/posterFinal.png",
            backPath = PosterBack,
            linenPath = LinenArt + "/linnenBacking.png",
            completionCutscenePath = null,
            stages = new[]
            {
                Scrub("01_Dust", "poster02.dust", "dust", GameScreen.Cleaning, ToolId.DustRemover,
                    Poster2Art + "/posterBeforeDusting.png", Poster2Art + "/posterNoDust.png"),
                new StageRecipe
                {
                    assetName = "02_Stickers",
                    stageId = "poster02.stickers",
                    titleKey = "stage.stickers.title",
                    tutorialKey = "stage.stickers.prompt",
                    screen = GameScreen.StickerRemoval,
                    tool = ToolId.None,
                    kind = StageKind.StickerPeel,
                    fromPath = Poster2Art + "/posterNoDust.png",
                    toPath = Poster2Art + "/posterNoSticker.png",
                    coverage = 1f,
                    onComplete = StageTransition.None,
                    closeUpPath = Poster2Art + "/close-upForStickerRemoval.png",
                    stickers = Poster2StickerDefaults()
                },
                Scrub("03_Wash", "poster02.wash", "wash", GameScreen.Cleaning, ToolId.WaterSpray,
                    Poster2Art + "/posterNoSticker.png", Poster2Art + "/posterYellowWet.png"),
                Scrub("04_Deacidify", "poster02.deacidify", "deacidify", GameScreen.Cleaning, ToolId.Deacidifier,
                    Poster2Art + "/posterYellowWet.png", Poster2Art + "/posterWhiteWet.png",
                    onComplete: StageTransition.SwapToLinenTools),
                Scrub("05_Squeegee", "poster02.squeegee", "squeegee", GameScreen.LinenBackingFront, ToolId.Squeegee,
                    Poster2Art + "/posterWhiteWet.png", Poster2Art + "/posterDry.png",
                    onComplete: StageTransition.FlipToBack),
                Scrub("06_Adhesive", "poster02.adhesive", "adhesive", GameScreen.LinenBackingBack, ToolId.Roller,
                    PosterBack, PosterBackAfterRoller,
                    coverage: 0.80f, onComplete: StageTransition.FlipToFrontAndMount),
                Scrub("07_Mend", "poster02.mend", "mend", GameScreen.LinenBackingFinal, ToolId.Pencil,
                    Poster2Art + "/posterDry.png", Poster2Art + "/posterFinal.png",
                    coverage: 0.60f, invert: true, onComplete: StageTransition.GoToFinishedRepair)
            }
        };

        /// <summary>
        /// The two tape stickers, placed as in Figma (section "Poster2"): close-up at
        /// (500, 3780) 2304 x 4096; sticker1 at (1818, 4912), sticker2 at (1818, 6505),
        /// both 710 x 603. Rotation is baked into the art, so 0 here.
        /// </summary>
        public static StickerRecipe[] Poster2StickerDefaults()
        {
            return new[]
            {
                FromDesign(Poster2Art + "/sticker1.png", 1818f, 4912f),
                FromDesign(Poster2Art + "/sticker2.png", 1818f, 6505f)
            };
        }

        private static StickerRecipe FromDesign(string spritePath, float x, float y)
        {
            StickerLayoutMath.DesignRectToNormalized(
                500f, 3780f, 2304f, 4096f, x, y, 710f, 603f,
                out float cx, out float cy, out float w, out float h);

            return new StickerRecipe
            {
                spritePath = spritePath, centerX = cx, centerY = cy, width = w, height = h, rotation = 0f
            };
        }

        private static StageRecipe Scrub(
            string assetName, string stageId, string textId, GameScreen screen, ToolId tool,
            string fromPath, string toPath, float coverage = 0.85f, bool invert = false,
            StageTransition onComplete = StageTransition.None)
        {
            return new StageRecipe
            {
                assetName = assetName,
                stageId = stageId,
                titleKey = $"stage.{textId}.title",
                tutorialKey = $"stage.{textId}.prompt",
                screen = screen,
                tool = tool,
                kind = StageKind.Scrub,
                fromPath = fromPath,
                toPath = toPath,
                coverage = coverage,
                invert = invert,
                onComplete = onComplete
            };
        }
    }
}
