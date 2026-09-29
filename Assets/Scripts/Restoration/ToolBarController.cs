// ============================================================
// ToolBarController — binds the authored tool sets to the buttons in the bar.
// WHAT & WHY: The player's only choice in a restoration is which tool to pick
//   up, and the bar's job is to make the right choice obvious and the wrong one
//   harmless. This component maps a list of ToolData assets onto a fixed row of
//   ToolButtons, keeps their saturation in step with the current stage, and
//   swaps the whole row from the cleaning set to the linen set when the
//   deacidifier stage asks for it.
// KEY DECISIONS:
//   - Two authored lists and a fixed row of buttons, rather than instantiating
//     a prefab per tool. Both sets hold three tools, the bar is a hand-placed
//     layout in the Figma design, and rebinding three components is cheaper and
//     far easier to lay out than spawning and destroying them.
//   - The swap is driven by StageTransition.SwapToLinenTools coming off
//     RestorationController.TransitionRequested, not by counting stages. The
//     data says when the tools change; the bar just listens.
//   - Which set a bar binds at startup is authored per bar (startWithLinenSet),
//     and a bar with no linen set ignores the swap entirely. Both exist because
//     this component supports two shapes: ONE shared bar that swaps sets
//     mid-restoration, or one bar per screen that only ever shows its own set.
//     Without them a per-screen linen bar would have to duplicate its tools into
//     both lists to survive Awake and the swap, which is data lying to dodge
//     a code path.
//   - The swap only ever goes cleaning -> linen, so the bar also re-picks its set
//     from CurrentStage.requiredTool whenever it is enabled or a stage starts.
//     That is what puts the cleaning tools back when the NEXT poster starts on a
//     bar that swapped during the previous one, and it also covers a resumed save.
//   - Highlighting keys off CurrentStage.requiredTool, not off ActiveTool. The
//     bar must show what the player SHOULD pick up before they have picked
//     anything up, which is the whole point of the greyed-out row.
//   - A rejected tap shakes the button that was tapped and does nothing else. No
//     log, no sound of failure, no disabled button. A disabled button would give
//     the player nothing to tap and no feedback at all.
//   - Subscribes in OnEnable and unsubscribes in OnDisable. The gameplay screens
//     are disabled siblings in one scene, so a bar on a hidden screen must not
//     keep reacting to stage changes.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// It is "ToolBarRoot". The screen checklists (CleaningScreen.cs,
// LinenBackingScreen.cs) own the bar's name and layout and are the authority;
// this component just goes on the object they build. There is no separate
// "ToolBar" object.
//
// [x] Select a screen's ToolBarRoot -> Add Component -> Tool Bar Controller.
//     One per screen that has tools; four in this scene.
// [x] Runtime <- the "GameFlow" object (its Restoration Controller).
// [x] Buttons <- Size 3, then that screen's three tool buttons in left-to-right
//     screen order. Each needs a ToolButton component; see ToolButton.cs.
// [x] Fill in only the set that bar actually shows, from Assets/Data/Tools/.
//     Order matters: it is the order the buttons appear in on screen.
//       CleaningScreen/ToolBarRoot
//         Cleaning Tools <- 01_DustRemover, 02_WaterSpray, 03_Deacidifier
//         Linen Tools    <- leave EMPTY
//         Start With Linen Set = unticked
//       each LinenBacking*Screen/ToolBarRoot
//         Cleaning Tools <- leave EMPTY
//         Linen Tools    <- 04_Squeegee, 05_Roller, 06_Pencil
//         Start With Linen Set = TICKED
// [x] Both lists are only filled on a single shared bar that swaps sets
//     mid-restoration. With one bar per screen, each bar authors one set.
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    [DisallowMultipleComponent]
    public class ToolBarController : MonoBehaviour
    {
        [Header("Runtime")]
        [Tooltip("The restoration this bar drives. The GameFlow object.")]
        [SerializeField] private RestorationController runtime;

        [Header("Buttons")]
        [Tooltip("The button row, in left-to-right screen order.")]
        [SerializeField] private ToolButton[] buttons = new ToolButton[0];

        [Header("Tool sets")]
        [Tooltip("Shown from the start: dust remover, water spray, deacidifier.")]
        [SerializeField] private List<ToolData> cleaningTools = new List<ToolData>();

        [Tooltip("Shown after SwapToLinenTools: squeegee, roller, pencil. " +
                 "Leave EMPTY on a bar that never swaps; the swap is then ignored.")]
        [SerializeField] private List<ToolData> linenTools = new List<ToolData>();

        [Header("Initial state")]
        [Tooltip("Tick on a bar that lives on a linen screen, so it binds the linen " +
                 "set at startup instead of the cleaning set.")]
        [SerializeField] private bool startWithLinenSet;

        private readonly List<ToolData> _activeSet = new List<ToolData>();
        private bool _showingLinenSet;
        private bool _subscribed;

        /// <summary>True once the bar has swapped to the linen set.</summary>
        public bool IsShowingLinenSet => _showingLinenSet;

        private void Awake()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Clicked += OnButtonClicked;
                }
            }

            ShowSet(startWithLinenSet, true);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Clicked -= OnButtonClicked;
                }
            }
        }

        private void OnEnable()
        {
            Subscribe();
            MatchSetToStage(true);
            RefreshHighlight(true);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        /// <summary>
        /// Swaps the visible set. Called automatically on SwapToLinenTools; public
        /// so a resumed save can jump straight to the linen set.
        /// </summary>
        public void ShowSet(bool linen, bool instant = false)
        {
            _showingLinenSet = linen;

            _activeSet.Clear();
            List<ToolData> source = linen ? linenTools : cleaningTools;
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    _activeSet.Add(source[i]);
                }
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                ToolButton slot = buttons[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SetTool(i < _activeSet.Count ? _activeSet[i] : null);
            }

            RefreshHighlight(instant);
        }

        private void Subscribe()
        {
            if (_subscribed || runtime == null)
            {
                return;
            }

            runtime.StageStarted += OnStageStarted;
            runtime.StageCompleted += OnStageCompleted;
            runtime.TransitionRequested += OnTransitionRequested;
            runtime.ToolSelected += OnToolSelected;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || runtime == null)
            {
                return;
            }

            runtime.StageStarted -= OnStageStarted;
            runtime.StageCompleted -= OnStageCompleted;
            runtime.TransitionRequested -= OnTransitionRequested;
            runtime.ToolSelected -= OnToolSelected;
            _subscribed = false;
        }

        private void OnStageStarted(RestorationStageData stage, int index)
        {
            MatchSetToStage(false);
            RefreshHighlight(false);
        }

        /// <summary>
        /// Shows the set that holds the current stage's required tool when only one
        /// set holds it. A swap to linen is one-way within a poster, so without this
        /// a bar that swapped during poster 1 would still show linen tools when
        /// poster 2 starts back on the cleaning screen. No stage, no required tool,
        /// or a tool in both sets (or neither) leaves the row as it is.
        /// </summary>
        private void MatchSetToStage(bool instant)
        {
            RestorationStageData stage = runtime != null ? runtime.CurrentStage : null;
            ToolId required = stage != null ? stage.requiredTool : ToolId.None;

            if (required == ToolId.None)
            {
                return;
            }

            bool inCleaning = Contains(cleaningTools, required);
            bool inLinen = Contains(linenTools, required);

            if (inCleaning == inLinen)
            {
                return;
            }

            if (inLinen != _showingLinenSet)
            {
                ShowSet(inLinen, instant);
            }
        }

        private static bool Contains(List<ToolData> set, ToolId tool)
        {
            if (set == null)
            {
                return false;
            }

            for (int i = 0; i < set.Count; i++)
            {
                if (set[i] != null && set[i].id == tool)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Raises the tool the player is holding and settles every other one. The
        /// controller reports ToolId.None on stage boundaries, which drops the row
        /// flat without this needing to know why.
        /// </summary>
        private void OnToolSelected(ToolId tool)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                ToolButton slot = buttons[i];
                if (slot == null || slot.Tool == null)
                {
                    continue;
                }

                slot.SetSelected(tool != ToolId.None && slot.Tool.id == tool);
            }
        }

        private void OnStageCompleted(RestorationStageData stage, int index)
        {
            // Nothing is usable between stages; drain the whole row.
            SetAllUsable(false, false);
        }

        private void OnTransitionRequested(StageTransition transition, RestorationStageData stage)
        {
            // A bar with no linen set authored is a per-screen bar that never
            // swaps. Honouring the transition there would blank its whole row.
            if (transition == StageTransition.SwapToLinenTools
                && linenTools != null && linenTools.Count > 0)
            {
                ShowSet(true);
            }
        }

        private void OnButtonClicked(ToolButton slot)
        {
            if (slot == null || slot.Tool == null)
            {
                return;
            }

            if (runtime == null)
            {
                slot.PlayRejection();
                return;
            }

            if (runtime.TrySelectTool(slot.Tool.id))
            {
                RefreshHighlight(false);
                return;
            }

            // Soft rejection: the bar nudges, it does not scold.
            slot.PlayRejection();
        }

        private void RefreshHighlight(bool instant)
        {
            RestorationStageData stage = runtime != null ? runtime.CurrentStage : null;
            ToolId required = stage != null ? stage.requiredTool : ToolId.None;

            for (int i = 0; i < buttons.Length; i++)
            {
                ToolButton slot = buttons[i];
                if (slot == null || slot.Tool == null)
                {
                    continue;
                }

                slot.SetUsable(slot.Tool.id == required && required != ToolId.None, instant);
            }
        }

        private void SetAllUsable(bool usable, bool instant)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                ToolButton slot = buttons[i];
                if (slot != null && slot.Tool != null)
                {
                    slot.SetUsable(usable, instant);
                }
            }
        }
    }
}
