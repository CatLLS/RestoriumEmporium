// ============================================================
// SceneLoader — the three scene names, and one safe way to move between them.
// WHAT & WHY: The MVP has exactly three scenes (Title, Game, ThanksForPlaying).
//   Scene names are magic strings that Unity resolves at runtime, so a typo is a
//   silent failure at the worst possible moment. Naming them once as constants
//   and loading through one method makes that class of bug impossible.
// KEY DECISIONS:
//   - A static Instance, unlike everything else in Core, which uses serialized
//     references. Objects in the Game scene must be able to trigger a scene
//     change, and Unity cannot serialise a reference from a scene object to a
//     DontDestroyOnLoad object. A service registered in ServiceLocator would
//     work too, but scene loading is not a service anyone should be able to
//     swap out.
//   - The static Load() works even with no instance alive: it falls back to a
//     synchronous SceneManager.LoadScene. That keeps a scene opened directly in
//     the Editor (no Systems object) functional, just without the fade.
//   - The fade is two events rather than an animation this class owns. Whoever
//     draws the fade overlay lives in the UI layer; this class only knows when
//     to ask for it and how long to wait. FadeOutRequested is raised, the loader
//     waits fadeSeconds, then loads.
//   - allowSceneActivation is held off until the load reports 0.9 so the fade
//     always covers the swap, instead of the new scene popping in mid-fade on a
//     fast device.
//   - IsLoading rejects re-entry. Two taps on a Continue button would otherwise
//     start two loads, and the second one's fade-in would fight the first.
//   - No Addressables. Three scenes in the build settings is the entire need.
//   - GameSignals.Clear() runs right BEFORE the new scene is activated (both
//     load paths). That drops any listener a destroyed object forgot to remove,
//     while the new scene's objects — which subscribe in their Awake/OnEnable,
//     before Unity's sceneLoaded callback — keep their subscriptions.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Create the three scenes if they do not exist: menu File -> New Scene,
//     choose the "Basic 2D (URP)" template, then File -> Save As... into
//     Assets/Scenes/. Save them under exactly these names (spelling and
//     capitalisation matter): "Title", "Game", "ThanksForPlaying".
// [x] Open File -> Build Profiles (Unity 6) or File -> Build Settings.
//     Drag all three scenes from Assets/Scenes/ into the "Scenes In Build" list.
// [x] Order them: Title first (index 0), then Game, then ThanksForPlaying.
//     Index 0 is the scene the built app opens on.
// [x] Make sure every scene's checkbox in that list is ticked.
// [x] Select the "Systems" object in the Title scene, click "Add Component",
//     type "SceneLoader", press Enter.
// [x] Leave "Fade Seconds" at 0.25 unless the fade overlay animation is longer.
//     Whoever builds the fade overlay subscribes to FadeOutRequested and
//     FadeInRequested from their own script; nothing to wire in the Inspector.
// ---------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestoriumEmporium.Core
{
    [DisallowMultipleComponent]
    public class SceneLoader : MonoBehaviour
    {
        /// <summary>Build-settings name of the title scene.</summary>
        public const string TitleScene = "Title";

        /// <summary>Build-settings name of the single gameplay scene.</summary>
        public const string GameScene = "Game";

        /// <summary>Build-settings name of the end-card scene.</summary>
        public const string ThanksForPlayingScene = "ThanksForPlaying";

        [Header("Fade")]
        [Tooltip("Seconds to wait after FadeOutRequested before the load starts, " +
                 "and after the load before FadeInRequested. Match the overlay animation.")]
        [Range(0f, 2f)]
        [SerializeField] private float fadeSeconds = 0.25f;

        /// <summary>The live loader, or null when no Systems object is present.</summary>
        public static SceneLoader Instance { get; private set; }

        /// <summary>True between the fade-out and the fade-in. Blocks re-entry.</summary>
        public bool IsLoading { get; private set; }

        /// <summary>Raised before the load begins. Argument is the target scene name.</summary>
        public event Action<string> FadeOutRequested;

        /// <summary>Raised once the new scene is active. Argument is the scene name.</summary>
        public event Action<string> FadeInRequested;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // GameBootstrap destroys duplicate Systems objects, but this
                // guard keeps the static pointing at the survivor either way.
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Loads <paramref name="sceneName"/> through the live loader when there is
        /// one, and synchronously (no fade) when there is not.
        /// </summary>
        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] Load called with no scene name.");
                return;
            }

            if (Instance != null)
            {
                Instance.LoadAsync(sceneName);
                return;
            }

            Debug.LogWarning($"[SceneLoader] No SceneLoader in the running game; loading " +
                             $"'{sceneName}' without a fade. This is expected when a scene is " +
                             "opened directly in the Editor.");
            GameSignals.Clear();
            SceneManager.LoadScene(sceneName);
        }

        public static void LoadTitle() => Load(TitleScene);

        public static void LoadGame() => Load(GameScene);

        public static void LoadThanksForPlaying() => Load(ThanksForPlayingScene);

        /// <summary>Fades out, loads, fades in. Ignored while another load is running.</summary>
        public void LoadAsync(string sceneName)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading; ignoring the request for " +
                                 $"'{sceneName}'.", this);
                return;
            }

            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;

            FadeOutRequested?.Invoke(sceneName);

            if (fadeSeconds > 0f)
            {
                // Realtime, so a fade still finishes if something left
                // Time.timeScale at zero. Allocated per load rather than cached:
                // three loads a session make the garbage irrelevant, and a shared
                // instance abandoned mid-wait carries a stale deadline into the
                // next one.
                yield return new WaitForSecondsRealtime(fadeSeconds);
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

            if (operation == null)
            {
                // Almost always a scene missing from the build settings list.
                Debug.LogError($"[SceneLoader] Could not start loading '{sceneName}'. Is it in " +
                               "File -> Build Profiles -> Scenes In Build, and ticked?", this);
                IsLoading = false;
                FadeInRequested?.Invoke(sceneName);
                yield break;
            }

            // Hold the swap until the fade definitely covers it.
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                yield return null;
            }

            // Leak safety net: drop listeners before the old scene is torn down
            // and before the new one subscribes (see KEY DECISIONS).
            GameSignals.Clear();
            operation.allowSceneActivation = true;

            while (!operation.isDone)
            {
                yield return null;
            }

            IsLoading = false;
            FadeInRequested?.Invoke(sceneName);
        }
    }
}
