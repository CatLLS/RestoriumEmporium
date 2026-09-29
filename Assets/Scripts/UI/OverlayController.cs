// ============================================================
// OverlayController — the modal overlay stack (Game Paused, Settings) and the back key.
// WHAT & WHY: Pause and Settings are not screens: they float over whatever screen
//   is showing (a half-scrubbed poster, the desk hub) and must leave it exactly as
//   it was. This component owns which overlays are open, in what order, and what
//   the Android back button / Escape does. Screens, the hamburger buttons and the
//   tutorial only ever talk to it through OpenPause / OpenSettings /
//   OpenBuyCoins / CloseTop / AnyOpen. The coin store (BuyCoinsOverlay, "The
//   Golden Vault") is a modal too, opened from the Shop's Buy More Coins button.
// KEY DECISIONS:
//   - A STACK, not a single "current overlay": Settings opened from Pause returns
//     to Pause on back, while Settings opened from the desk hub closes straight to
//     the hub. A list used as a stack models both with no special cases.
//   - Opening the pause menu while it is already on top closes it, so tapping the
//     hamburger again resumes (Figma flow). Opening Settings while it is on top is
//     a no-op, so two listeners on one button cannot stack it twice.
//   - Input underneath is blocked by the overlays themselves: each sits on its own
//     Canvas (Override Sorting, order 30) with a full-screen raycast-target
//     background, above the tutorial (10/11) and below the CutscenePlayer. Nothing
//     underneath is disabled or re-parented, so closing restores it for free.
//   - The first open stops the tool loop sound (IAudioService.StopToolLoop): a
//     drag that was in progress when the menu opened would otherwise keep
//     scrubbing noise playing under a paused game.
//   - GameSignals.RaiseModalChanged(true/false) fires only on the 0->1 and 1->0
//     edges of the stack. The tutorial listens and waits while anything is open.
//   - Back key through the NEW Input System (Keyboard.current.escapeKey — Android's
//     back button arrives as Escape). Polled in Update: one null check and one
//     bool read per frame, no allocation. Back closes the top overlay; with nothing
//     open it presses the visible PauseButton (so back on a restoration screen
//     pauses, back on the desk hub opens Settings). Ignored while a cutscene plays.
//   - Time.timeScale is NOT touched. Videos, the screen router and the tutorial
//     all run on unscaled time, and every gameplay input is already blocked by the
//     overlay; freezing time would only add a global side effect to undo.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] In the Game scene, under the Canvas, create an empty object named exactly
//     "Overlays" (right-click Canvas -> Create Empty), anchor stretch/stretch,
//     all offsets 0. Drag it BELOW the screens and the tutorial objects in the
//     Hierarchy, but ABOVE CutscenePlayer.
// [ ] On "Overlays": Add Component -> Canvas, tick Override Sorting,
//     Order in Layer = 30. Add Component -> Graphic Raycaster.
// [ ] Add Component -> Overlay Controller (this script) on "Overlays".
// [ ] Build the two children "PauseMenuOverlay" and "SettingsOverlay" (see those
//     scripts' checklists) and drag them into "Pause Menu" and "Settings" here.
// [ ] Drag the GameFlow object into "Flow" (GameFlowController).
// [ ] Leave "Handle Back Key" ticked.
// [ ] Every hamburger button gets a PauseButton component that points at this
//     object (see PauseButton.cs).
// ---------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Cinematics;
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    public class OverlayController : MonoBehaviour
    {
        [Header("Overlays")]
        [SerializeField] private PauseMenuOverlay pauseMenu;
        [SerializeField] private SettingsOverlay settings;

        [Tooltip("The Golden Vault coin store. Built by Restorium/Scene/Build Coin Shop.")]
        [SerializeField] private BuyCoinsOverlay buyCoins;

        [Header("Flow")]
        [Tooltip("The GameFlowController on the GameFlow object. The pause menu's " +
                 "Journal and Quit buttons call into it.")]
        [SerializeField] private GameFlowController flow;

        [Header("Back key")]
        [Tooltip("Android back / Escape closes the top overlay, or opens the visible " +
                 "hamburger's overlay when none is open.")]
        [SerializeField] private bool handleBackKey = true;

        private readonly List<ModalOverlay> _stack = new List<ModalOverlay>();

        /// <summary>True while at least one modal overlay is open.</summary>
        public bool AnyOpen => _stack.Count > 0;

        /// <summary>The overlay on top, or null.</summary>
        public ModalOverlay Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        public GameFlowController Flow => flow;

        private void Awake()
        {
            if (pauseMenu != null)
            {
                pauseMenu.Bind(this);
                pauseMenu.HideImmediate();
            }

            if (settings != null)
            {
                settings.Bind(this);
                settings.HideImmediate();
            }

            if (buyCoins != null)
            {
                buyCoins.Bind(this);
                buyCoins.HideImmediate();
            }
        }

        private void OnDisable()
        {
            CloseAll();
        }

        /// <summary>Opens Game Paused. When it is already on top this closes it (hamburger = toggle).</summary>
        public void OpenPause()
        {
            if (pauseMenu == null)
            {
                Debug.LogWarning("[OverlayController] 'Pause Menu' is empty; cannot pause.", this);
                return;
            }

            if (Top == pauseMenu)
            {
                CloseTop();
                return;
            }

            Push(pauseMenu);
        }

        /// <summary>Opens Settings on top of whatever is open. No-op when Settings is already on top.</summary>
        public void OpenSettings()
        {
            if (settings == null)
            {
                Debug.LogWarning("[OverlayController] 'Settings' is empty; cannot open settings.", this);
                return;
            }

            if (Top == settings)
            {
                return;
            }

            Push(settings);
        }

        /// <summary>Opens The Golden Vault coin store. No-op when it is already on top.</summary>
        public void OpenBuyCoins()
        {
            if (buyCoins == null)
            {
                Debug.LogWarning("[OverlayController] 'Buy Coins' is empty; run Restorium/Scene/Build Coin Shop.",
                    this);
                return;
            }

            if (Top == buyCoins)
            {
                return;
            }

            Push(buyCoins);
        }

        /// <summary>Closes the top overlay. Safe when nothing is open.</summary>
        public void CloseTop()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            var top = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            top.Close();

            // The one underneath (Pause under Settings) becomes interactive again.
            if (_stack.Count > 0)
            {
                _stack[_stack.Count - 1].SetInteractable(true);
            }
            else
            {
                GameSignals.RaiseModalChanged(false);
            }
        }

        /// <summary>Closes every overlay (used before leaving the screen: Journal, Quit).</summary>
        public void CloseAll()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i] != null)
                {
                    _stack[i].Close();
                }
            }

            _stack.Clear();
            GameSignals.RaiseModalChanged(false);
        }

        private void Push(ModalOverlay overlay)
        {
            // An overlay can be in the stack only once; re-opening moves it to the top.
            var existing = _stack.IndexOf(overlay);

            if (existing >= 0)
            {
                _stack.RemoveAt(existing);
            }

            var wasEmpty = _stack.Count == 0;

            if (!wasEmpty)
            {
                _stack[_stack.Count - 1].SetInteractable(false);
            }

            _stack.Add(overlay);
            overlay.Open();

            if (wasEmpty)
            {
                // A drag in progress keeps its loop sound going under the menu otherwise.
                if (ServiceLocator.TryGet(out IAudioService audio))
                {
                    audio.StopToolLoop();
                }

                GameSignals.RaiseModalChanged(true);
            }
        }

        private void Update()
        {
            if (!handleBackKey)
            {
                return;
            }

            var keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            OnBackPressed();
        }

        /// <summary>What Android back / Escape does. Public so a UI button can reuse it.</summary>
        public void OnBackPressed()
        {
            var cutscenes = ServiceLocator.Get<ICutscenePlayer>();

            if (cutscenes != null && cutscenes.IsPlaying)
            {
                return;
            }

            if (AnyOpen)
            {
                CloseTop();
                return;
            }

            var button = PauseButton.FirstVisible();

            if (button != null)
            {
                button.Press();
            }
        }
    }

    /// <summary>
    /// Base for a modal overlay managed by OverlayController: a CanvasGroup that fades
    /// in/out on unscaled time and blocks raycasts while open. Abstract, so it is never
    /// attached on its own (that is why it can share this file).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ModalOverlay : MonoBehaviour
    {
        [Tooltip("Seconds for the fade in / out. Unscaled time.")]
        [Range(0f, 1f)]
        [SerializeField] private float fadeSeconds = 0.2f;

        private CanvasGroup _group;
        private Coroutine _fade;

        protected OverlayController Controller { get; private set; }

        public bool IsOpen { get; private set; }

        protected CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());

        internal void Bind(OverlayController controller) => Controller = controller;

        internal void HideImmediate()
        {
            IsOpen = false;
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            gameObject.SetActive(false);
        }

        internal void Open()
        {
            IsOpen = true;
            gameObject.SetActive(true);
            Group.blocksRaycasts = true;
            Group.interactable = true;
            OnOpened();
            FadeTo(1f, false);
        }

        internal void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            Group.blocksRaycasts = false;
            Group.interactable = false;
            OnClosed();

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            FadeTo(0f, true);
        }

        internal void SetInteractable(bool value)
        {
            Group.interactable = value && IsOpen;
            Group.blocksRaycasts = IsOpen;
        }

        /// <summary>Called when the overlay opens, before the fade. Refresh labels here.</summary>
        protected virtual void OnOpened() { }

        /// <summary>Called when the overlay starts closing.</summary>
        protected virtual void OnClosed() { }

        private void FadeTo(float target, bool deactivateAtEnd)
        {
            if (_fade != null)
            {
                StopCoroutine(_fade);
            }

            _fade = StartCoroutine(FadeRoutine(target, deactivateAtEnd));
        }

        private IEnumerator FadeRoutine(float target, bool deactivateAtEnd)
        {
            var start = Group.alpha;
            var t = 0f;

            while (fadeSeconds > 0f && t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                Group.alpha = Mathf.Lerp(start, target, t / fadeSeconds);
                yield return null;
            }

            Group.alpha = target;
            _fade = null;

            if (deactivateAtEnd && !IsOpen)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
