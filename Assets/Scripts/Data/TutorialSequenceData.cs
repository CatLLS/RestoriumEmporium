// ============================================================
// TutorialSequenceData — one self-contained tutorial script and when it starts.
// WHAT & WHY: The MVP tutorial was one linear list that began on the journal and
//   ended on Finished Repair. Batch 2 teaches things in several places, at
//   different times (the desk hub after poster 1, the shop, poster 2's sticker
//   phase, the journal's second page). Each of those is a sequence: an ordered
//   list of TutorialStepData plus a trigger. The controller starts a sequence when
//   its trigger fires and its prerequisites are met, plays it once, and records
//   it in SaveData.completedTutorialSequences.
// KEY DECISIONS:
//   - Triggers are data (enum + argument), not code, so a new tutorial is a new
//     asset. The controller evaluates them on ScreenChanged / StageStarted.
//   - requiresSequence chains sequences ("deskhub_lamp" only after
//     "first_restoration") without a global step index across unrelated screens.
//   - requiresPosterCompleted gates by game progress (e.g. the journal page-2 hint
//     only once poster 1 is done).
//   - sequenceId, not asset order, is what the save stores, so sequences can be
//     re-ordered or inserted safely.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand: Restorium -> Tutorial -> Rebuild Tutorial Sequences creates
//     them under Assets/Data/Tutorial/Sequences/. Drag every sequence asset into
//     TutorialController -> Sequences (the scene builder does this).
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Data
{
    using RestoriumEmporium.Core;

    public enum TutorialTrigger
    {
        /// <summary>The router shows triggerScreen.</summary>
        ScreenEntered = 0,

        /// <summary>A restoration stage of kind triggerStageKind starts.</summary>
        StageKindStarted = 1,

        /// <summary>Only started from code via TutorialController.TryStart(id).</summary>
        Manual = 2
    }

    [CreateAssetMenu(menuName = "Restorium/Tutorial Sequence", fileName = "TutorialSequence")]
    public class TutorialSequenceData : ScriptableObject
    {
        [Tooltip("Stable id written into the save. Never rename once shipped.")]
        public string sequenceId = string.Empty;

        [Header("When it starts")]
        public TutorialTrigger trigger = TutorialTrigger.ScreenEntered;

        [Tooltip("Used when Trigger is Screen Entered.")]
        public GameScreen triggerScreen = GameScreen.None;

        [Tooltip("Used when Trigger is Stage Kind Started.")]
        public StageKind triggerStageKind = StageKind.StickerPeel;

        [Tooltip("Only start once this sequence id has completed. Blank = no requirement.")]
        public string requiresSequence = string.Empty;

        [Tooltip("Only start once this posterId has been completed. Blank = no requirement.")]
        public string requiresPosterCompleted = string.Empty;

        [Header("What it plays")]
        [Tooltip("Steps in order. The save stores the index into THIS list.")]
        public TutorialStepData[] steps = new TutorialStepData[0];

        public int StepCount => steps != null ? steps.Length : 0;
    }
}
