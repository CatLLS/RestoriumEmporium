// ============================================================
// RevealMaskPainter — turns touch drags on the poster into reveal strokes.
// WHAT & WHY: The only input the restoration needs is "finger down, finger
//   moves, finger up". This component is that translation and nothing else: it
//   converts each pointer position into poster UV and forwards it to the
//   IRevealSurface. Keeping it separate from PosterLayerStack means the stage
//   runner can disable input (wrong tool, mid-transition, tutorial pause) by
//   toggling one component, without the surface losing its painted mask.
// KEY DECISIONS:
//   - uGUI EventSystem interfaces only (IPointerDownHandler / IDragHandler /
//     IPointerUpHandler). Legacy UnityEngine.Input is not merely discouraged in
//     this project, it throws: the player uses the Input System package. Going
//     through the EventSystem also gets correct behaviour for free when the
//     poster is behind another raycast target or off-screen.
//   - IDragHandler, not Update polling. The EventSystem delivers a move event
//     per input sample, and PosterLayerStack interpolates between samples, so
//     the stroke is continuous without this class ever running per frame.
//   - PaintedAt reports a SCREEN position, not UV. The FX layer spawns particles
//     in canvas space and would only have to convert UV back; reporting screen
//     space means this class never needs to know that particles exist.
//   - StrokeStarted / StrokeEnded exist alongside PaintedAt so the audio layer
//     can drive IAudioService.StartToolLoop / StopToolLoop without polling.
//   - OnDisable ends any stroke in progress. Being switched off mid-drag (a
//     stage completing under the player's finger) must not leave the surface
//     believing a stroke is still open.
//   - Zero allocation in the drag path: no closures, no lambdas, no LINQ, no
//     string work. The event invocations are plain delegate calls.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] There must be exactly one EventSystem in the scene. If the Hierarchy has
//     none: right-click in the Hierarchy -> UI -> Event System. Then select it
//     and make sure the component is "Input System UI Input Module" (Unity adds
//     this automatically for this project). If it says "Standalone Input
//     Module", click the "Replace with InputSystemUIInputModule" button on it.
// [x] Select the "Poster" object built in the PosterLayerStack checklist.
//     Add Component -> Reveal Mask Painter.
// [x] The same object must have an Image component with "Raycast Target"
//     TICKED. Without a raycast target, no pointer event ever reaches this
//     script and nothing will paint. The Image's Source Image may be None and
//     its Color's alpha may be 0 - an empty Image still receives raycasts.
// [x] Wire the field:
//       Poster Surface <- the same "Poster" object (its Poster Layer Stack)
// [x] UNTICK the checkbox next to "Reveal Mask Painter" in the Inspector so the
//     component starts disabled. RestorationController enables it only once the
//     player has picked up the stage's required tool.(user's question: but what if they stop clicking then grab the tool again?)
// [x] Optional: add a Tutorial Anchor component to the same object with
//     Anchor Id "poster.surface" so the tutorial hand can point at it.
// ---------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RestoriumEmporium.Restoration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class RevealMaskPainter : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("The poster whose mask this paints. Usually on the same object.")]
        [SerializeField] private PosterLayerStack posterSurface;

        [Tooltip("Ignore extra fingers while one is already painting.")]
        [SerializeField] private bool singlePointerOnly = true;

        private IRevealSurface _surface;
        private int _activePointerId = int.MinValue;
        private bool _painting;

        /// <summary>Screen position of every point painted, including the first.</summary>
        public event Action<Vector2> PaintedAt;

        /// <summary>Raised when a stroke begins. Argument is the screen position.</summary>
        public event Action<Vector2> StrokeStarted;

        /// <summary>Raised when a stroke ends, whether by lift or by being disabled.</summary>
        public event Action StrokeEnded;

        /// <summary>The surface being painted, as the interface.</summary>
        public IRevealSurface Surface => _surface;

        /// <summary>True while a finger is down and painting.</summary>
        public bool IsPainting => _painting;

        private void Awake()
        {
            if (posterSurface == null)
            {
                posterSurface = GetComponent<PosterLayerStack>();
            }

            _surface = posterSurface;

            if (_surface == null)
            {
                Debug.LogError(
                    "[RevealMaskPainter] No Poster Surface assigned; dragging will do " +
                    "nothing. Drag the Poster object into the Poster Surface field.", this);
            }
        }

        private void OnDisable()
        {
            // Never leave the surface with a half-open stroke.
            if (_painting)
            {
                _surface?.EndStroke();
                _painting = false;
                _activePointerId = int.MinValue;
                StrokeEnded?.Invoke();
            }
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_surface == null || posterSurface == null || eventData == null)
            {
                return;
            }

            if (singlePointerOnly && _painting)
            {
                return;
            }

            if (!posterSurface.TryScreenToUv(eventData.position, out Vector2 uv))
            {
                return;
            }

            _activePointerId = eventData.pointerId;
            _painting = true;

            _surface.BeginStroke(uv);

            StrokeStarted?.Invoke(eventData.position);
            PaintedAt?.Invoke(eventData.position);
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData)
        {
            if (!_painting || _surface == null || posterSurface == null || eventData == null)
            {
                return;
            }

            if (singlePointerOnly && eventData.pointerId != _activePointerId)
            {
                return;
            }

            if (!posterSurface.TryScreenToUv(eventData.position, out Vector2 uv))
            {
                return;
            }

            _surface.ContinueStroke(uv);
            PaintedAt?.Invoke(eventData.position);
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_painting)
            {
                return;
            }

            if (singlePointerOnly && eventData != null && eventData.pointerId != _activePointerId)
            {
                return;
            }

            _painting = false;
            _activePointerId = int.MinValue;

            _surface?.EndStroke();
            StrokeEnded?.Invoke();
        }
    }
}
