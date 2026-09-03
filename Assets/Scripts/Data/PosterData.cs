// ============================================================
// PosterData — one poster and its full restoration sequence.
// WHAT & WHY: Section 3 of the build spec requires that adding content never
//   requires a code change. This asset is the whole definition of a poster:
//   what it looks like at each end of the job, and the ordered stages between.
// KEY DECISIONS:
//   - beforeSprite and finalSprite are stored separately from the stage list
//     because the FinishedRepair screen shows them as a before/after flip. It
//     must not have to reach into stages[0].fromSprite and stages[last].toSprite
//     and hope the authoring stayed consistent.
//   - Stages are an array in play order. Order is the only sequencing rule:
//     there is no graph, no branching, and no per-stage "next" pointer that
//     could fall out of sync with the list.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Right-click in Project -> Create -> Restorium -> Poster Data.
// [ ] Name it Poster01 and put it in Assets/Data/Poster1/.
// [ ] Set Poster Id to "poster01". This string goes into the save file, so do
//     not change it after you have shipped a build.
// [ ] Drag in the sprites:
//       Journal Thumbnail    <- Art/journalAssets/posterBeforeDusting(30opacity...)
//       Before Sprite        <- Art/Posters/poster1/posterBeforeDusting
//       Final Sprite         <- Art/Posters/poster1/posterFinal
//       Back Sprite          <- Art/LinnenAssets/PosterBack
//       Linen Backing Sprite <- Art/LinnenAssets/linnenBacking
// [ ] Drag the six RestorationStage assets into Stages, in play order.
// [ ] Assign this asset to RestorationController.poster in the Game scene
//     (the MVP has a single poster).
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Data
{
    [CreateAssetMenu(menuName = "Restorium/Poster Data", fileName = "PosterData")]
    public class PosterData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written into the save file. Never change it once shipped.")]
        public string posterId = "poster01";

        [Tooltip("Localisation key for the poster's title in the journal.")]
        public string titleKey = string.Empty;

        [Header("Sprites")]
        [Tooltip("Faded preview shown on the journal page.")]
        public Sprite journalThumbnail;

        [Tooltip("The untouched poster. Front face of the before/after flip.")]
        public Sprite beforeSprite;

        [Tooltip("The fully restored poster. Back face of the before/after flip.")]
        public Sprite finalSprite;

        [Tooltip("The back of the poster, shown during the roller stage.")]
        public Sprite backSprite;

        [Tooltip("The framed linen the poster is mounted on.")]
        public Sprite linenBackingSprite;

        [Header("Sequence")]
        [Tooltip("The restoration stages, in play order.")]
        public RestorationStageData[] stages = new RestorationStageData[0];

        [Header("Economy (unused in the MVP, kept for Part 5)")]
        public int coinReward = 50;

        public int StageCount => stages != null ? stages.Length : 0;

        /// <summary>Returns the stage at <paramref name="index"/>, or null when out of range.</summary>
        public RestorationStageData GetStage(int index)
        {
            if (stages == null || index < 0 || index >= stages.Length)
            {
                return null;
            }

            return stages[index];
        }
    }
}
