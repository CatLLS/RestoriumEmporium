// ============================================================
// PauseButton — the hamburger icon. Every screen that can be paused has one.
// WHAT & WHY: Several screens (every restoration screen, the desk hub, the
//   journal) show the same hamburger in the top-left. Rather than each screen
//   wiring its own "open pause" logic, this tiny component is the one thing
//   every hamburger needs: a button that opens (or, on the desk hub, opens
//   Settings directly — see below) the right overlay through OverlayController.
// KEY DECISIONS:
//   - Points at OverlayController.OpenPause() by default. The desk hub's own
//     hamburger opens Settings directly instead (Section 9 of the flow: "DeskHub:
//     hamburger -> Settings overlay"), which is why "Opens" is a serialized
//     choice rather than a hard-coded call — one component, two behaviours,
//     picked in the Inspector instead of two near-duplicate scripts.
//   - FirstVisible() lets OverlayController's back-key handler find "the
//     hamburger on whatever screen is showing" without every screen registering
//     itself somewhere else. A static list of active buttons, like
//     TutorialAnchor's registry, is the same trick already used in this codebase.
//   - Registers in OnEnable / unregisters in OnDisable, matching TutorialAnchor:
//     screens are toggled via SetActive, and only an enabled button should ever
//     be "the" visible one.
//   - Press() is public and separate from the click handler so OverlayController
//     can simulate the tap (Android back with nothing open presses the visible
//     hamburger) without needing a PointerEventData.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Every restoration screen (Cleaning, the three LinenBacking variants,
//     StickerRemoval) and the desk hub needs a hamburger icon Button in its
//     top-left (Pos X ~14, Pos Y ~-38, Width 53, Height 57, Source Image =
//     Assets/Art/UI/hamburgerIcon.png, last child so nothing covers it). NONE
//     of these exist yet in Game.unity (verified: no such object is in the
//     scene) — create the Button first, THEN Add Component -> Pause Button.
//     The journal has NO hamburger by design (contract: no pause access from
//     the journal) — do not add one there.
// [ ] Drag the "Overlays" object (its OverlayController) into "Overlays".
// [ ] Leave "Opens" at "Pause" for every hamburger EXCEPT the desk hub's, which
//     the flow sends straight to Settings: set the desk hub's hamburger to
//     "Settings" instead (or simply use DeskHubScreen's own Menu Button wiring,
//     which already calls OverlayController.OpenSettings() directly — this
//     component is for every OTHER screen's hamburger).
// [ ] Add Component -> Button Sfx, Sfx = Button Click.
// [ ] Add Component -> Tutorial Anchor, Anchor Id = pause.button.
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class PauseButton : MonoBehaviour
    {
        public enum Target
        {
            Pause = 0,
            Settings = 1
        }

        [Tooltip("The Overlays object's OverlayController.")]
        [SerializeField] private OverlayController overlays;

        [Tooltip("Which overlay this hamburger opens. Every screen uses Pause except " +
                 "the desk hub, which the flow sends straight to Settings.")]
        [SerializeField] private Target opens = Target.Pause;

        private static readonly List<PauseButton> Active = new List<PauseButton>();

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Press);
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(Press);
            }
        }

        /// <summary>Opens this button's overlay. Public so OverlayController's back-key can call it directly.</summary>
        public void Press()
        {
            if (overlays == null)
            {
                Debug.LogWarning("[PauseButton] 'Overlays' is not assigned; nothing to open.", this);
                return;
            }

            if (opens == Target.Settings)
            {
                overlays.OpenSettings();
            }
            else
            {
                overlays.OpenPause();
            }
        }

        /// <summary>
        /// The most recently enabled hamburger, i.e. the one on whatever screen is
        /// currently showing. Used by OverlayController's back-key handler.
        /// </summary>
        public static PauseButton FirstVisible()
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var button = Active[i];

                if (button != null && button.isActiveAndEnabled)
                {
                    return button;
                }
            }

            return null;
        }
    }
}
