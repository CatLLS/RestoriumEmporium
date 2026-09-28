// ============================================================
// TutorialInputGate — makes only the step's target tappable while a step is gated.
// WHAT & WHY: TutorialStepData.gateInputToTarget asks that, while a step runs, the
//   only thing the player can touch is the thing the hand is pointing at. That is
//   what keeps a first-time player (the human's family) from wandering off the
//   guided path. This component is the one place that restriction is implemented.
// KEY DECISIONS:
//   - IMPLEMENTATION CHOSEN: toggling CanvasGroup.blocksRaycasts on an authored list
//     of gated roots (the tool bar, the poster, the journal page, the desk hub top
//     bar, the shop grid...). The alternative - a full-screen transparent Image
//     sorted under the target - was rejected: to let the target through it must
//     add a Canvas override to an object owned by another system and bump its
//     sortingOrder, which fights every other sortingOrder in the scene and breaks
//     the moment a screen reparents its tool bar mid-flip. The CanvasGroup approach
//     touches nothing it was not handed and restores itself with one call. Its cost
//     is that the list must be populated, and anything NOT in the list is never
//     blocked — the safe direction to fail for a tutorial that must never soft-lock.
//   - Batch 2: the gate REMEMBERS each root's blocksRaycasts before gating and puts
//     THAT value back on release, instead of forcing true. Other systems now own
//     that flag too (a screen blocking input during a flip, the desk hub in edit
//     mode), and forcing it back on would break them. Release with nothing gated is
//     a no-op for the same reason.
//   - Modal overlays (Pause / Settings) and the CutscenePlayer must NOT be in the
//     list: they sit above the gate on their own canvases, so the player can always
//     pause and videos can always be skipped.
//   - FAILS OPEN, three ways: an unknown anchor id releases everything, an anchor
//     that lives outside every gated root releases everything, and OnDisable
//     releases everything. A typo in an asset costs a warning, never a player who
//     cannot touch anything.
//   - Only blocksRaycasts is toggled, never 'interactable'. Turning interactable off
//     would grey the Buttons out, which reads as "this is broken", not "not yet".
//   - The ancestor test walks parents with a plain while loop. No LINQ, no
//     allocation, and it runs once per step, not per frame.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Select the "GameFlow" GameObject (the one that also holds TutorialController).
// [x] Add Component -> Tutorial Input Gate (this script).
// [ ] Give every group of buttons you want to be gate-able a Canvas Group
//     (select it -> Add Component -> Canvas Group), leaving "Blocks Raycasts" ticked:
//       - JournalScreen/Page and JournalScreen's top bar (back button)
//       - each restoration screen's tool bar and its poster object
//       - FinishedRepairScreen/Buttons
//       - DeskHubScreen's top bar, room and preview/edit bars
//       - ShopScreen's grid and top bar
//       - StickerRemovalScreen's sticker root (optional: sticker steps are not gated)
//     Keep each screen's hamburger (PauseButton) OUTSIDE these groups so the
//     player can always pause.
// [ ] Back on GameFlow, set "Gated Roots" Size to the number of Canvas Groups and
//     drag each of those GameObjects into a slot.
// [x] IMPORTANT: every element a gated tutorial step points at must be a CHILD of
//     one of those roots, or the gate (safely) turns itself off for that step and
//     logs which anchor was outside.
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

        // blocksRaycasts of each root before the current gate, restored on release.
        private bool[] _saved = new bool[0];

        /// <summary>True while at least one root is currently blocked.</summary>
        public bool IsGating { get; private set; }

        private void OnDisable()
        {
            // Never leave the scene with raycasts switched off behind us.
            ReleaseAll();
        }

        /// <summary>True when an enabled anchor with this id exists and lies inside a gated root.</summary>
        public bool CanGate(string anchorId)
        {
            var anchor = TutorialAnchor.Find(anchorId);
            var target = anchor != null ? anchor.Target : null;
            return target != null && IsCovered(target);
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

            // Is the target inside anything we are able to gate? If not, blocking
            // everything would leave the player with nothing to touch.
            if (!IsCovered(target))
            {
                Debug.LogWarning(
                    $"[TutorialInputGate] '{label}' is not inside any of the Gated Roots, " +
                    "so the gate stays open for this step. Add its parent to Gated Roots " +
                    "if you want the rest of the screen blocked.", this);
                ReleaseAll();
                return;
            }

            // Re-gating while gated keeps the ORIGINAL saved values.
            if (!IsGating)
            {
                SaveState();
            }

            for (var i = 0; i < gatedRoots.Length; i++)
            {
                var root = gatedRoots[i];

                if (root == null)
                {
                    continue;
                }

                var allow = IsAncestorOf(root.transform, target);

                // Allowing never turns ON raycasts that the root's owner had switched off.
                root.blocksRaycasts = allow ? _saved[i] : false;

                if (verbose)
                {
                    Debug.Log($"[TutorialInputGate] '{root.name}' blocksRaycasts = {root.blocksRaycasts} (target '{label}').", root);
                }
            }

            IsGating = true;
        }

        /// <summary>Puts every gated root back the way it was. No-op when nothing is gated.</summary>
        public void ReleaseAll()
        {
            if (!IsGating || gatedRoots == null)
            {
                IsGating = false;
                return;
            }

            for (var i = 0; i < gatedRoots.Length && i < _saved.Length; i++)
            {
                if (gatedRoots[i] != null)
                {
                    gatedRoots[i].blocksRaycasts = _saved[i];
                }
            }

            IsGating = false;
        }

        private void SaveState()
        {
            if (_saved.Length != gatedRoots.Length)
            {
                _saved = new bool[gatedRoots.Length];
            }

            for (var i = 0; i < gatedRoots.Length; i++)
            {
                _saved[i] = gatedRoots[i] == null || gatedRoots[i].blocksRaycasts;
            }
        }

        private bool IsCovered(Transform target)
        {
            if (gatedRoots == null)
            {
                return false;
            }

            for (var i = 0; i < gatedRoots.Length; i++)
            {
                if (gatedRoots[i] != null && IsAncestorOf(gatedRoots[i].transform, target))
                {
                    return true;
                }
            }

            return false;
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
