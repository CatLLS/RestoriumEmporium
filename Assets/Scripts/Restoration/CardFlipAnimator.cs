// ============================================================
// CardFlipAnimator — code-driven 3D card flip for a UI RectTransform.
// WHAT & WHY: The poster is turned over twice in the flow (front to back for
//   the roller, back to front to be mounted) and the FinishedRepair screen
//   flips between the before and after images. All three are the same motion:
//   spin the rect around its own Y axis and change which artwork is showing at
//   the moment the card is edge-on. Doing it in code rather than with an
//   Animator asset means the sprites are arguments, the duration is a field,
//   and there is no controller asset to keep in sync with six stage assets.
// KEY DECISIONS:
//   - The face swap happens at the 90-degree crossing, where the card is
//     edge-on and one pixel wide. Swapping earlier or later shows the wrong
//     artwork for a frame, which reads as a flicker.
//   - The face is counter-mirrored whenever cos(angle) is negative. A rect
//     rotated past 90 degrees is showing its own back, so its child artwork is
//     laterally reversed; setting the face's localScale.x to -1 for that half
//     of the turn is what makes the back read the right way round. Deriving it
//     from cos(angle) rather than from a "which call is this" flag means the
//     mirror is always correct even if a flip is interrupted or the rect starts
//     at 180 degrees.
//   - A flip requested while one is already running is IGNORED, and its
//     onComplete is NOT invoked. Queueing was the alternative: two flips played
//     back to back land the card where it started, which reads as a bug rather
//     than as an animation. Every caller in this game is a scripted stage
//     transition that the RestorationController has already gated behind
//     "awaiting transition", so a second call means something is mis-wired -
//     hence the warning, so it is visible rather than silent.
//   - Perspective comes from the Canvas, which is Screen Space - Camera with a
//     perspective UI camera. Nothing here sets up a camera; a flip on an
//     Overlay canvas would look like a horizontal squash instead of a turn.
//   - unscaledDeltaTime, so a flip still plays while the game is paused (the
//     pause overlay is part of this project's flow).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] The Canvas must be able to show perspective. Select the Canvas object:
//       Render Mode  = Screen Space - Camera
//       Render Camera= the UI camera in the scene
//     Then select that UI camera and set Projection = Perspective.
//     With Render Mode = Screen Space - Overlay the flip will look flat.
// [ ] Pick the object to flip - for the poster, the "Poster" object under the
//     Canvas; for the FinishedRepair screen, its before/after Image object.
//     Select it -> Add Component -> Card Flip Animator.
// [ ] Wire its fields:
//       Target          <- leave empty to spin this object's own Rect Transform
//       Face Image      <- the Image whose Source Image should change mid-flip.
//                          For the poster this is the "BottomLayer" child; for
//                          the FinishedRepair card it is "FrontFace".
//       Face Rect       <- leave empty to use the Face Image's own Rect Transform
//       Back Face Source<- only for the FinishedRepair card: drag "BackFace" in.
//                          Leave EMPTY on the poster, which is driven from script.
// [ ] Set Duration to 0.5 (seconds). Leave the Curve at its default
//     ease-in-out; click it if you want to reshape the motion.
// [ ] This animator is ONE-SIDED: it spins the rect and swaps the sprite on the
//     single Face Image as the card goes edge-on. There is no second face being
//     revealed, so "BackFace" is only ever a place to keep the after artwork —
//     Play() switches its object off the first time it runs.
// [ ] Two ways in:
//       - From script: the object listening to
//         RestorationController.TransitionRequested calls
//         Flip(front, back, onComplete) and passes
//         RestorationController.ContinueAfterTransition as the callback.
//       - From the Inspector: Play() takes no arguments, so a UnityEvent can
//         call it. This is what FinishedRepairScreen's On Shown uses.
// ---------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.Restoration
{
    [DisallowMultipleComponent]
    public class CardFlipAnimator : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The rect that spins. Left empty, this object's own rect.")]
        [SerializeField] private RectTransform target;

        [Tooltip("The Image whose sprite is swapped as the card goes edge-on.")]
        [SerializeField] private Image faceImage;

        [Tooltip("Counter-mirrored during the second half. Left empty, Face Image's rect.")]
        [SerializeField] private RectTransform faceRect;

        [Tooltip("Optional, for Play(). The Image holding the artwork the card turns TO. " +
                 "Only its sprite is used — this animator is one-sided, so the object " +
                 "itself is switched off and never drawn.")]
        [SerializeField] private Image backFaceSource;

        [Header("Motion")]
        [Range(0.1f, 2f)]
        [Tooltip("Seconds for a full half-turn.")]
        [SerializeField] private float duration = 0.5f;

        [Tooltip("Maps 0..1 time onto 0..1 of the 180 degree turn.")]
        [SerializeField]
        private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private RectTransform _rect;
        private RectTransform _face;
        private Coroutine _routine;

        /// <summary>True while a flip is playing.</summary>
        public bool IsFlipping => _routine != null;

        /// <summary>Raised when a flip finishes, after its onComplete callback.</summary>
        public event Action Flipped;

        private void Awake()
        {
            _rect = target != null ? target : transform as RectTransform;
            _face = faceRect != null ? faceRect : (faceImage != null ? faceImage.rectTransform : null);
        }

        /// <summary>
        /// Turns the card a half-revolution around its Y axis, showing
        /// <paramref name="front"/> until it goes edge-on and <paramref name="back"/>
        /// after. <paramref name="onComplete"/> runs once the turn has landed.
        /// <para>
        /// Ignored, with a warning, if a flip is already running; in that case
        /// <paramref name="onComplete"/> is not invoked. See KEY DECISIONS.
        /// </para>
        /// </summary>
        public void Flip(Sprite front, Sprite back, Action onComplete)
        {
            if (IsFlipping)
            {
                Debug.LogWarning(
                    "[CardFlipAnimator] Flip requested while one is already playing; " +
                    "the request was ignored.", this);
                return;
            }

            if (_rect == null)
            {
                _rect = target != null ? target : transform as RectTransform;
            }

            if (_rect == null || !isActiveAndEnabled)
            {
                // Nothing to animate: still honour the callback so a state machine
                // waiting on the transition does not stall.
                SetFace(back);
                onComplete?.Invoke();
                Flipped?.Invoke();
                return;
            }

            _routine = StartCoroutine(FlipRoutine(front, back, onComplete));
        }

        /// <summary>
        /// Parameterless flip for a UnityEvent, which cannot call
        /// <see cref="Flip"/> (three arguments, one of them a delegate). Turns the
        /// card from whatever <c>Face Image</c> currently shows to
        /// <c>Back Face Source</c>'s sprite. This is what the FinishedRepair
        /// screen's On Shown event calls.
        /// </summary>
        public void Play()
        {
            if (faceImage == null)
            {
                Debug.LogWarning("[CardFlipAnimator] Play() needs a Face Image. Drag the Image " +
                                 "showing the card's front into the Face Image field.", this);
                return;
            }

            if (backFaceSource == null)
            {
                Debug.LogWarning("[CardFlipAnimator] Play() needs a Back Face Source. Drag the " +
                                 "Image holding the after artwork into that field, or call " +
                                 "Flip() from script instead.", this);
                return;
            }

            // One-sided by design: the back face is a sprite donor, not a second
            // drawn object. Left visible it would sit mirrored on top of the front.
            if (backFaceSource.gameObject.activeSelf)
            {
                backFaceSource.gameObject.SetActive(false);
            }

            Flip(faceImage.sprite, backFaceSource.sprite, null);
        }

        /// <summary>Sets the visible artwork without animating.</summary>
        public void SetFace(Sprite sprite)
        {
            if (faceImage != null)
            {
                faceImage.sprite = sprite;
                faceImage.enabled = sprite != null;
            }
        }

        /// <summary>Snaps the card back to un-rotated, un-mirrored, showing <paramref name="sprite"/>.</summary>
        public void ResetToFront(Sprite sprite)
        {
            ResetOrientation();
            SetFace(sprite);
        }

        /// <summary>
        /// Undoes a landed flip's rotation and mirror without touching the
        /// artwork. Visually a no-op — a rect at 180 degrees with a
        /// counter-mirrored face looks identical to one at 0 with a normal face —
        /// but it leaves the rect in a state that survives being reparented or
        /// flipped again, which a raw 180 does not.
        /// </summary>
        public void ResetOrientation()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_rect == null)
            {
                _rect = target != null ? target : transform as RectTransform;
            }

            if (_rect != null)
            {
                _rect.localEulerAngles = Vector3.zero;
            }

            ApplyMirror(0f);
        }

        private IEnumerator FlipRoutine(Sprite front, Sprite back, Action onComplete)
        {
            float startAngle = NormaliseAngle(_rect.localEulerAngles.y);
            float endAngle = startAngle + 180f;

            SetFace(front);
            ApplyMirror(startAngle);

            bool swapped = false;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = curve != null ? curve.Evaluate(t) : t;
                float angle = Mathf.LerpUnclamped(startAngle, endAngle, eased);

                _rect.localEulerAngles = new Vector3(0f, angle, 0f);
                ApplyMirror(angle);

                if (!swapped && Mathf.Abs(angle - startAngle) >= 90f)
                {
                    // Edge-on: this is the only frame where the swap is invisible.
                    swapped = true;
                    SetFace(back);
                }

                yield return null;
            }

            if (!swapped)
            {
                SetFace(back);
            }

            float landed = NormaliseAngle(endAngle);
            _rect.localEulerAngles = new Vector3(0f, landed, 0f);
            ApplyMirror(landed);

            _routine = null;
            onComplete?.Invoke();
            Flipped?.Invoke();
        }

        /// <summary>
        /// A rect past 90 degrees is showing its own back, so the artwork on it is
        /// laterally reversed. Flipping the face's X scale for that half undoes it.
        /// </summary>
        private void ApplyMirror(float angleDegrees)
        {
            if (_face == null)
            {
                _face = faceRect != null ? faceRect : (faceImage != null ? faceImage.rectTransform : null);
                if (_face == null)
                {
                    return;
                }
            }

            float sign = Mathf.Cos(angleDegrees * Mathf.Deg2Rad) < 0f ? -1f : 1f;
            Vector3 scale = _face.localScale;

            if (!Mathf.Approximately(scale.x, sign))
            {
                _face.localScale = new Vector3(sign, scale.y, scale.z);
            }
        }

        private static float NormaliseAngle(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
