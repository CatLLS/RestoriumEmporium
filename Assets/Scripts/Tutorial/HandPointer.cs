// ============================================================
// HandPointer — floats the helping-hand icon over whatever the tutorial is pointing at.
// WHAT & WHY: Section 3 of the build spec asks for the provided pointer asset to be
//   positioned over a target. This component owns that: it takes a RectTransform (or
//   a TutorialAnchor id), parks the hand under it and plays a gentle bob/tap loop so
//   a first-time player's eye lands where it should.
// KEY DECISIONS:
//   - Converts the target's WORLD corners into the pointer's own parent space every
//     frame instead of copying anchoredPosition. The target usually lives under a
//     different parent with a different anchor, pivot and scale, and the canvas is
//     Screen Space - Camera at a 412x917 reference, so a straight anchoredPosition
//     copy is wrong at every resolution except the reference one. Corners -> screen
//     point -> local point is the only conversion that is correct in all of them.
//   - Follows in LateUpdate every frame while a step is active, because the target
//     moves: the tool bar swaps its icons between the cleaning and linen sets, and
//     screens animate in. It disables itself the instant there is no target, so an
//     idle tutorial costs nothing.
//   - Zero per-frame allocations: the world-corner buffer is allocated once and the
//     loop touches only structs. No LINQ, no GetComponent, no string work in it.
//   - The animation is an AnimationCurve evaluated in code rather than an Animator
//     asset, so there is no controller, no state machine and nothing extra for the
//     human to wire, and the bob distance stays tweakable in the Inspector.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] In the Hierarchy, select your Canvas (the Screen Space - Camera one with
//     Reference Resolution 412 x 917), right-click it and choose UI -> Image.
// [x] Rename that new Image to "HelpingHand" and drag it to the BOTTOM of the
//     Canvas' list of children, so that it draws on top of everything else.
// [x] Select HelpingHand. In the Inspector, on the Image component, click the small
//     circle next to "Source Image" and pick "helpingHandIcon"
//     (Assets/Art/helpingHandIcon.png).
//     If it does not show up in that picker, click the png in the Project window,
//     set Texture Type to "Sprite (2D and UI)" in the Inspector and press Apply.
// [x] Still on the Image component, UNTICK "Raycast Target". The hand must never
//     swallow a tap meant for the button underneath it.
// [x] In the Rect Transform set Width 96 and Height 96. The anchor preset does
//     not matter: Follow() converts into whatever anchor this rect uses.
// [x] SORTING - the hand must sit above both the tool bar (Order 2) and Tracy's
//     panel (Order 10), since it points at things on both:
//       Add Component -> Canvas. Tick "Override Sorting".
//       Sorting Layer = Default, Order in Layer = 11.
//       No Graphic Raycaster here: the hand never takes a tap.
// [x] Click "Add Component" and add this script (HandPointer).
// [x] Leave "Pointer" empty to use HelpingHand's own Rect Transform.
// [x] Drag your Canvas GameObject into the "Canvas" field. (Leaving it empty makes
//     the script look for a Canvas in its parents, but filling it in is safer.)
// [x] Leave "Offset" at 0, 20 so the hand sits just ABOVE the middle of whatever
//     it points at. A downward offset pushes it off the bottom of the screen on
//     the tool bar, which sits near the bottom edge already.
// [x] Drag this HelpingHand GameObject into TutorialController -> "Hand Pointer".
// [x] Leave the HelpingHand GameObject ACTIVE (its checkbox ticked). The script
//     hides the graphic by itself when there is nothing to point at.
// ---------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.Tutorial
{
    [DisallowMultipleComponent]
    public class HandPointer : MonoBehaviour
    {
        [Header("Pieces")]
        [Tooltip("The hand's Rect Transform. Leave empty to use this object's own.")]
        [SerializeField] private RectTransform pointer;

        [Tooltip("The Canvas the hand lives under. Leave empty to search the parents.")]
        [SerializeField] private Canvas canvas;

        [Header("Placement")]
        [Tooltip("Offset from the target's CENTRE, in canvas reference pixels. " +
                 "Positive Y lifts the hand; it sits slightly above the middle so it " +
                 "reads as pointing AT the target rather than covering it, without " +
                 "dropping off the bottom of the screen on the tool bar.")]
        [SerializeField] private Vector2 offset = new Vector2(0f, 20f);

        [Header("Animation")]
        [Tooltip("One full bob cycle. X runs 0..1 through the cycle, Y is 0..1 of Bob Distance.")]
        [SerializeField] private AnimationCurve bobCurve = BuildDefaultBobCurve();

        [Tooltip("How far the hand dips, in canvas reference pixels.")]
        [Range(0f, 60f)]
        [SerializeField] private float bobDistance = 16f;

        [Tooltip("Seconds for one bob. Bigger is slower and calmer.")]
        [Range(0.2f, 4f)]
        [SerializeField] private float bobPeriod = 0.9f;

        [Tooltip("How much the hand shrinks at the bottom of the bob, as a fraction.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float tapScale = 0.08f;

        // Allocated once. GetWorldCorners fills this buffer instead of returning a new array.
        private readonly Vector3[] _corners = new Vector3[4];

        private RectTransform _parentRect;
        private RectTransform _target;
        private Graphic _graphic;
        private Camera _uiCamera;
        private float _time;
        private bool _showing;

        /// <summary>True while the hand is visible and following something.</summary>
        public bool IsPointing => _showing && _target != null;

        private void Awake()
        {
            if (pointer == null)
            {
                pointer = transform as RectTransform;
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            if (pointer != null)
            {
                _parentRect = pointer.parent as RectTransform;
            }

            _graphic = GetComponent<Graphic>();

            RefreshCamera();
            SetVisible(false);
        }

        private void OnEnable()
        {
            RefreshCamera();
        }

        /// <summary>Re-reads the canvas' render camera. Call again if the render mode changes.</summary>
        public void RefreshCamera()
        {
            if (canvas == null)
            {
                _uiCamera = null;
                return;
            }

            var root = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

            // Screen Space - Overlay must be given a null camera; every other mode
            // needs the canvas' own camera or the screen-point conversion is off by
            // the camera's projection.
            _uiCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }

        /// <summary>Points the hand at an anchored element. An unknown id just hides the hand.</summary>
        public void PointAt(string anchorId)
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                Clear();
                return;
            }

            var anchor = TutorialAnchor.Find(anchorId);

            if (anchor == null)
            {
                // Fail open: a typo in a step asset costs a missing hand, never a lock-up.
                Debug.LogWarning(
                    $"[HandPointer] No enabled TutorialAnchor with id '{anchorId}'. The hand stays hidden.",
                    this);
                Clear();
                return;
            }

            PointAt(anchor.Target);
        }

        /// <summary>Points the hand at a specific rect and starts the bob loop.</summary>
        public void PointAt(RectTransform target)
        {
            if (target == null)
            {
                Clear();
                return;
            }

            _target = target;
            _time = 0f;
            RefreshCamera();
            SetVisible(true);
            Follow();
        }

        /// <summary>Stops following and hides the hand.</summary>
        public void Clear()
        {
            _target = null;
            SetVisible(false);
        }

        private void LateUpdate()
        {
            if (!_showing)
            {
                return;
            }

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                // The screen holding the target was hidden mid-step. Hide the hand
                // rather than leave it frozen over empty space.
                Clear();
                return;
            }

            _time += Time.unscaledDeltaTime;
            Follow();
        }

        private void Follow()
        {
            if (pointer == null || _parentRect == null || _target == null)
            {
                return;
            }

            var phase = bobPeriod > 0.0001f ? Mathf.Repeat(_time, bobPeriod) / bobPeriod : 0f;
            var wave = bobCurve != null ? bobCurve.Evaluate(phase) : 0f;

            _target.GetWorldCorners(_corners);

            // _corners[0] is bottom-left and _corners[2] is top-right, in world space.
            var centre = new Vector3(
                (_corners[0].x + _corners[2].x) * 0.5f,
                (_corners[0].y + _corners[2].y) * 0.5f,
                (_corners[0].z + _corners[2].z) * 0.5f);

            var screenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, centre);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _parentRect, screenPoint, _uiCamera, out var local))
            {
                // ScreenPointToLocalPointInRectangle answers relative to the PARENT's
                // pivot, but anchoredPosition is measured from this rect's own anchor.
                // Those agree only when the hand is centre-anchored; with the top-left
                // preset the checklist used to ask for, the hand lands half a canvas up
                // and to the left of its target. Shift by the gap between the two so
                // any anchor preset is correct.
                Rect parentBounds = _parentRect.rect;
                Vector2 anchorCentre = (pointer.anchorMin + pointer.anchorMax) * 0.5f;

                local.x -= (anchorCentre.x - _parentRect.pivot.x) * parentBounds.width;
                local.y -= (anchorCentre.y - _parentRect.pivot.y) * parentBounds.height;

                local.x += offset.x;
                local.y += offset.y - wave * bobDistance;
                pointer.anchoredPosition = local;
            }

            var scale = 1f - wave * tapScale;
            pointer.localScale = new Vector3(scale, scale, 1f);
        }

        private void SetVisible(bool visible)
        {
            _showing = visible;

            if (pointer != null && pointer.gameObject != gameObject)
            {
                pointer.gameObject.SetActive(visible);
                return;
            }

            // The hand IS this object, so toggle the graphic rather than the
            // GameObject: disabling the object would stop LateUpdate and the hand
            // could never bring itself back.
            if (_graphic != null)
            {
                _graphic.enabled = visible;
            }
        }

        private static AnimationCurve BuildDefaultBobCurve()
        {
            // 0 -> 1 -> 0 across the cycle, smoothed, so the hand dips and rises
            // without a visible corner at the turn-around.
            var curve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f));

            for (var i = 0; i < curve.length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }
    }
}
