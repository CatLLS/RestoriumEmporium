// ============================================================
// TutorialStepData — one beat of the guided first playthrough.
// WHAT & WHY: Section 3 of the build spec asks for scriptable tutorial steps
//   that point at a target, gate input to it, and advance on the expected
//   action. Authoring these as assets lets the tutorial be re-paced without a
//   recompile, which matters when testing with family and friends.
// KEY DECISIONS:
//   - The target is a string anchor id, not a scene reference. A ScriptableObject
//     cannot hold a scene object reference at all, and ids let the same step
//     asset work across scene reloads and across screens.
//   - advance is an enum rather than a UnityEvent because a tutorial step must
//     have exactly one exit condition. Multiple listeners would make double
//     advances possible, which is how tutorials soft-lock.
//   - autoAdvanceSeconds is a safety valve, not a design tool: any step can be
//     given a timeout so a missed tap can never strand a playtester. Zero
//     disables it.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Create one asset per step: right-click in Project ->
//     Create -> Restorium -> Tutorial Step.
// [ ] Put them in Assets/Data/Tutorial/ and number them: 01_Welcome,
//     02_TapRestore, 03_PickDustRemover, and so on.
// [ ] Set Line Key to a key present in the pt-BR LocaleTable.
// [ ] Set Target Anchor Id to match the Anchor Id field on the
//     TutorialAnchor component of the button you want the hand to point at.
// [ ] Drag the finished list, in order, into TutorialController.steps.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Data
{
    using RestoriumEmporium.Core;

    /// <summary>What ends a tutorial step.</summary>
    public enum TutorialAdvance
    {
        /// <summary>Any tap anywhere. Used for pure dialogue beats.</summary>
        TapAnywhere = 0,

        /// <summary>A tap on the anchored target. Used for "press this button".</summary>
        TapTarget = 1,

        /// <summary>The player started painting the current stage.</summary>
        StageStarted = 2,

        /// <summary>The current restoration stage reached its coverage threshold.</summary>
        StageCompleted = 3,

        /// <summary>The router moved to requiredScreen.</summary>
        ScreenEntered = 4,

        /// <summary>The player selected requiredTool in the tool bar.</summary>
        ToolSelected = 5
    }

    [CreateAssetMenu(menuName = "Restorium/Tutorial Step", fileName = "TutorialStep")]
    public class TutorialStepData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id. The save file stores the index, but this makes " +
                 "the asset readable in the Inspector and in logs.")]
        public string stepId = string.Empty;

        [Header("What Tracy says")]
        [Tooltip("Localisation key for the line. Blank means no dialogue box.")]
        public string lineKey = string.Empty;

        [Tooltip("Which Tracy portrait to show.")]
        public TracyMood mood = TracyMood.Still;

        [Header("Where the hand points")]
        [Tooltip("Anchor Id of the TutorialAnchor to point at. Blank means no hand.")]
        public string targetAnchorId = string.Empty;

        [Tooltip("Block every raycast except the anchored target while this step runs.")]
        public bool gateInputToTarget = true;

        [Tooltip("Seconds to wait before the hand appears. Lets a line be read first.")]
        [Range(0f, 5f)]
        public float pointerDelay = 0.4f;

        [Header("How it ends")]
        public TutorialAdvance advance = TutorialAdvance.TapAnywhere;

        [Tooltip("Used when Advance is Screen Entered.")]
        public GameScreen requiredScreen = GameScreen.None;

        [Tooltip("Used when Advance is Tool Selected.")]
        public ToolId requiredTool = ToolId.None;

        [Tooltip("Safety valve: advance anyway after this many seconds. " +
                 "Zero disables it. Never leave a gated step without one.")]
        [Range(0f, 60f)]
        public float autoAdvanceSeconds;
    }
}
