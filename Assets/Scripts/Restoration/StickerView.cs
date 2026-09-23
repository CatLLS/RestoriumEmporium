// ============================================================
// StickerView — one tappable sticker on the sticker-removal close-up.
// WHAT & WHY: Each sticker is a small Image the player taps; on the tap it lifts,
//   tilts, and falls off the bottom of the screen. This component is that one
//   sticker: it shows the sprite, catches the tap, and plays the peel motion
//   (StickerPeelMotion) frame by frame. It knows nothing about how many stickers
//   there are, sounds, the tutorial or the stage — StickerRemovalScreen owns all
//   of that and hears about the tap through a callback.
// KEY DECISIONS:
//   - Reacts on pointer DOWN, not click. A click waits for the finger to lift and
//     be released over the same spot; a peel that starts the instant you touch it
//     is what makes it feel physical.
//   - Views are pooled and REUSED by the screen (Bind/Unbind). Re-entering the
//     stage re-binds the same objects instead of instantiating new ones.
//   - The motion runs on UNSCALED time, so a paused Time.timeScale (the pause
//     overlay) does not freeze a sticker in mid-air, and the fall reads the same
//     at any frame rate because the pose is sampled from elapsed time, not
//     integrated.
//   - No allocations per frame: the callback is stored once at Bind, Update only
//     writes three transform values, and Update is disabled (enabled = false)
//     whenever the sticker is not falling, so idle stickers cost nothing.
//   - The fall distance is measured once, at the tap, from the sticker's current
//     position to the bottom edge of the ROOT canvas (plus the sticker's own
//     size), so "off the bottom of the screen" is true on every phone shape.
//   - The falling sticker is moved to the last sibling so it passes in front of
//     the stickers still on the poster, as a real one would.
//   - Hidden = Image disabled, not GameObject deactivated. The screen binds and
//     clears stickers from its own OnEnable/OnDisable, and toggling child
//     GameObjects while their parent is being (de)activated is exactly what Unity
//     refuses to do. Toggling a component is always safe and just as cheap.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Usually NOTHING: StickerRemovalScreen creates these at runtime (one per
//     sticker in the stage asset) when its "Sticker Template" field is empty.
// [ ] Optional, if you want to style the stickers by hand (e.g. add a drop
//     shadow child): right-click the StickerRemovalScreen's StickerRoot ->
//     UI -> Image, name it "StickerTemplate", Add Component -> Sticker View,
//     leave Source Image empty, keep "Raycast Target" TICKED, then UNTICK the
//     object's checkbox (the template itself stays hidden) and drag it into
//     StickerRemovalScreen -> Sticker Template.
// ---------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoriumEmporium.Restoration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class StickerView : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("The sticker artwork. Left empty, the Image on this object is used.")]
        [SerializeField] private Image image;

        private RectTransform _rect;
        private Canvas _rootCanvas;
        private Action<StickerView> _onTapped;
        private Action<StickerView> _onFallen;

        private StickerPeelMotion _motion;
        private Vector2 _restPosition;
        private float _restRotation;
        private float _elapsed;
        private float _duration;
        private float _fallDistance;
        private int _direction = 1;
        private bool _falling;
        private bool _interactable;

        /// <summary>The index into the stage's stickers array this view shows, or -1.</summary>
        public int Index { get; private set; } = -1;

        /// <summary>True while the peel-and-fall animation is playing.</summary>
        public bool IsFalling => _falling;

        public RectTransform Rect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = (RectTransform)transform;
                }

                return _rect;
            }
        }

        private void Awake()
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            // Idle stickers need no Update at all.
            enabled = _falling;
        }

        /// <summary>
        /// Shows this view as sticker <paramref name="index"/>: sprite, local centre
        /// position (relative to the sticker root's centre), size and rotation.
        /// </summary>
        public void Bind(
            int index, Sprite sprite, Vector2 localCentre, Vector2 size, float rotationDegrees,
            Action<StickerView> onTapped, Action<StickerView> onFallen)
        {
            if (image == null)
            {
                image = GetComponent<Image>();
            }

            Index = index;
            _onTapped = onTapped;
            _onFallen = onFallen;
            _falling = false;
            _interactable = true;
            enabled = false;

            if (image != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }

            SetVisible(sprite != null);

            RectTransform rect = Rect;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            _restPosition = localCentre;
            _restRotation = rotationDegrees;
            ApplyRestPose();
        }

        /// <summary>Moves a bound sticker (layout change) without restarting anything.</summary>
        public void Relayout(Vector2 localCentre, Vector2 size)
        {
            _restPosition = localCentre;
            Rect.sizeDelta = size;

            if (!_falling)
            {
                ApplyRestPose();
            }
        }

        /// <summary>Hides the view and forgets its callbacks. Safe to call repeatedly.</summary>
        public void Unbind()
        {
            Index = -1;
            _onTapped = null;
            _onFallen = null;
            _falling = false;
            _interactable = false;
            enabled = false;
            SetVisible(false);
            ApplyRestPose();
        }

        /// <summary>Stops taps without hiding (e.g. while the stage is completing).</summary>
        public void SetInteractable(bool value)
        {
            _interactable = value;
        }

        /// <summary>
        /// Starts the peel-and-fall. Called by the screen right after it accepted
        /// the tap, so the screen stays the one place that decides what counts.
        /// </summary>
        public void Peel(StickerPeelMotion motion)
        {
            if (_falling)
            {
                return;
            }

            _motion = motion;
            _falling = true;
            _interactable = false;
            _elapsed = 0f;

            // Drift away from the middle of the close-up.
            _direction = _restPosition.x >= 0f ? 1 : -1;

            _fallDistance = MeasureFallDistance();
            _duration = _motion.DurationFor(_fallDistance);

            if (image != null)
            {
                // Not tappable any more, but still drawn while it falls.
                image.raycastTarget = false;
            }

            Rect.SetAsLastSibling();
            enabled = true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_interactable || _falling || Index < 0)
            {
                return;
            }

            _onTapped?.Invoke(this);
        }

        private void Update()
        {
            if (!_falling)
            {
                enabled = false;
                return;
            }

            _elapsed += Time.unscaledDeltaTime;

            StickerPose pose = _motion.Evaluate(_elapsed, _direction);
            RectTransform rect = Rect;
            rect.anchoredPosition = new Vector2(_restPosition.x + pose.OffsetX, _restPosition.y + pose.OffsetY);
            rect.localRotation = Quaternion.Euler(0f, 0f, _restRotation + pose.RotationDegrees);
            rect.localScale = new Vector3(pose.Scale, pose.Scale, 1f);

            if (_elapsed < _duration)
            {
                return;
            }

            _falling = false;
            enabled = false;
            SetVisible(false);

            Action<StickerView> fallen = _onFallen;
            fallen?.Invoke(this);
        }

        private void OnDisable()
        {
            // The screen was hidden mid-fall. It re-binds every sticker when it is
            // shown again, so just stop; no callback, nothing half-done to finish.
            _falling = false;
        }

        private void SetVisible(bool visible)
        {
            if (image != null)
            {
                image.enabled = visible;
                image.raycastTarget = visible;
            }
        }

        private void ApplyRestPose()
        {
            RectTransform rect = Rect;
            rect.anchoredPosition = _restPosition;
            rect.localRotation = Quaternion.Euler(0f, 0f, _restRotation);
            rect.localScale = Vector3.one;
        }

        /// <summary>
        /// Local-space distance from the rest position to just below the bottom
        /// edge of the root canvas, so the sticker is fully off screen at the end.
        /// </summary>
        private float MeasureFallDistance()
        {
            RectTransform rect = Rect;
            RectTransform parent = rect.parent as RectTransform;

            if (_rootCanvas == null)
            {
                Canvas nearest = GetComponentInParent<Canvas>();
                _rootCanvas = nearest != null ? nearest.rootCanvas : null;
            }

            Vector2 size = rect.rect.size;
            float margin = Mathf.Max(size.x, size.y) * _motion.liftScale;

            if (_rootCanvas == null || parent == null)
            {
                return 1000f + margin;
            }

            RectTransform canvasRect = (RectTransform)_rootCanvas.transform;

            // Everything in the canvas's own units first, then back into the
            // parent's local units (they differ only if a parent is scaled).
            Vector3 stickerInCanvas = canvasRect.InverseTransformPoint(rect.position);
            float canvasBottom = canvasRect.rect.yMin;
            float distanceCanvas = stickerInCanvas.y - canvasBottom;

            float parentScale = parent.lossyScale.y;
            float canvasScale = canvasRect.lossyScale.y;
            float toLocal = parentScale > 0.0001f ? canvasScale / parentScale : 1f;

            return Mathf.Max(0f, distanceCanvas * toLocal) + margin;
        }
    }
}
