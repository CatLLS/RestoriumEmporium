// ============================================================
// RestorationStageData — one step of a restoration, as an authorable asset.
// WHAT & WHY: Every stage in the game is the same operation with different
//   inputs: pick a tool, drag until enough of the surface is covered, then do
//   something. Describing that as data means poster #2 is authored, not coded.
// KEY DECISIONS:
//   - fromSprite/toSprite are a pair, not a single "result". The runner stacks
//     them and reveals 'to' through the painted mask, which is what produces
//     the wipe-away feel; a single sprite could only be cross-faded uniformly.
//   - invertMask exists for the pencil stage. There, 'from' (posterDry) is
//     erased away to expose 'to' (posterFinal) underneath: the same shader,
//     read in the opposite direction, rather than a second code path.
//   - revealTint multiplies the top layer. The roller stage sets toSprite to
//     the poster's back and tints it to a wet sheen, which is why no
//     "posterBackWithGlue" artwork is needed.
//   - requiredCoverage is per stage because the pencil stage deliberately
//     auto-completes early (60%) so a missed spot cannot strand the player,
//     while the cleaning stages want a thorough 85%.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Create one asset per stage: right-click in Project ->
//     Create -> Restorium -> Restoration Stage.
// [ ] Put them in Assets/Data/Poster1/Stages/ and name them in play order:
//     01_Dust, 02_Water, 03_Deacidify, 04_Squeegee, 05_Roller, 06_Pencil.
// [ ] On each asset, drag the poster sprites into From Sprite and To Sprite.
// [ ] IMPORTANT: every poster sprite must be imported with
//     Texture Type = Sprite (2D and UI) and Mesh Type = Full Rect.
//     Tight meshes break the reveal shader's UV mapping.
// [ ] Assign Required Tool and On Complete for each stage.
// [ ] Finally, drag the stages, in order, into PosterData.stages.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Data
{
    using RestoriumEmporium.Core;

    [CreateAssetMenu(menuName = "Restorium/Restoration Stage", fileName = "RestorationStage")]
    public class RestorationStageData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used in save files and tutorial steps. Never reuse one.")]
        public string stageId = string.Empty;

        [Tooltip("Which screen this stage plays on.")]
        public GameScreen screen = GameScreen.Cleaning;

        [Tooltip("Only this tool can paint during this stage.")]
        public ToolId requiredTool = ToolId.None;

        [Header("Layers")]
        [Tooltip("The state the poster starts this stage in (bottom layer).")]
        public Sprite fromSprite;

        [Tooltip("The state revealed through the painted mask (top layer).")]
        public Sprite toSprite;

        [Tooltip("Multiplies the revealed layer. Used for the roller's wet-adhesive " +
                 "sheen, where 'to' is the same sprite as 'from'.")]
        public Color revealTint = Color.white;

        [Tooltip("Pencil stage: erase the 'from' layer to expose 'to' underneath.")]
        public bool invertMask;

        [Header("Completion")]
        [Range(0.1f, 1f)]
        [Tooltip("Coverage at which the stage completes. 0.85 for cleaning, " +
                 "0.6 for the pencil so a missed spot cannot strand the player.")]
        public float requiredCoverage = 0.85f;

        [Range(0f, 0.5f)]
        [Tooltip("Overrides ToolData.brushRadiusUv when greater than zero.")]
        public float brushRadiusOverride;

        [Tooltip("What happens once requiredCoverage is reached.")]
        public StageTransition onComplete = StageTransition.None;

        [Header("Text and sound")]
        [Tooltip("Localisation key for the header shown during this stage.")]
        public string titleKey = string.Empty;

        [Tooltip("Localisation key for Tracy's instruction. Blank means no prompt.")]
        public string tutorialKey = string.Empty;

        [Tooltip("Played once when the stage completes.")]
        public SfxId completeSfx = SfxId.StageComplete;

        /// <summary>Effective brush radius for this stage, given its tool.</summary>
        public float ResolveBrushRadius(ToolData tool)
        {
            if (brushRadiusOverride > 0f)
            {
                return brushRadiusOverride;
            }

            return tool != null ? tool.brushRadiusUv : 0.12f;
        }
    }
}
