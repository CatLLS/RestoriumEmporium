// ============================================================
// PauseMenuOverlay — the Game Paused modal (Figma GamePausedOverlay 283:177).
// WHAT & WHY: Journal / Settings / Continue, reachable from any restoration screen
//   or the desk hub through the hamburger. It is a ModalOverlay (see
//   OverlayController.cs), so OverlayController owns showing, hiding, stacking
//   and the back key; this component only wires its three buttons.
// KEY DECISIONS:
//   - Journal calls GameFlowController.OpenJournal(), NOT router.Go() directly.
//     OpenJournal() stops the tool loop sound and saves before routing, which is
//     exactly what "Journal from pause keeps the restoration saved; the journal
//     then shows Continue" (contract §1.7) requires, and it is GameFlowController
//     that owns that rule, not this overlay.
//   - Journal and Continue close the WHOLE stack (Controller.CloseAll()), not just
//     this overlay. Both leave the paused context entirely, so leaving Settings
//     open underneath a screen that is no longer paused would be a stray modal
//     the player cannot explain.
//   - Continue (Figma's "Quit" slot, replaced by design decision) only resumes;
//     nothing in the pause menu returns to the title screen.
//   - The flavour line and every button's caption are LocalizedText components
//     wired in the Inspector, matching the rest of the codebase's rule that
//     static text never appears in a script.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y, anchor preset TOP-LEFT (Alt+Shift,
// top-left box). Pos Y is NEGATIVE: type -175 where the design says y = 175.
// Full layout in Docs/Batch2/FigmaLayout.md (GamePausedOverlay 283:177).
//
// [ ] Under "Overlays" (see OverlayController.cs) create an empty object named
//     exactly PauseMenuOverlay, anchor stretch/stretch, offsets 0.
// [ ] Add Component -> Canvas Group. Add Component -> Pause Menu Overlay (this
//     script). Leave the object ACTIVE; OverlayController hides it at runtime.
// [ ] Children (order = draw order, first is behind):
//       Scrim          Image, stretch/stretch, colour black alpha ~0.6,
//                       Raycast Target ON (blocks the screen underneath).
//       Panel          Image (the paper/card art), centred, sized per Figma.
//       Title          TMP text inside Panel. Localized Text, Key = ui.pause.title
//       Flavour        TMP text inside Panel. Localized Text, Key = ui.pause.flavour
//       JournalButton  Button - TextMeshPro. Its label: Localized Text,
//                      Key = ui.pause.journal. Add Button Sfx (Button Click).
//       SettingsButton Button - TextMeshPro. Label Key = ui.pause.settings.
//                      Add Button Sfx.
//       ContinueButton Button - TextMeshPro. Label Key = ui.pause.continue.
//                      Add Button Sfx.
// [ ] Select PauseMenuOverlay and drag: Journal Button / Settings Button /
//     Continue Button <- the three buttons above.
// [ ] Drag this PauseMenuOverlay object into OverlayController -> "Pause Menu".
// ---------------------------------------------------------------

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    [DisallowMultipleComponent]
    public class PauseMenuOverlay : ModalOverlay
    {
        [Header("Buttons")]
        [SerializeField] private Button journalButton;
        [SerializeField] private Button settingsButton;
        [FormerlySerializedAs("quitButton")]
        [SerializeField] private Button continueButton;

        private void Awake()
        {
            AddClick(journalButton, OnJournal);
            AddClick(settingsButton, OnSettings);
            AddClick(continueButton, OnContinue);
        }

        private void OnDestroy()
        {
            RemoveClick(journalButton, OnJournal);
            RemoveClick(settingsButton, OnSettings);
            RemoveClick(continueButton, OnContinue);
        }

        private void OnJournal()
        {
            Controller?.CloseAll();
            Controller?.Flow?.OpenJournal();
        }

        private void OnSettings()
        {
            // Stays on the stack: back from Settings returns here.
            Controller?.OpenSettings();
        }

        private void OnContinue()
        {
            Controller?.CloseAll();
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
