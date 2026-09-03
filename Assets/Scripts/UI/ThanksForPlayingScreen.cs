// ============================================================
// ThanksForPlayingScreen — the closing scene: Tracy, a thank-you, and Quit.
// WHAT & WHY: The last thing the player sees. It lives in its own scene rather
//   than as a fifth view in the Game scene, because nothing here shares the
//   poster stack and keeping it separate means the Game scene can be unloaded
//   along with all its restoration state.
// KEY DECISIONS:
//   - A plain MonoBehaviour, not a ScreenView. ScreenView exists so the router
//     can toggle siblings in the Game scene; this screen IS its scene, and
//     inheriting a Show/Hide it never uses would only invite someone to call it.
//   - Quit calls Application.Quit and, in the Editor, also stops play mode.
//     Application.Quit does nothing in the Editor, so without the second branch
//     the human would tap the button, see nothing happen, and assume it is
//     broken.
//   - Every string on this screen is a localisation key on a LocalizedText
//     binder, including the long body paragraph. The one thing this script sets
//     directly is nothing at all: it only wires the button.
//   - Holds an optional reference to the body label purely so the Inspector
//     shows the human which object carries the long text; it is never written to.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y with the anchor preset set to
// TOP-LEFT (click the anchor square, hold Alt+Shift, pick the top-left box).
// Pos Y is NEGATIVE: type -74 where the design says y = 74.
//
// A) CREATE THE SCENE
// [ ] File -> New Scene -> Basic 2D (Built-in) -> Create.
// [ ] File -> Save As -> Assets/Scenes/ThanksForPlaying.unity
// [ ] File -> Build Profiles -> Scene List -> Add Open Scenes, so it can be
//     loaded at runtime. Order: SampleScene = 0, Game = 1, ThanksForPlaying = 2.
//
// B) THE CANVAS
// [ ] GameObject -> UI -> Canvas. Name it exactly: Canvas
//     Render Mode = Screen Space - Camera. Drag Main Camera into Render Camera.
//     Plane Distance = 100.
// [ ] Canvas Scaler: UI Scale Mode = Scale With Screen Size,
//     Reference Resolution X = 412, Y = 917, Match = 0.5.
// [ ] Unity adds an EventSystem object automatically. If it did NOT,
//     GameObject -> UI -> Event System. Then select it and confirm the component
//     is "Input System UI Input Module" (NOT "Standalone Input Module").
//     If it is the wrong one, click the "Replace with InputSystemUIInputModule"
//     button Unity shows on it. Buttons will not respond otherwise.
//
// C) CHILDREN, in this order (order = draw order, first is behind)
// [ ] Right-click Canvas -> Create Empty. Name: ThanksForPlayingScreen
//       Anchor stretch/stretch, Left/Right/Top/Bottom = 0.
//       Add Component -> Thanks For Playing Screen (this script).
// [ ] Right-click ThanksForPlayingScreen -> UI -> Image. Name: Background
//       Anchor stretch/stretch, all offsets 0.
//       Source Image = Assets/Art/thanksForPlaying/ThanksForPlayingBG
// [ ] Right-click ThanksForPlayingScreen -> UI -> Image. Name: TracyPortrait
//       Anchor top-left. Pos X = 141, Pos Y = -74, Width = 130, Height = 164.
//       Source Image = Assets/Art/TracyHelpOverlay/tracy&DialogueBox(happy)
// [ ] Right-click ThanksForPlayingScreen -> UI -> Text - TextMeshPro. Name: Title
//       Anchor top-left. Pos X = 100, Pos Y = -267, Width = 212, Height = 45.
//       Alignment = Center + Middle. Font Size = 34.
//       Add Component -> Localized Text, Key = ui.thanks.title
// [ ] Right-click ThanksForPlayingScreen -> UI -> Image. Name: Divider
//       Anchor top-left. Pos X = 161, Pos Y = -336, Width = 90, Height = 2.
//       Source Image = None. Colour = a dark brown, alpha 255.
// [ ] Right-click ThanksForPlayingScreen -> UI -> Text - TextMeshPro. Name: BodyText
//       Anchor top-left. Pos X = 51, Pos Y = -360, Width = 311, Height = 307.
//       Alignment = Center + Top. Font Size = 18. Tick "Wrapping".
//       Add Component -> Localized Text, Key = ui.thanks.body
// [ ] Right-click ThanksForPlayingScreen -> UI -> Button - TextMeshPro.
//       Name: QuitButton
//       Anchor top-left. Pos X = 105, Pos Y = -738, Width = 202, Height = 69.
//       Image -> Source Image = Assets/Art/journalAssets/buttonBase
//       Its child Text (TMP): anchor stretch/stretch, all offsets 0,
//       Alignment = Center + Middle, Font Size = 24.
//       Add Component -> Localized Text on that child, Key = ui.thanks.quit
//       Add Component -> Button Sfx on QuitButton, Sfx = Button Click.
//
// D) WIRE THE INSPECTOR (select ThanksForPlayingScreen and drag these in)
// [ ] Quit Button <- the QuitButton child
// [ ] Body Label  <- the BodyText child (informational only)
// ---------------------------------------------------------------

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    [DisallowMultipleComponent]
    public class ThanksForPlayingScreen : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button quitButton;

        [Tooltip("The long closing paragraph. Its text comes from a Localized Text " +
                 "component with key ui.thanks.body; this script never writes to it.")]
        [SerializeField] private TMP_Text bodyLabel;

        private void Awake()
        {
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(Quit);
            }
        }

        private void OnDestroy()
        {
            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(Quit);
            }
        }

        /// <summary>Leaves the game. Public so a UnityEvent can call it directly.</summary>
        public void Quit()
        {
#if UNITY_EDITOR
            // Application.Quit is a no-op in the Editor, which looks like a broken
            // button. Stop play mode instead, and say so.
            Debug.Log("[ThanksForPlayingScreen] Quit pressed. Stopping play mode.", this);
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
