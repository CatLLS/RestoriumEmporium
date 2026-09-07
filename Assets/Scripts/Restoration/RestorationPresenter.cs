// ============================================================
// RestorationPresenter — plays the choreography the state machine asks for.
// WHAT & WHY: RestorationController deliberately raises TransitionRequested and
//   then WAITS: it says "flip the poster over", never performs one, so the rules
//   are not welded to a particular scene layout. Something has to be on the
//   other end of that event, play the animation, and call
//   ContinueAfterTransition(). This is that something. Without it the
//   restoration stops dead at the end of the first stage that asks for a
//   transition, which is stage 3 of 6.
// KEY DECISIONS:
//   - This is the ONLY component that calls ContinueAfterTransition(). The
//     controller's autoContinueWhenUnhandled fallback cannot cover for a missing
//     presenter, because ToolBarController also subscribes to
//     TransitionRequested: the handler is never null, so the fallback never
//     fires and the freeze looks like a rules bug rather than a wiring one.
//   - Transitions that have no animation still continue on the NEXT frame, not
//     synchronously. TransitionRequested is a multicast delegate and the tool
//     bar is on it too; continuing inline would start the next stage before the
//     bar had finished swapping its row, and the result would depend on
//     Inspector subscription order, which nobody can see or diff.
//   - The poster stack is collapsed with SetStage(null) before a flip. The card
//     animator swaps the sprite on ONE Image (the bottom layer), but a completed
//     stage leaves the top layer fully revealed on top of it, so without the
//     collapse the swap happens underneath artwork nobody can see past.
//   - The flip's landing state is normalised with ResetOrientation(). A landed
//     flip leaves the rect at 180 degrees with a counter-mirrored face; that
//     looks right, but reparenting it to the next screen would keep the mirror
//     and lose the rotation, so the artwork would come out backwards.
//   - The shared poster is reparented into the visible screen's PosterStackRoot
//     rather than each screen owning a poster. There is one reveal mask and one
//     restoration in flight; a second poster object would be a second, silently
//     diverging copy of the thing the whole game is about.
//   - On screens with no PosterStackRoot (Journal, FinishedRepair) the poster is
//     deactivated, not merely moved off-screen. Those screens have their own
//     artwork for the poster, and a stray raycast target parked over them would
//     eat taps meant for their buttons.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Select the "GameFlow" object -> Add Component -> Restoration Presenter.
//     It belongs on the same object as ScreenRouter and RestorationController.
// [x] Wire its fields:
//       Restoration <- the "GameFlow" object (its Restoration Controller)
//       Router      <- the "GameFlow" object (its Screen Router)
//       Poster      <- the "Poster" object under the Canvas
//       Poster Flip <- the same "Poster" object (its Card Flip Animator)
// [x] The Poster object needs a Card Flip Animator with Face Image set to its
//     BottomLayer child. Without one the flips are skipped, but the restoration
//     still advances rather than freezing.
// [x] Each restoration screen (Cleaning, LinenBackingFront/Back/Final) needs its
//     Poster Stack Root field filled in; that is where this component puts the
//     poster. Journal and FinishedRepair leave it empty on purpose.
// [ ] Nothing to tick. This component has no Update and no options.
// ---------------------------------------------------------------

using System.Collections;
using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.UI;

    [DisallowMultipleComponent]
    public class RestorationPresenter : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("The state machine whose transitions this component plays.")]
        [SerializeField] private RestorationController restoration;

        [Tooltip("The router, so the poster can follow the visible screen.")]
        [SerializeField] private ScreenRouter router;

        [Tooltip("The shared poster object that moves between the restoration screens.")]
        [SerializeField] private PosterLayerStack poster;

        [Tooltip("The Card Flip Animator on the poster. Optional: without it the " +
                 "flips are skipped and the restoration still advances.")]
        [SerializeField] private CardFlipAnimator posterFlip;

        private RectTransform _posterRect;
        private bool _subscribed;

        private void Awake()
        {
            if (restoration == null)
            {
                restoration = GetComponent<RestorationController>();
            }

            if (router == null)
            {
                router = GetComponent<ScreenRouter>();
            }

            if (poster != null)
            {
                _posterRect = poster.transform as RectTransform;

                if (posterFlip == null)
                {
                    posterFlip = poster.GetComponent<CardFlipAnimator>();
                }
            }

            if (restoration == null)
            {
                Debug.LogError(
                    "[RestorationPresenter] No Restoration Controller assigned or found. " +
                    "Transitions will never complete and the restoration will stop at the " +
                    "first stage that asks for one. Drag the GameFlow object into the " +
                    "Restoration field.", this);
            }

            if (poster == null)
            {
                Debug.LogWarning(
                    "[RestorationPresenter] No Poster assigned. Transitions will still " +
                    "advance, but the poster will not follow the visible screen.", this);
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Start()
        {
            // GameFlowController routes the first screen in its own Start, and
            // script execution order between the two is undefined. Applying the
            // router's current screen here covers the case where that already
            // happened; the ScreenChanged subscription covers the case where it
            // has not.
            ApplyScreen(router != null ? router.Current : GameScreen.None);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            if (restoration != null)
            {
                restoration.TransitionRequested += OnTransitionRequested;
            }

            if (router != null)
            {
                router.ScreenChanged += ApplyScreen;
            }

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            if (restoration != null)
            {
                restoration.TransitionRequested -= OnTransitionRequested;
            }

            if (router != null)
            {
                router.ScreenChanged -= ApplyScreen;
            }

            _subscribed = false;
        }

        // ---- Transitions -------------------------------------------------------------

        private void OnTransitionRequested(StageTransition transition, RestorationStageData stage)
        {
            switch (transition)
            {
                case StageTransition.FlipToBack:
                case StageTransition.FlipToFrontAndMount:
                    PlayFlip(stage);
                    break;

                // SwapToLinenTools is the tool bar's business and it is already
                // subscribed; GoToFinishedRepair is the router's, driven by the
                // PosterCompleted that AdvanceOrFinish is about to raise. Both
                // just need the state machine let go of.
                case StageTransition.SwapToLinenTools:
                case StageTransition.GoToFinishedRepair:
                default:
                    StartCoroutine(ContinueNextFrame());
                    break;
            }
        }

        private void PlayFlip(RestorationStageData stage)
        {
            Sprite front = stage != null ? stage.toSprite : null;
            Sprite back = NextStageEntrySprite();

            if (posterFlip == null || poster == null)
            {
                Debug.LogWarning(
                    "[RestorationPresenter] A flip was requested but no Card Flip Animator " +
                    "is wired. Skipping the animation.", this);
                StartCoroutine(ContinueNextFrame());
                return;
            }

            // The animator drives a single Image. A completed stage leaves the
            // top layer fully revealed over it, so collapse the stack to one
            // plain layer for the duration of the turn. The next stage's
            // SetStage rebuilds both layers a moment later.
            poster.SetStage(null);

            posterFlip.Flip(front, back, OnFlipLanded);
        }

        private void OnFlipLanded()
        {
            // Undo the landed 180 and its counter-mirror before anything
            // reparents this rect, or the next screen shows the art backwards.
            posterFlip?.ResetOrientation();
            restoration?.ContinueAfterTransition();
        }

        /// <summary>
        /// The artwork the next stage opens on. Both a normal stage (empty mask,
        /// bottom layer showing) and an inverted one (empty mask, top layer not
        /// yet erased) start by showing fromSprite, so this is what the card must
        /// be holding when the flip lands.
        /// </summary>
        private Sprite NextStageEntrySprite()
        {
            if (restoration == null)
            {
                return null;
            }

            PosterData data = restoration.Poster;
            if (data == null)
            {
                return null;
            }

            RestorationStageData next = data.GetStage(restoration.CurrentStageIndex + 1);
            return next != null ? next.fromSprite : null;
        }

        private IEnumerator ContinueNextFrame()
        {
            // One frame, so every other TransitionRequested subscriber has run
            // before the next stage starts. See KEY DECISIONS.
            yield return null;

            restoration?.ContinueAfterTransition();
        }

        // ---- Poster placement --------------------------------------------------------

        private void ApplyScreen(GameScreen screen)
        {
            if (poster == null || _posterRect == null)
            {
                return;
            }

            RectTransform host = ResolveStackRoot(screen);

            if (host == null)
            {
                // Journal and FinishedRepair draw their own poster artwork.
                poster.gameObject.SetActive(false);
                return;
            }

            if (_posterRect.parent != host)
            {
                _posterRect.SetParent(host, false);
            }

            // The stack root IS the authored framing for this screen, so the
            // poster fills it exactly rather than carrying per-screen offsets.
            _posterRect.anchorMin = Vector2.zero;
            _posterRect.anchorMax = Vector2.one;
            _posterRect.offsetMin = Vector2.zero;
            _posterRect.offsetMax = Vector2.zero;
            _posterRect.pivot = new Vector2(0.5f, 0.5f);
            _posterRect.localScale = Vector3.one;

            posterFlip?.ResetOrientation();

            poster.gameObject.SetActive(true);
        }

        private RectTransform ResolveStackRoot(GameScreen screen)
        {
            if (router == null || screen == GameScreen.None)
            {
                return null;
            }

            if (!router.TryGetView(screen, out ScreenView view))
            {
                return null;
            }

            return view is RestorationScreenBase restorationScreen
                ? restorationScreen.PosterStackRoot
                : null;
        }
    }
}
