// ============================================================
// TracyOverlayView — the dialogue overlay Tracy speaks through.
// WHAT & WHY: Every tutorial beat that has something to say shows the Figma
//   TracyHelpOverlay: a full-screen scrim, a Tracy portrait with her dialogue box,
//   the line itself and a "toque para continuar" hint. This view owns that panel and
//   nothing else - it resolves a localisation key, picks the mood sprite, reveals the
//   text and reports the tap that ends the beat.
// KEY DECISIONS:
//   - Show() takes a KEY, never a sentence. No user-facing text lives in runtime
//     code; the pt-BR LocaleTable is the single source of copy.
//   - The typewriter reveal drives TMP's maxVisibleCharacters instead of reassigning
//     .text every frame. Reassigning the string would allocate and re-layout the mesh
//     on every character; maxVisibleCharacters just changes how much of an already
//     built mesh is drawn, which costs nothing and allocates nothing.
//   - Two-tap contract: the first tap completes a line that is still revealing, the
//     second advances. A player who taps fast is never punished by skipping a line
//     they have not read, and a player who wants to read never has to wait.
//   - Visibility is CanvasGroup alpha, not SetActive. The typewriter runs as a
//     coroutine on this component; deactivating the GameObject would kill it
//     mid-line and leave the overlay in a half-revealed state on the way back.
//   - blockInput is a parameter, not a constant. Dialogue beats block the whole
//     screen so a tap anywhere advances them, but a "now drag on the poster" beat
//     must let the poster be touched THROUGH the overlay, so it shows the same
//     panel with raycasts off.
//   - Taps arrive through IPointerClickHandler (uGUI EventSystem). Legacy
//     UnityEngine.Input is disabled project-wide.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Make sure the scene has an EventSystem: right-click in the Hierarchy ->
//     UI -> Event System. Without it no tap is ever received.
//     If Unity created it with a "Standalone Input Module", click the
//     "Replace with InputSystemUIInputModule" button on that component.
// [ ] Right-click your Canvas -> UI -> Image. Rename it "TracyHelpOverlay".
//     Drag it near the BOTTOM of the Canvas' children (above HelpingHand).
// [ ] Select TracyHelpOverlay. In the Rect Transform, click the Anchor Presets
//     square, then hold ALT and click the bottom-right box (stretch/stretch) so
//     it fills the whole screen.
// [ ] On its Image component set Source Image to "TracyHelpOverlayBG"
//     (Assets/Art/TracyHelpOverlay/TracyHelpOverlayBG.png) and TICK "Raycast Target".
// [ ] With TracyHelpOverlay selected, click "Add Component" -> Canvas Group.
// [ ] Click "Add Component" again and add this script (TracyOverlayView).
// [ ] Right-click TracyHelpOverlay -> UI -> Image. Rename it "TracyPortrait".
//     Set its Source Image to "tracy&DialogueBox(still)"
//     (Assets/Art/TracyHelpOverlay/). UNTICK its "Raycast Target".
//     Position it over the lower half of the screen and press "Set Native Size".
// [ ] Right-click TracyPortrait -> UI -> Text - TextMeshPro. Rename it "LineText".
//     (If Unity asks to "Import TMP Essentials", click that button once.)
//     Drag its Rect Transform to sit exactly inside the dialogue-box art.
//     UNTICK its "Raycast Target". Set Alignment to left + middle and turn on
//     "Wrapping". Set Font Size around 22.
// [ ] Right-click TracyPortrait -> UI -> Text - TextMeshPro. Rename it "TapHint".
//     Put it in the bottom-right corner of the dialogue box, Font Size around 14,
//     colour a soft grey. UNTICK its "Raycast Target".
// [ ] Select TracyHelpOverlay again and fill in the Inspector fields of this script:
//       Scrim Image        <- TracyHelpOverlay itself (its own Image component)
//       Portrait Image     <- TracyPortrait
//       Line Label         <- LineText
//       Tap Hint Label     <- TapHint
//       Still Sprite       <- Art/TracyHelpOverlay/tracy&DialogueBox(still).png
//       Happy Sprite       <- Art/TracyHelpOverlay/tracy&DialogueBox(happy).png
//       Embarrassed Sprite <- Art/TracyHelpOverlay/tracy&DialogueBox(embarassed).png
// [ ] Leave "Tap Hint Key" as ui.dialogue.tapToContinue. That key is filled in by
//     the menu Restorium -> Create Poster 1 Data.
// [ ] Drag this TracyHelpOverlay GameObject into TutorialController -> "Overlay".
// [ ] Leave the TracyHelpOverlay GameObject ACTIVE. The script fades it out itself.
// ---------------------------------------------------------------

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoriumEmporium.Tutorial
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class TracyOverlayView : MonoBehaviour, IPointerClickHandler
    {
        [Header("Pieces")]
        [Tooltip("The full-screen scrim Image. Usually the Image on this same object.")]
        [SerializeField] private Image scrimImage;

        [Tooltip("The Tracy + dialogue box artwork. Its sprite is swapped per mood.")]
        [SerializeField] private Image portraitImage;

        [Tooltip("The TextMeshPro label the line is typed into.")]
        [SerializeField] private TMP_Text lineLabel;

        [Tooltip("The small 'toque para continuar' label. Optional.")]
        [SerializeField] private TMP_Text tapHintLabel;

        [Header("Mood sprites")]
        [SerializeField] private Sprite stillSprite;
        [SerializeField] private Sprite happySprite;
        [SerializeField] private Sprite embarrassedSprite;

        [Header("Text")]
        [Tooltip("Localisation key for the tap hint. Not player-facing text - a key.")]
        [SerializeField] private string tapHintKey = "ui.dialogue.tapToContinue";

        [Tooltip("Reveal the line character by character.")]
        [SerializeField] private bool useTypewriter = true;

        [Tooltip("Characters revealed per second while typing.")]
        [Range(10f, 200f)]
        [SerializeField] private float charactersPerSecond = 45f;

        [Tooltip("Seconds after the line finishes before the tap hint appears.")]
        [Range(0f, 2f)]
        [SerializeField] private float hintDelay = 0.35f;

        /// <summary>Raised when the player taps to advance a fully revealed line.</summary>
        public event Action Tapped;

        private CanvasGroup _group;
        private Coroutine _reveal;
        private ILocalizationService _localization;
        private int _totalCharacters;

        /// <summary>True while the overlay is on screen.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>True while the typewriter is still revealing the current line.</summary>
        public bool IsRevealing { get; private set; }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();

            if (scrimImage == null)
            {
                scrimImage = GetComponent<Image>();
            }

            ApplyVisibility(false, false);
        }

        /// <summary>
        /// Shows the overlay with the line behind <paramref name="localizationKey"/>.
        /// </summary>
        /// <param name="localizationKey">Key into the active LocaleTable.</param>
        /// <param name="mood">Which Tracy portrait to show.</param>
        /// <param name="blockInput">
        /// True for dialogue beats (a tap anywhere advances). False for beats where
        /// the player must reach the game underneath, such as dragging on the poster.
        /// </param>
        public void Show(string localizationKey, TracyMood mood, bool blockInput = true)
        {
            ApplyMood(mood);
            ApplyVisibility(true, blockInput);

            var text = Resolve(localizationKey);

            if (lineLabel != null)
            {
                lineLabel.text = text;

                // Build the mesh now so characterCount is the real, wrapped count
                // rather than the raw string length (rich text tags do not count).
                lineLabel.ForceMeshUpdate();
                _totalCharacters = lineLabel.textInfo.characterCount;
            }
            else
            {
                _totalCharacters = 0;
            }

            StopReveal();
            SetHintVisible(false);

            if (useTypewriter && lineLabel != null && _totalCharacters > 0)
            {
                lineLabel.maxVisibleCharacters = 0;
                IsRevealing = true;
                _reveal = StartCoroutine(RevealRoutine());
            }
            else
            {
                CompleteReveal();
            }
        }

        /// <summary>Hides the overlay and stops any reveal in progress.</summary>
        public void Hide()
        {
            StopReveal();
            IsRevealing = false;
            SetHintVisible(false);
            ApplyVisibility(false, false);
        }

        /// <summary>Reveals the rest of the current line immediately.</summary>
        public void CompleteReveal()
        {
            StopReveal();
            IsRevealing = false;

            if (lineLabel != null)
            {
                lineLabel.maxVisibleCharacters = int.MaxValue;
            }

            SetHintVisible(true);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsVisible)
            {
                return;
            }

            // First tap finishes the line, second tap advances the step.
            if (IsRevealing)
            {
                CompleteReveal();
                return;
            }

            Tapped?.Invoke();
        }

        private IEnumerator RevealRoutine()
        {
            var shown = 0f;
            var speed = Mathf.Max(1f, charactersPerSecond);

            while (shown < _totalCharacters)
            {
                shown += Time.unscaledDeltaTime * speed;
                lineLabel.maxVisibleCharacters = Mathf.Min(_totalCharacters, Mathf.FloorToInt(shown));
                yield return null;
            }

            lineLabel.maxVisibleCharacters = int.MaxValue;
            IsRevealing = false;
            _reveal = null;

            if (hintDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(hintDelay);
            }

            SetHintVisible(true);
        }

        private void StopReveal()
        {
            if (_reveal != null)
            {
                StopCoroutine(_reveal);
                _reveal = null;
            }
        }

        private void ApplyMood(TracyMood mood)
        {
            if (portraitImage == null)
            {
                return;
            }

            Sprite sprite;

            switch (mood)
            {
                case TracyMood.Happy:
                    sprite = happySprite;
                    break;
                case TracyMood.Embarrassed:
                    sprite = embarrassedSprite;
                    break;
                default:
                    sprite = stillSprite;
                    break;
            }

            // A missing mood sprite keeps the previous one rather than blanking Tracy.
            if (sprite != null)
            {
                portraitImage.sprite = sprite;
            }
        }

        private void ApplyVisibility(bool visible, bool blockInput)
        {
            IsVisible = visible;

            if (_group != null)
            {
                _group.alpha = visible ? 1f : 0f;
                _group.blocksRaycasts = visible && blockInput;
                _group.interactable = visible && blockInput;
            }

            if (scrimImage != null)
            {
                scrimImage.raycastTarget = visible && blockInput;
            }
        }

        private void SetHintVisible(bool visible)
        {
            if (tapHintLabel == null)
            {
                return;
            }

            if (visible && string.IsNullOrEmpty(tapHintLabel.text))
            {
                tapHintLabel.text = Resolve(tapHintKey);
            }

            tapHintLabel.enabled = visible;
        }

        private string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            // Resolved lazily: the Systems object registers its services in Awake and
            // this view may wake up first.
            if (_localization == null)
            {
                _localization = ServiceLocator.Get<ILocalizationService>();
            }

            // Falling back to the key itself makes a missing row obvious on screen
            // instead of silently blank.
            return _localization != null ? _localization.Get(key) : key;
        }
    }
}
