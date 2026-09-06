// ============================================================
// PosterZoomer — moves the poster between an authored wide shot and a close-up.
// WHAT & WHY: Once the poster is mounted on the linen, the pencil stage needs
//   the player working at a much bigger scale than the mounting step did. That
//   is a camera move in spirit, but the whole game is UI on one canvas, so it
//   is done by tweening the poster's own anchoredPosition and localScale.
// KEY DECISIONS:
//   - The two framings are authored as empty RectTransforms in the scene, not
//     as numbers in this component. A designer drags a marker around in the
//     Scene view until the framing looks right, on the real device aspect,
//     which is not something anyone can do by typing offsets into a script.
//   - Only anchoredPosition and localScale are read from the markers. Copying
//     size, anchors or pivot as well would fight the layout the poster already
//     has and would make the marker a second, competing source of truth for the
//     poster's shape.
//   - One AnimationCurve drives both position and scale, so they always arrive
//     together. Two curves would let the poster finish moving before it finished
//     growing, which reads as a wobble.
//   - Coroutine-based and re-entrant: a zoom requested mid-zoom stops the
//     running one and retargets from wherever the poster currently is. Unlike
//     the card flip there is no half-state to protect - interrupting a zoom just
//     changes where it is going, which is exactly what a player expects.
//   - unscaledDeltaTime, so the move still plays under the pause overlay.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Select the "Poster" object under the Canvas -> Add Component ->
//     Poster Zoomer. Leave "Target" empty so it moves this object.
// [x] Create the two framing markers as siblings of Poster:
//       Right-click the Poster's PARENT in the Hierarchy -> Create Empty,
//       name it "PosterFraming_Wide". Add Component -> Rect Transform is
//       automatic for UI children; if the Inspector shows a plain Transform,
//       delete the object and instead duplicate Poster (Ctrl+D), rename the
//       copy, and delete every component on it except Rect Transform.
//       Duplicate that marker and name the copy "PosterFraming_Zoomed".
// [x] Give the markers the SAME anchors and pivot as Poster (copy them by hand
//     from Poster's Rect Transform), then:
//       PosterFraming_Wide  : drag it to where the poster sits while it is
//                             being mounted. Scale 1, 1, 1.
//       PosterFraming_Zoomed: drag it to the close-up framing for the pencil
//                             stage, and set its Scale to about 1.8, 1.8, 1.
// [x] UNTICK the checkbox at the top of both marker objects' Inspectors so they
//     are inactive and never drawn. Their Rect Transform values are still read.
// [x] Wire this component's fields:
//       Target       <- leave empty (uses the Poster itself)
//       Wide State   <- PosterFraming_Wide
//       Zoomed State <- PosterFraming_Zoomed
// [x] Set Duration to 0.6 and leave the Curve at its default ease-in-out.
// [x] Nothing calls this by itself. The object listening to
//     RestorationController.TransitionRequested calls ZoomIn(onComplete) when
//     it receives FlipToFrontAndMount.
// ---------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    [DisallowMultipleComponent]
    public class PosterZoomer : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("What moves. Left empty, this object's own Rect Transform.")]
        [SerializeField] private RectTransform target;

        [Tooltip("Inactive marker holding the wide framing.")]
        [SerializeField] private RectTransform wideState;

        [Tooltip("Inactive marker holding the close-up framing.")]
        [SerializeField] private RectTransform zoomedState;

        [Header("Motion")]
        [Range(0.1f, 3f)]
        [Tooltip("Seconds for a full move between the two framings.")]
        [SerializeField] private float duration = 0.6f;

        [Tooltip("Maps 0..1 time onto 0..1 of the move.")]
        [SerializeField]
        private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private RectTransform _rect;
        private Coroutine _routine;
        private bool _zoomed;

        /// <summary>True once the poster has settled on (or is heading to) the close-up.</summary>
        public bool IsZoomed => _zoomed;

        /// <summary>True while a move is playing.</summary>
        public bool IsMoving => _routine != null;

        /// <summary>Raised when a move finishes, after its onComplete callback.</summary>
        public event Action Arrived;

        private void Awake()
        {
            _rect = target != null ? target : transform as RectTransform;
        }

        /// <summary>Moves to the close-up framing.</summary>
        public void ZoomIn(Action onComplete = null)
        {
            MoveTo(true, onComplete);
        }

        /// <summary>Moves back to the wide framing.</summary>
        public void ZoomOut(Action onComplete = null)
        {
            MoveTo(false, onComplete);
        }

        /// <summary>Jumps straight to one of the framings with no animation.</summary>
        public void SetImmediate(bool zoomed)
        {
            StopRoutine();

            _zoomed = zoomed;
            RectTransform state = zoomed ? zoomedState : wideState;

            if (_rect == null)
            {
                _rect = target != null ? target : transform as RectTransform;
            }

            if (_rect == null || state == null)
            {
                return;
            }

            _rect.anchoredPosition = state.anchoredPosition;
            _rect.localScale = state.localScale;
        }

        private void MoveTo(bool zoomed, Action onComplete)
        {
            if (_rect == null)
            {
                _rect = target != null ? target : transform as RectTransform;
            }

            RectTransform state = zoomed ? zoomedState : wideState;

            if (_rect == null || state == null)
            {
                Debug.LogWarning(
                    "[PosterZoomer] Missing Target or a framing marker; the move was " +
                    "skipped. Assign Wide State and Zoomed State in the Inspector.", this);

                _zoomed = zoomed;
                onComplete?.Invoke();
                Arrived?.Invoke();
                return;
            }

            _zoomed = zoomed;

            if (!isActiveAndEnabled || duration <= 0f)
            {
                SetImmediate(zoomed);
                onComplete?.Invoke();
                Arrived?.Invoke();
                return;
            }

            // Re-entrant on purpose: retarget from wherever the poster is now.
            StopRoutine();
            _routine = StartCoroutine(MoveRoutine(state, onComplete));
        }

        private IEnumerator MoveRoutine(RectTransform state, Action onComplete)
        {
            Vector2 startPosition = _rect.anchoredPosition;
            Vector3 startScale = _rect.localScale;

            Vector2 endPosition = state.anchoredPosition;
            Vector3 endScale = state.localScale;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = curve != null ? curve.Evaluate(t) : t;

                _rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, eased);
                _rect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
                yield return null;
            }

            _rect.anchoredPosition = endPosition;
            _rect.localScale = endScale;

            _routine = null;
            onComplete?.Invoke();
            Arrived?.Invoke();
        }

        private void StopRoutine()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }
    }
}
