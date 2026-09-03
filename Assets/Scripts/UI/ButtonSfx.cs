// ============================================================
// ButtonSfx — makes any uGUI Button click through the audio service.
// WHAT & WHY: Every button in the game should click. Wiring a UnityEvent by
//   hand on each one is a step that gets forgotten, and a forgotten one is a
//   silent button nobody notices until release. Dropping this component on the
//   Button is a single, visible action instead.
// KEY DECISIONS:
//   - Resolves IAudioService lazily at click time, not in Awake. A screen in the
//     Game scene may awake before the persistent Systems object has registered
//     itself, and caching a null at Awake would mute the button forever.
//   - Adds the listener in Awake and removes it in OnDestroy rather than in
//     OnEnable/OnDisable. The screens are disabled GameObjects that get toggled
//     constantly; add/remove on every toggle is churn for no benefit, and
//     onClick is cleaned up with the Button anyway.
//   - No audio when no service is registered. Opening a gameplay scene directly
//     in the Editor (no Systems object) should be silent, never a null-reference
//     exception on the first tap.
//   - The SfxId is serialised rather than hard-coded to ButtonClick, so the same
//     component covers the page-turn arrows without a second script.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Select a Button in the Hierarchy -> Inspector -> Add Component ->
//     type "Button Sfx" -> Enter.
// [ ] Leave Sfx at "Button Click" for normal buttons.
// [ ] Add it to every one of these objects:
//       JournalScreen/RestoreButton          Sfx = Button Click
//       JournalScreen/PrevPageButton         Sfx = Page Flip
//       JournalScreen/NextPageButton         Sfx = Page Flip
//       FinishedRepairScreen/ContinueButton  Sfx = Button Click
//       ThanksForPlaying/QuitButton          Sfx = Button Click
//       TitleScreen/NewGameButton            Sfx = Button Click
//       Every tool button in the tool bar     Sfx = Tool Select
// [ ] Nothing else to wire. The sound itself comes from the SfxLibrary asset
//     on the AudioManager, so an unauthored clip is simply silence.
// ---------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class ButtonSfx : MonoBehaviour
    {
        [Tooltip("Which logical sound this button plays. Usually Button Click.")]
        [SerializeField] private SfxId sfx = SfxId.ButtonClick;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Play);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(Play);
            }
        }

        /// <summary>Public so a UnityEvent can trigger it directly if ever needed.</summary>
        public void Play()
        {
            // Resolved per click: the Systems object may register after this Awake.
            if (ServiceLocator.TryGet(out IAudioService audio))
            {
                audio.PlaySfx(sfx);
            }
        }
    }
}
