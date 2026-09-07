// ============================================================
// TutorialInputGate — makes only the step's target tappable while a step is gated.
// WHAT & WHY: TutorialStepData.gateInputToTarget asks that, while a step runs, the
//   only thing the player can touch is the thing the hand is pointing at. That is
//   what keeps a first-time player (the human's family) from wandering off the
//   guided path. This component is the one place that restriction is implemented.
// KEY DECISIONS:
//   - IMPLEMENTATION CHOSEN: toggling CanvasGroup.blocksRaycasts on an authored list
//     of gated roots (the tool bar, the poster, the button rows...). The alternative
//     - a full-screen transparent Image sorted under the target - was rejected.
//     TRADE-OFF: the blocker-Image approach needs no authored list, but to let the
//     target through it must temporarily add a Canvas + GraphicRaycaster override to
//     an object owned by another system and bump its sortingOrder, then put it back.
//     That mutates other people's hierarchies at runtime, fights every other
//     sortingOrder in the scene, and breaks the moment a screen reparents its tool
//     bar mid-flip - which this game does twice. The CanvasGroup approach touches
//     nothing it was not handed, is visible and debuggable in the Inspector, and
//     restores itself with one call. Its cost is that the human must populate the
//     list, and anything NOT in the list is never blocked. That is the safe
//     direction to fail for a tutorial that must never soft-lock.
//   - FAILS OPEN, three ways: an unknown anchor id releases everything, an anchor
//     that lives outside every gated root releases everything, and OnDisable
//     releases everything. A typo in an asset costs a warning in the Console, never
//     a player who cannot touch anything.
//   - Only blocksRaycasts is toggled, never 'interactable'. Turning interactable off
//     would push Buttons into their greyed-out disabled tint, which reads as "this
//     is broken" rather than "not yet".
//   - The ancestor test walks parents with a plain while loop. No LINQ, no
//     allocation, and it runs once per step, not per frame.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Select the "GameFlow" GameObject (the one that also holds
//     TutorialController). If it does not exist yet: right-click in the
//     Hierarchy -> Create Empty, and rename it "GameFlow".
// [x] Click "Add Component" and add this script (TutorialInputGate).
// [x] Now give every group of buttons you want to be gate-able a Canvas Group:
//     select each of the GameObjects below, click "Add Component" -> Canvas Group.
//       - the Journal screen's page/button area
//       - the tool bar (the object holding the six tool buttons)
//       - the poster object the player drags on
//       - the FinishedRepair screen's button row
// [x] On EVERY one of those Canvas Groups leave "Blocks Raycasts" TICKED. This
//     script unticks and re-ticks it at runtime; ticked is the normal state.
// [x] Back on GameFlow, set "Gated Roots" Size to the number of Canvas Groups you
//     just made, and drag each of those GameObjects into a slot.
// [x] IMPORTANT: every element a tutorial step points at must be a CHILD of one of
//     those gated roots, or the gate will (safely) turn itself off for that step
//     and log a warning telling you which anchor was outside.
// [x] Drag this GameFlow GameObject into TutorialController -> "Input Gate".
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Tutorial
{
    [DisallowMultipleComponent]
    public class TutorialInputGate : MonoBehaviour
    {
        [Tooltip("Every group of interactive UI the tutorial may need to block. " +
                 "Anything not listed here is never blocked.")]
        [SerializeField] private CanvasGroup[] gatedRoots = new CanvasGroup[0];

        [Tooltip("Log which roots were blocked for each step. Handy while authoring.")]
        [SerializeField] private bool verbose;

        /// <summary>True while at least one root is currently blocked.</summary>
        public bool IsGating { get; private set; }

        private void OnDisable()
        {
            // Never leave the scene with raycasts switched off behind us.
            ReleaseAll();
        }

        /// <summary>
        /// Blocks every gated root except the one containing <paramref name="anchorId"/>.
        /// Releases everything (and warns) when the id cannot be resolved.
        /// </summary>
        public void GateTo(string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                ReleaseAll();
                return;
            }

            var anchor = TutorialAnchor.Find(anchorId);

            if (anchor == null)
            {
                Debug.LogWarning(
                    $"[TutorialInputGate] No enabled TutorialAnchor with id '{anchorId}'. " +
                    "Input is left open so the player can never be locked out.", this);
                ReleaseAll();
                return;
            }

            var target = anchor.Target != null ? anchor.Target : anchor.transform as RectTransform;

            if (target == null)
            {
                ReleaseAll();
                return;
            }

            GateTo(target, anchorId);
        }

        /// <summary>Blocks every gated root except the one containing this rect.</summary>
        public void GateTo(RectTransform target)
        {
            GateTo(target, target != null ? target.name : string.Empty);
        }

        private void GateTo(RectTransform target, string label)
        {
            if (target == null || gatedRoots == null || gatedRoots.Length == 0)
            {
                ReleaseAll();
                return;
            }

            // First pass: is the target inside anything we are able to gate? If it is
            // not, blocking everything would leave the player with nothing to touch.
            var covered = false;

            for (var i = 0; i < gatedRoots.Length; i++)
            {
                var root = gatedRoots[i];

                if (root != null && IsAncestorOf(root.transform, target))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
            {
                Debug.LogWarning(
                    $"[TutorialInputGate] '{label}' is not inside any of the Gated Roots, " +
                    "so the gate stays open for this step. Add its parent to Gated Roots " +
                    "if you want the rest of the screen blocked.", this);
                ReleaseAll();
                return;
            }

            for (var i = 0; i < gatedRoots.Length; i++)
            {
                var root = gatedRoots[i];

                if (root == null)
                {
                    continue;
                }

                var allow = IsAncestorOf(root.transform, target);
                root.blocksRaycasts = allow;

                if (verbose)
                {
                    Debug.Log($"[TutorialInputGate] '{root.name}' blocksRaycasts = {allow} (target '{label}').", root);
                }
            }

            IsGating = true;
        }

        /// <summary>Re-enables raycasts on every gated root. Safe to call at any time.</summary>
        public void ReleaseAll()
        {
            if (gatedRoots == null)
            {
                IsGating = false;
                return;
            }

            for (var i = 0; i < gatedRoots.Length; i++)
            {
                var root = gatedRoots[i];

                if (root != null)
                {
                    root.blocksRaycasts = true;
                }
            }

            IsGating = false;
        }

        private static bool IsAncestorOf(Transform candidate, Transform child)
        {
            if (candidate == null || child == null)
            {
                return false;
            }

            var cursor = child;

            while (cursor != null)
            {
                if (cursor == candidate)
                {
                    return true;
                }

                cursor = cursor.parent;
            }

            return false;
        }
    }
}
