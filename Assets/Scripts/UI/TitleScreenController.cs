// ============================================================
// TitleScreenController — New Game on the title screen, and nothing else.
// WHAT & WHY: The title scene already exists (video background, New Game
//   button, audio manager). All it was missing is the one line that leaves it.
//   Kept deliberately tiny so the human-built scene stays the source of truth.
// KEY DECISIONS:
//   - The scene to load is a serialised string, not a hard-coded constant. The
//     Game scene does not exist yet at the time this is written, and a string
//     the human types once in the Inspector beats a rename that silently
//     compiles and then fails at runtime.
//   - Loads by name via SceneManager rather than by build index. Indices shift
//     whenever a scene is added to the Build Profile; names do not.
//   - Warns instead of throwing when the field is empty or the scene is not in
//     the build list. A missing scene is an Editor setup mistake, and a clear
//     console line points straight at the fix.
//   - No fade, no loading screen. The Game scene is small enough to load in a
//     frame on a phone, and an async load with a progress bar for a 200 ms wait
//     is complexity buying nothing.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] File -> Open Scene -> Assets/Scenes/SampleScene.unity
// [ ] In the Hierarchy, find the Canvas that holds the New Game button.
//     Right-click that Canvas -> Create Empty. Name it exactly: TitleScreen
// [ ] Select TitleScreen -> Add Component -> Title Screen Controller.
// [ ] Set "Game Scene Name" to exactly: Game
//     (This must match the file name of Assets/Scenes/Game.unity, without the
//      .unity extension. If the scene is named differently, type that name.)
// [ ] Drag the existing New Game button object from the Hierarchy into the
//     "New Game Button" field.
// [ ] Select the New Game button -> Add Component -> Button Sfx,
//     Sfx = Button Click.
// [ ] File -> Build Profiles -> Scene List. Make sure BOTH scenes are listed
//     and ticked: SampleScene at index 0, Game at index 1. A scene missing from
//     this list cannot be loaded at runtime, no matter how the name is spelled.
// [ ] Press Play and click New Game. If the console says the scene could not be
//     loaded, the name or the Scene List is wrong — nothing else can cause it.
// ---------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    [DisallowMultipleComponent]
    public class TitleScreenController : MonoBehaviour
    {
        [Tooltip("File name of the gameplay scene, without the .unity extension. " +
                 "It must also appear, ticked, in File -> Build Profiles -> Scene List.")]
        [SerializeField] private string gameSceneName = "Game";

        [Tooltip("The New Game button already present in the title scene.")]
        [SerializeField] private Button newGameButton;

        private void Awake()
        {
            if (newGameButton != null)
            {
                newGameButton.onClick.AddListener(StartNewGame);
            }
        }

        private void OnDestroy()
        {
            if (newGameButton != null)
            {
                newGameButton.onClick.RemoveListener(StartNewGame);
            }
        }

        /// <summary>Loads the gameplay scene. Public so a UnityEvent can call it too.</summary>
        public void StartNewGame()
        {
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogError(
                    "[TitleScreenController] Game Scene Name is empty. Set it in the " +
                    "Inspector to the name of the gameplay scene.", this);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
                return;
            }

            Debug.LogError(
                "[TitleScreenController] Scene \"" + gameSceneName + "\" is not in the " +
                "build list. Add it in File -> Build Profiles -> Scene List.", this);
        }
    }
}
