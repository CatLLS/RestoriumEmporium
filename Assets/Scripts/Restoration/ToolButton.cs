// ============================================================
// ToolButton — one slot in the tool bar: an icon, a saturation state, a tap.
// WHAT & WHY: The bar has to make "this is the tool you need right now" obvious
//   without words, because the game is played in Portuguese and read by
//   children. Draining the colour out of every tool but one does that at a
//   glance. This component owns that presentation for a single slot and knows
//   nothing about the restoration rules.
// KEY DECISIONS:
//   - Saturation is a MATERIAL property, not two sprite variants. One greyscale
//     sprite per tool would double the art and could not be tweened; the
//     UISaturation shader does it with one dot product, and _Saturation animates
//     smoothly between the two states.
//   - Each button copies the shared material with new Material(source) and
//     destroys the copy in OnDestroy. Without the copy, setting _Saturation on
//     one button would set it on all six (they share the material instance) and
//     would dirty the material asset on disk in the Editor.
//   - Rejection is a SHAKE, never a red flash or a log line. Tapping the wrong
//     tool is the game teaching its order, not the player making an error; the
//     shake reads as "not this one" and the player moves on.
//   - The shake moves anchoredPosition and always restores the exact starting
//     value, captured once at Awake. Reading the position at the start of each
//     shake would drift if two shakes overlapped.
//   - Uses the uGUI Button component so press feedback and the EventSystem's
//     navigation come for free; the listener is added in code so the human never
//     has to wire an OnClick entry by hand.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Create the saturation material: right-click in Assets/Art ->
//     Create -> Material, name it "M_UISaturation". Set its Shader (top of the
//     Inspector) to Restorium/UI/Saturation. Leave Saturation at 1.
// [x] Build one button: right-click the tool bar object in the Hierarchy ->
//     UI -> Button - TextMeshPro. Name it "ToolButton_1". When Unity asks to
//     import TMP Essentials, click "Import TMP Essentials".
//     Delete the child "Text (TMP)" object; the buttons are icon-only.
// [x] On ToolButton_1's own Image component: Source Image = None,
//     Color alpha = 0 (it is only the tap area), Raycast Target TICKED.
// [x] Right-click ToolButton_1 -> UI -> Image, name it "Icon".
//     Anchor preset stretch/stretch with all offsets 0 (hold Alt when clicking
//     the preset), Raycast Target UNTICKED.
//     Drag the M_UISaturation material into the Icon's Material field.
// [x] Select ToolButton_1 -> Add Component -> Tool Button. Wire:
//       Icon Image         <- the "Icon" child
//       Button             <- leave empty; it finds the Button on this object
//       Saturation Material<- the M_UISaturation asset in the Project window
//       Shake Target       <- leave empty to shake this object's own RectTransform
// [x] Duplicate ToolButton_1 twice (Ctrl+D) so the bar has ToolButton_1,
//     ToolButton_2 and ToolButton_3. Three slots are enough: the cleaning set
//     and the linen set each hold three tools.
// [x] Add a Tutorial Anchor component to each button. Anchor Ids are assigned
//     by the tutorial's own checklist, e.g. "toolbar.dustRemover".
// ---------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Data;

    [DisallowMultipleComponent]
    public class ToolButton : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("The Image showing the tool icon. Needs the UISaturation material.")]
        [SerializeField] private Image iconImage;

        [Tooltip("Tap target. Left empty, the Button on this object is used.")]
        [SerializeField] private Button button;

        [Tooltip("Template material using Restorium/UI/Saturation. Copied at runtime.")]
        [SerializeField] private Material saturationMaterial;

        [Tooltip("What the rejection shake moves. Left empty, this object's rect.")]
        [SerializeField] private RectTransform shakeTarget;

        [Header("Feel")]
        [Range(0.05f, 1f)]
        [Tooltip("Seconds for the colour to drain in or out.")]
        [SerializeField] private float saturationTweenSeconds = 0.2f;

        [Range(0f, 1f)]
        [Tooltip("Saturation of a tool that is not usable right now.")]
        [SerializeField] private float dimmedSaturation;

        [Range(0.05f, 0.6f)]
        [Tooltip("Seconds the rejection shake lasts.")]
        [SerializeField] private float shakeSeconds = 0.25f;

        [Range(1f, 30f)]
        [Tooltip("Pixels the rejection shake travels at its widest.")]
        [SerializeField] private float shakePixels = 8f;

        [Range(0f, 40f)]
        [Tooltip("Pixels the tool lifts while it is the one being held. The only " +
                 "cue that a tool is IN HAND rather than merely usable.")]
        [SerializeField] private float selectedRisePixels = 10f;

        [Range(0.02f, 0.5f)]
        [Tooltip("Seconds for the tool to lift or settle back.")]
        [SerializeField] private float selectedTweenSeconds = 0.12f;

        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        private Material _runtimeMaterial;
        private RectTransform _shakeRect;
        private Vector2 _restPosition;
        private Coroutine _saturationRoutine;
        private Coroutine _shakeRoutine;
        private Coroutine _riseRoutine;

        // The shake and the lift both move this rect, on different axes. They are
        // kept as separate offsets and composed in ApplyOffsets, so a rejection
        // mid-lift cannot leave the button parked at the wrong height.
        private float _shakeOffsetX;
        private float _riseOffsetY;

        private ToolData _tool;
        private float _saturation = 1f;
        private bool _usable = true;
        private bool _selected;

        /// <summary>Raised when the player taps this button. Argument is the bound tool.</summary>
        public event Action<ToolButton> Clicked;

        /// <summary>The tool currently bound to this slot, or null when the slot is empty.</summary>
        public ToolData Tool => _tool;

        /// <summary>True when this slot is the one the current stage accepts.</summary>
        public bool IsUsable => _usable;

        /// <summary>True while this tool is the one the player is holding.</summary>
        public bool IsSelected => _selected;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            _shakeRect = shakeTarget != null ? shakeTarget : transform as RectTransform;
            if (_shakeRect != null)
            {
                _restPosition = _shakeRect.anchoredPosition;
            }

            EnsureMaterial();

            if (button != null)
            {
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }

            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeMaterial);
                }
                else
                {
                    DestroyImmediate(_runtimeMaterial);
                }

                _runtimeMaterial = null;
            }
        }

        /// <summary>
        /// Binds a tool to this slot. Passing null empties and hides the slot, which
        /// is how a set with fewer tools than there are buttons is handled.
        /// </summary>
        public void SetTool(ToolData tool)
        {
            _tool = tool;

            // A rebound slot is a different tool; it must not inherit the previous
            // one's raised "in hand" pose.
            SetSelected(false, true);

            bool hasTool = tool != null;
            gameObject.SetActive(hasTool);

            if (!hasTool)
            {
                return;
            }

            if (iconImage != null)
            {
                iconImage.sprite = tool.icon;
                iconImage.enabled = tool.icon != null;
            }
        }

        /// <summary>
        /// Sets whether this tool is the one the current stage accepts. Usable tools
        /// tween to full colour, the rest drain to grey.
        /// </summary>
        public void SetUsable(bool usable, bool instant = false)
        {
            _usable = usable;

            if (button != null)
            {
                // Still tappable when dimmed: tapping a dimmed tool is how the
                // player learns the order, and it must produce the shake.
                button.interactable = true;
            }

            float target = usable ? 1f : dimmedSaturation;

            if (instant || !isActiveAndEnabled || saturationTweenSeconds <= 0f)
            {
                StopSaturationRoutine();
                ApplySaturation(target);
                return;
            }

            StopSaturationRoutine();
            _saturationRoutine = StartCoroutine(TweenSaturation(target));
        }

        /// <summary>
        /// Lifts the tool to show it is the one in hand, or settles it back.
        /// </summary>
        public void SetSelected(bool selected, bool instant = false)
        {
            _selected = selected;

            float target = selected ? selectedRisePixels : 0f;

            if (_riseRoutine != null)
            {
                StopCoroutine(_riseRoutine);
                _riseRoutine = null;
            }

            if (instant || !isActiveAndEnabled || selectedTweenSeconds <= 0f)
            {
                _riseOffsetY = target;
                ApplyOffsets();
                return;
            }

            _riseRoutine = StartCoroutine(RiseRoutine(target));
        }

        /// <summary>Soft rejection: a short horizontal shake. Never an error state.</summary>
        public void PlayRejection()
        {
            if (_shakeRect == null || !isActiveAndEnabled)
            {
                return;
            }

            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeOffsetX = 0f;
                ApplyOffsets();
            }

            _shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private void OnClicked()
        {
            Clicked?.Invoke(this);
        }

        private void EnsureMaterial()
        {
            if (_runtimeMaterial != null || iconImage == null)
            {
                return;
            }

            Material source = saturationMaterial != null ? saturationMaterial : iconImage.material;
            if (source == null)
            {
                return;
            }

            _runtimeMaterial = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            iconImage.material = _runtimeMaterial;
            ApplySaturation(_saturation);
        }

        private void ApplySaturation(float value)
        {
            _saturation = value;

            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetFloat(SaturationId, value);
            }
        }

        private void StopSaturationRoutine()
        {
            if (_saturationRoutine != null)
            {
                StopCoroutine(_saturationRoutine);
                _saturationRoutine = null;
            }
        }

        private IEnumerator TweenSaturation(float target)
        {
            float start = _saturation;
            float elapsed = 0f;

            while (elapsed < saturationTweenSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / saturationTweenSeconds);
                // Smoothstep so the drain eases instead of ramping linearly.
                ApplySaturation(Mathf.Lerp(start, target, t * t * (3f - (2f * t))));
                yield return null;
            }

            ApplySaturation(target);
            _saturationRoutine = null;
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;

            while (elapsed < shakeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / shakeSeconds);

                // Three quick swings, damped to nothing by the end.
                _shakeOffsetX = Mathf.Sin(t * Mathf.PI * 6f) * shakePixels * (1f - t);
                ApplyOffsets();
                yield return null;
            }

            _shakeOffsetX = 0f;
            ApplyOffsets();
            _shakeRoutine = null;
        }

        private IEnumerator RiseRoutine(float target)
        {
            float start = _riseOffsetY;
            float elapsed = 0f;

            while (elapsed < selectedTweenSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / selectedTweenSeconds);
                _riseOffsetY = Mathf.Lerp(start, target, t * t * (3f - (2f * t)));
                ApplyOffsets();
                yield return null;
            }

            _riseOffsetY = target;
            ApplyOffsets();
            _riseRoutine = null;
        }

        private void ApplyOffsets()
        {
            if (_shakeRect != null)
            {
                _shakeRect.anchoredPosition = new Vector2(
                    _restPosition.x + _shakeOffsetX,
                    _restPosition.y + _riseOffsetY);
            }
        }
    }
}
