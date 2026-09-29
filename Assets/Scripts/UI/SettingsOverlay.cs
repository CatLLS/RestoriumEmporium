// ============================================================
// SettingsOverlay — language, SFX and music (Figma SettingsScene 300:298).
// WHAT & WHY: Reachable from Pause (desk hub or a restoration screen) or
//   straight off the desk hub's hamburger. A ModalOverlay (see
//   OverlayController.cs), so showing/hiding/stacking/back-key are already
//   handled; this component only wires the language cycler and the two sliders.
// KEY DECISIONS:
//   - Language is a CYCLE BUTTON, not a dropdown: ILocalizationService.
//     AvailableLocales is a short, fixed list (en, pt-BR today), and
//     LocalePicker.Next (already unit-tested) picks the next one with wraparound.
//     A dropdown would need its own popup layer inside a modal that is itself
//     inside a modal stack; a single tap that cycles needs none of that.
//   - Volume sliders write BOTH IAudioService (hears the change immediately)
//     AND SaveData (survives a relaunch) AND call SaveSoon(), not Save(): a
//     slider drag can raise onValueChanged every frame, and coalescing that to
//     one write at end-of-frame is exactly what SaveSoon exists for (contract
//     §0, mobile hygiene).
//   - Sliders are re-read from SaveData every time the overlay opens
//     (OnOpened), not once in Awake: the same overlay instance can be opened
//     from Pause on different screens across a whole session.
//   - _initializingSliders guards the listener while RefreshSliders() sets
//     Slider.value on open, so opening Settings does not itself count as an
//     edit and re-save the volume that was just loaded.
//   - The Tracy flavour line (ui.settings.tracyLine) and every static caption
//     are LocalizedText components wired in the Inspector, matching the rest
//     of the codebase.
//   - Reset progress (red button) asks first: it only opens an in-overlay
//     confirm panel, and only that panel's Erase button wipes. Erase calls
//     ISaveService.ResetProgress (keeps language, volumes and the IAP ledger —
//     see SaveManager) and then reloads the Title scene, because the Game
//     scene's screens, placed decorations and tutorial all hold state read from
//     the old save; a fresh load is the only way every one of them starts clean.
//   - The confirm panel is always hidden again on open AND on close, so back
//     (which closes the whole overlay) doubles as Cancel.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y, anchor preset TOP-LEFT (Alt+Shift,
// top-left box). Pos Y is NEGATIVE: type -175 where the design says y = 175.
// Full layout in Docs/Batch2/FigmaLayout.md (SettingsScene 300:298).
//
// [ ] Under "Overlays" create an empty object named exactly SettingsOverlay,
//     anchor stretch/stretch, offsets 0.
// [ ] Add Component -> Canvas Group. Add Component -> Settings Overlay (this
//     script). Leave the object ACTIVE; OverlayController hides it at runtime.
// [ ] Children (order = draw order, first is behind):
//       Scrim            Image, stretch/stretch, colour black alpha ~0.6,
//                        Raycast Target ON.
//       Panel            Image (the settings card art), centred per Figma.
//       Title            TMP text. Localized Text, Key = ui.settings.title
//       TracyLine        TMP text. Localized Text, Key = ui.settings.tracyLine
//       BackButton       Button - TextMeshPro (an arrow icon, top-left of the
//                        panel per Figma). No label needed. Add Button Sfx.
//       LanguageRow
//         Label          TMP text, Localized Text Key = ui.settings.language
//         LanguageButton Button - TextMeshPro. Its own TMP child shows the
//                        CURRENT language name (e.g. "English") — this is
//                        NOT a LocalizedText, the script writes it directly.
//                        Add Button Sfx.
//       SfxRow
//         Label          TMP text, Localized Text Key = ui.settings.sfx
//         SfxSlider      UI -> Slider. Min Value 0, Max Value 1.
//       MusicRow
//         Label          TMP text, Localized Text Key = ui.settings.music
//         MusicSlider    UI -> Slider. Min Value 0, Max Value 1.
// [ ] Select SettingsOverlay and drag: Back Button, Language Button, Language
//     Value Label (the button's TMP child), Sfx Slider, Music Slider.
// [ ] Drag this SettingsOverlay object into OverlayController -> "Settings".
// [ ] Reset progress button + its confirm panel: open Game.unity and run
//     Restorium -> Scene -> Build Settings Reset (BuildSettingsReset.cs). It
//     adds only those children and fills the four "Reset Progress" fields.
// ---------------------------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    public class SettingsOverlay : ModalOverlay
    {
        [Header("Back")]
        [SerializeField] private Button backButton;

        [Header("Language")]
        [Tooltip("Tapping cycles through ILocalizationService.AvailableLocales.")]
        [SerializeField] private Button languageButton;

        [Tooltip("Shows the current language's own name (e.g. 'English'). Set from " +
                 "code, not a LocalizedText — it names ANOTHER language, not this one.")]
        [SerializeField] private TMP_Text languageValueLabel;

        [Header("Volume")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;

        [Header("Reset Progress")]
        [Tooltip("The red button. Only opens the confirm panel; never wipes by itself.")]
        [SerializeField] private Button resetButton;

        [Tooltip("Hidden until Reset is tapped. Holds the warning text and the two buttons below.")]
        [SerializeField] private GameObject resetConfirmPanel;

        [SerializeField] private Button resetYesButton;
        [SerializeField] private Button resetNoButton;

        private ILocalizationService _loc;
        private IAudioService _audio;
        private ISaveService _save;
        private bool _initializingSliders;

        private void Awake()
        {
            AddClick(backButton, OnBack);
            AddClick(languageButton, OnCycleLanguage);
            AddClick(resetButton, OnResetPressed);
            AddClick(resetYesButton, OnResetConfirmed);
            AddClick(resetNoButton, HideResetConfirm);

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }
        }

        private void OnDestroy()
        {
            RemoveClick(backButton, OnBack);
            RemoveClick(languageButton, OnCycleLanguage);
            RemoveClick(resetButton, OnResetPressed);
            RemoveClick(resetYesButton, OnResetConfirmed);
            RemoveClick(resetNoButton, HideResetConfirm);

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            }
        }

        protected override void OnOpened()
        {
            ResolveServices();
            RefreshLanguageLabel();
            RefreshSliders();
            HideResetConfirm();
        }

        protected override void OnClosed()
        {
            HideResetConfirm();
        }

        private void ResolveServices()
        {
            // Resolved lazily: the Systems object registers these in its own Awake,
            // whose order relative to this one Unity does not promise.
            if (_loc == null)
            {
                _loc = ServiceLocator.Get<ILocalizationService>();
            }

            if (_audio == null)
            {
                _audio = ServiceLocator.Get<IAudioService>();
            }

            if (_save == null)
            {
                _save = ServiceLocator.Get<ISaveService>();
            }
        }

        private void OnBack()
        {
            Controller?.CloseTop();
        }

        private void OnCycleLanguage()
        {
            if (_loc == null)
            {
                Debug.LogWarning("[SettingsOverlay] No ILocalizationService registered; cannot change language.", this);
                return;
            }

            var next = LocalePicker.Next(_loc.CurrentLocale, _loc.AvailableLocales);

            if (!string.IsNullOrEmpty(next))
            {
                _loc.SetLocale(next);
            }

            RefreshLanguageLabel();
        }

        private void RefreshLanguageLabel()
        {
            if (languageValueLabel == null)
            {
                return;
            }

            languageValueLabel.text = _loc != null ? _loc.DisplayNameFor(_loc.CurrentLocale) : string.Empty;
        }

        private void RefreshSliders()
        {
            _initializingSliders = true;
            var data = _save != null ? _save.Data : null;

            if (sfxSlider != null && data != null)
            {
                sfxSlider.value = Mathf.Clamp01(data.sfxVolume);
            }

            if (musicSlider != null && data != null)
            {
                musicSlider.value = Mathf.Clamp01(data.musicVolume);
            }

            _initializingSliders = false;
        }

        private void OnSfxChanged(float value)
        {
            if (_initializingSliders)
            {
                return;
            }

            _audio?.SetSfxVolume(value);

            if (_save != null && _save.Data != null)
            {
                _save.Data.sfxVolume = value;
                _save.SaveSoon();
            }
        }

        private void OnMusicChanged(float value)
        {
            if (_initializingSliders)
            {
                return;
            }

            _audio?.SetMusicVolume(value);

            if (_save != null && _save.Data != null)
            {
                _save.Data.musicVolume = value;
                _save.SaveSoon();
            }
        }

        private void OnResetPressed()
        {
            if (resetConfirmPanel != null)
            {
                resetConfirmPanel.SetActive(true);
            }
        }

        private void HideResetConfirm()
        {
            if (resetConfirmPanel != null)
            {
                resetConfirmPanel.SetActive(false);
            }
        }

        private void OnResetConfirmed()
        {
            if (_save == null)
            {
                Debug.LogWarning("[SettingsOverlay] No ISaveService registered; cannot reset progress.", this);
                HideResetConfirm();
                return;
            }

            _save.ResetProgress();
            Controller?.CloseAll();
            SceneLoader.LoadTitle();
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
