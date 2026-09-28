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
//   - Batch 2: journalOrder (unlock order), chapterKey, completionCutscene and a
//     live coinReward. journalThumbnail is optional now — when empty the journal
//     draws beforeSprite at reduced opacity, so a new poster needs no extra art.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Right-click in Project -> Create -> Restorium -> Poster Data.
// [x] Name it Poster01 and put it in Assets/Data/Poster1/.
// [x] Set Poster Id to "poster01". This string goes into the save file, so do
//     not change it after you have shipped a build.
// [x] Drag in the sprites:
//       Journal Thumbnail    <- Art/journalAssets/posterBeforeDusting(30opacity...)
//       Before Sprite        <- Art/Posters/poster1/posterBeforeDusting
//       Final Sprite         <- Art/Posters/poster1/posterFinal
//       Back Sprite          <- Art/LinnenAssets/PosterBack
//       Linen Backing Sprite <- Art/LinnenAssets/linnenBacking
// [x] Drag the six RestorationStage assets into Stages, in play order.
// [x] Assign this asset to RestorationController.poster in the Game scene
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

        [Tooltip("Journal page / unlock order. Poster 1 = 1, poster 2 = 2, ...")]
        public int journalOrder = 1;

        [Tooltip("Localisation key for the chapter line on Finished Repair, e.g. \"Ch1 - ...\". Blank hides it.")]
        public string chapterKey = string.Empty;

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

        [Header("Cutscenes")]
        [Tooltip("Plays once, after the last stage and BEFORE Finished Repair (poster 1: tracysc2). Optional.")]
        public UnityEngine.Video.VideoClip completionCutscene;

        [Header("Sequence")]
        [Tooltip("The restoration stages, in play order.")]
        public RestorationStageData[] stages = new RestorationStageData[0];

        [Header("Economy")]
        [Tooltip("Coins paid once, the first time this poster is completed. Doubled once by the rewarded ad.")]
        [Min(0)] public int coinReward = 100;

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
