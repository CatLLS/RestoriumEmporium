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
// [ ] Build the bar: right-click the Cleaning screen object -> UI -> Image,
//     name it "ToolBar". Drag Assets/Art/LinnenAssets/toolsBarBG into its
//     Source Image. Position it along the bottom of the screen.
// [ ] Add the three ToolButton objects from the ToolButton checklist as
//     children of ToolBar, laid out left to right.
// [ ] Select ToolBar -> Add Component -> Tool Bar Controller.
// [ ] Wire its fields:
//       Runtime  <- the "GameFlow" object (its Restoration Controller)
//       Buttons  <- Size 3, then drag ToolButton_1, ToolButton_2, ToolButton_3
//                   in left-to-right order
//       Cleaning Tools <- Size 3: ToolDustRemover, ToolWaterSpray,
//                         ToolDeacidifier   (in that order)
//       Linen Tools    <- Size 3: ToolSqueegee, ToolRoller, ToolPencil
//                         (in that order)
// [ ] Both lists come from Assets/Data/Tools/. Order matters: it is the order
//     the buttons appear in on screen.
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

        [Tooltip("Shown after SwapToLinenTools: squeegee, roller, pencil.")]
        [SerializeField] private List<ToolData> linenTools = new List<ToolData>();

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

            ShowSet(false, true);
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
            _subscribed = false;
        }

        private void OnStageStarted(RestorationStageData stage, int index)
        {
            RefreshHighlight(false);
        }

        private void OnStageCompleted(RestorationStageData stage, int index)
        {
            // Nothing is usable between stages; drain the whole row.
            SetAllUsable(false, false);
        }

        private void OnTransitionRequested(StageTransition transition, RestorationStageData stage)
        {
            if (transition == StageTransition.SwapToLinenTools)
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
