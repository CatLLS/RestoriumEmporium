// ============================================================
// ToolData — one restoration tool, as an authorable asset.
// WHAT & WHY: The tool bar is built from a list of these instead of from
//   hand-placed buttons with hard-coded sprites, so a new tool is a new asset.
// KEY DECISIONS:
//   - Holds no screen position. Where a tool sits in the bar is a layout
//     concern that belongs to the scene, and hard-coding Figma pixel offsets
//     in an asset would fight the Canvas Scaler on other aspect ratios.
//   - brushRadiusUv lives here, not on the stage, because it describes the
//     tool's physical size: a wide squeegee covers more per stroke than a
//     pencil tip. A stage can still override it when it needs to.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Create one asset per tool: right-click in Project ->
//     Create -> Restorium -> Tool Data. Name them ToolDustRemover,
//     ToolWaterSpray, ToolDeacidifier, ToolSqueegee, ToolRoller, ToolPencil.
// [x] Put them in Assets/Data/Tools/.
// [x] On each asset set Id, then drag the matching sprite from
//     Assets/Art/cleaningAssets/ or Assets/Art/LinnenAssets/ into Icon.
// [x] Set Name Key to the matching key in the pt-BR LocaleTable
//     (for example "tool.dustRemover").
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Data
{
    using RestoriumEmporium.Core;

    [CreateAssetMenu(menuName = "Restorium/Tool Data", fileName = "ToolData")]
    public class ToolData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Must be unique; stages reference tools by this id.")]
        public ToolId id = ToolId.None;

        [Tooltip("Localisation key for the tool's display name.")]
        public string nameKey = string.Empty;

        [Header("Presentation")]
        [Tooltip("Icon shown in the tool bar. Import as Sprite (2D and UI).")]
        public Sprite icon;

        [Tooltip("Looping sound played while the player drags this tool.")]
        public SfxId loopSfx = SfxId.None;

        [Header("Feel")]
        [Range(0.02f, 0.5f)]
        [Tooltip("Brush radius as a fraction of the poster's width. " +
                 "A stage may override this if it needs a different size.")]
        public float brushRadiusUv = 0.12f;

        [Tooltip("Particle system spawned at the drag point while this tool is used.")]
        public GameObject fxPrefab;
    }
}
