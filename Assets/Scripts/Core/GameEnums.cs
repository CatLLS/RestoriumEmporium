// ============================================================
// GameEnums — the shared vocabulary every system speaks.
// WHAT & WHY: One file holding every cross-system enum, so that Core, UI,
//   Restoration, Tutorial and Audio can talk about the same screens, tools
//   and sounds without any of them referencing each other's classes.
// KEY DECISIONS:
//   - Explicit numeric values on every member. These enums are serialised
//     into the save file and into ScriptableObject assets; if a member were
//     inserted in the middle later, unnumbered values would silently shift
//     and corrupt existing saves and authored assets.
//   - GameScreen models the LinenBacking stage as three screens
//     (Front / Back / Final) because each has a different layout and camera
//     framing in the Figma design, even though they share one background.
//   - SfxId is an enum rather than direct AudioClip fields so that designers
//     swap sounds in one SfxLibrary asset instead of hunting through prefabs.
//   - Batch 2 added DeskHub/Shop/StickerRemoval screens, StageKind, TracyPresentation
//     and ShopCategory. Members are only ever APPENDED with new numbers.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This file declares types only; there is no component to attach.
// ---------------------------------------------------------------

namespace RestoriumEmporium.Core
{
    /// <summary>Every distinct full-screen view in the MVP flow.</summary>
    public enum GameScreen
    {
        None = 0,

        /// <summary>Poster picker. MVP shows a single page: poster #1.</summary>
        Journal = 1,

        /// <summary>Dust remover, water spray, deacidifier.</summary>
        Cleaning = 2,

        /// <summary>Squeegee on the front of the poster.</summary>
        LinenBackingFront = 3,

        /// <summary>Poster flipped over; roller spreads adhesive on the back.</summary>
        LinenBackingBack = 4,

        /// <summary>Poster mounted on the linen; pencil / mend work.</summary>
        LinenBackingFinal = 5,

        /// <summary>Before/after flip reveal.</summary>
        FinishedRepair = 6,

        /// <summary>
        /// The player's workshop desk. Edit mode and item preview are MODES of this
        /// screen, not screens of their own, because they show the same room with
        /// the same placed decorations underneath a different bar.
        /// </summary>
        DeskHub = 7,

        /// <summary>Tracy's Emporium: the decoration catalogue.</summary>
        Shop = 8,

        /// <summary>Close-up of the poster; tap each sticker to peel it off.</summary>
        StickerRemoval = 9
    }

    /// <summary>
    /// How a restoration stage is played. Scrub is the original drag-to-reveal
    /// stage; every other kind is a different interaction on its own screen that
    /// completes the stage through IRestorationRuntime.ForceCompleteCurrentStage().
    /// </summary>
    public enum StageKind
    {
        Scrub = 0,
        StickerPeel = 1
    }

    /// <summary>Which Tracy presentation a tutorial step uses.</summary>
    public enum TracyPresentation
    {
        /// <summary>The original bust portrait + dialogue box overlay (restoration screens).</summary>
        Portrait = 0,

        /// <summary>Full-body Tracy standing in the desk hub, with the hub dialogue box.</summary>
        HubFullBody = 1
    }

    /// <summary>Shop tabs. Only Decor has items in this build.</summary>
    public enum ShopCategory
    {
        Decor = 0,
        Misc = 1
    }

    /// <summary>
    /// The six restoration tools. Order matches the play order, but nothing
    /// depends on that — the sequence lives in PosterData.stages.
    /// </summary>
    public enum ToolId
    {
        None = 0,
        DustRemover = 1,
        WaterSpray = 2,
        Deacidifier = 3,
        Squeegee = 4,
        Roller = 5,
        Pencil = 6
    }

    /// <summary>Which Tracy portrait the help overlay shows.</summary>
    public enum TracyMood
    {
        Still = 0,
        Happy = 1,
        Embarrassed = 2
    }

    /// <summary>
    /// What happens after a restoration stage reaches its coverage threshold.
    /// Authored per stage so the choreography is data, not a switch statement.
    /// </summary>
    public enum StageTransition
    {
        /// <summary>Advance to the next stage on the same screen.</summary>
        None = 0,

        /// <summary>Swap the tool bar from the cleaning set to the linen set.</summary>
        SwapToLinenTools = 1,

        /// <summary>Flip the poster over to reveal its back.</summary>
        FlipToBack = 2,

        /// <summary>Flip back to the front and mount on the linen.</summary>
        FlipToFrontAndMount = 3,

        /// <summary>Leave the restoration and show the before/after reveal.</summary>
        GoToFinishedRepair = 4
    }

    /// <summary>
    /// Logical sound effects. Mapped to clips in the SfxLibrary asset, so a
    /// missing clip is silence rather than a crash — safe while SFX are still
    /// being authored.
    /// </summary>
    public enum SfxId
    {
        None = 0,
        ButtonClick = 1,
        PageFlip = 2,
        PosterFlip = 3,
        StageComplete = 4,
        RestorationComplete = 5,
        ToolSelect = 6,

        ToolDustRemover = 10,
        ToolWaterSpray = 11,
        ToolDeacidifier = 12,
        ToolSqueegee = 13,
        ToolRoller = 14,
        ToolPencil = 15,

        /// <summary>A sticker peeling off the close-up (Assets/Audio/(stickerPeel)...).</summary>
        StickerPeel = 20,

        /// <summary>Coins added to the wallet. No clip yet: silent until one is assigned.</summary>
        CoinsGained = 21,

        /// <summary>A shop purchase went through. No clip yet: silent until assigned.</summary>
        Purchase = 22,

        /// <summary>A decoration was dropped into place. No clip yet: silent until assigned.</summary>
        ItemPlaced = 23
    }
}
