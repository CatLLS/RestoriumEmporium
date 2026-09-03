// ============================================================
// GameBootstrap — brings the three process-lifetime services online, once.
// WHAT & WHY: SaveManager, LocalizationService and AudioManager all live on one
//   persistent "Systems" object and must be reachable from scenes loaded later.
//   This component owns that object's lifetime, publishes those three into
//   ServiceLocator, restores the player's saved settings, and applies the
//   device-level settings a mobile game needs.
// KEY DECISIONS:
//   - Registration happens here rather than in each service's own Awake, so
//     there is exactly one place that decides what is registered and in what
//     order. The save must be readable before the locale and volumes are
//     applied, and that ordering is only obvious when it is written out.
//   - The duplicate guard destroys the NEWCOMER, not the incumbent. Destroying
//     the incumbent would tear down the services other objects already hold
//     references to. It also returns from Awake before registering anything, so
//     the newcomer never overwrites the survivor's entries in the locator, and
//     its OnDestroy has nothing to undo.
//   - Audio is typed as a MonoBehaviour plus a runtime interface cast, because
//     the concrete AudioManager is authored in another part of the project and
//     Core must not depend on it. Save and localisation are typed concretely
//     since they live in the folders this bootstrap already owns.
//   - Unregister is the identity-checked overload, so a scene reload or a
//     stray duplicate can never unregister the live service out from under
//     everyone.
//   - vSyncCount is zeroed before targetFrameRate is set. Unity ignores
//     targetFrameRate entirely while vSync is on, so setting the rate alone
//     would look like it worked and silently do nothing.
//   - Screen.sleepTimeout is NeverSleep for the whole session. A restoration
//     stage can run for minutes of slow dragging with no taps at all, and having
//     the screen dim mid-stroke is the single most annoying bug a cosy game can
//     ship.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open the Title scene (Assets/Scenes/Title.unity).
// [ ] If it does not exist yet, create the systems object: menu
//     GameObject -> Create Empty, then rename it to exactly "Systems"
//     (select it, press F2, type Systems, press Enter).
// [ ] With "Systems" selected, click "Add Component" and add all four of these,
//     one at a time: "GameBootstrap", "SaveManager", "LocalizationService",
//     "SceneLoader". Add the audio manager component too once it exists.
// [ ] Now wire the GameBootstrap fields. Everything you drag in is on this SAME
//     "Systems" object, so drag the "Systems" object itself from the Hierarchy
//     into each slot:
//       Save Manager          <- drag "Systems"  (picks up SaveManager)
//       Localization          <- drag "Systems"  (picks up LocalizationService)
//       Audio Service Source  <- drag the object holding the audio manager
//                                component; leave empty until it exists.
//     If a drag offers you a list of components, choose the one named in
//     brackets above.
// [ ] Leave "Target Frame Rate" at 60 and "Keep Screen Awake" ticked.
// [ ] Turn "Systems" into a prefab: drag it from the Hierarchy into the Project
//     window folder Assets/Prefabs/ (right-click -> Create -> Folder to make it).
// [ ] Open the Game scene and the ThanksForPlaying scene and make sure NEITHER
//     of them contains a "Systems" object. The one from Title survives scene
//     loads on its own; a second copy would be destroyed on arrival and is just
//     confusing.
// [ ] Startup ordering needs no setup: this script carries a
//     [DefaultExecutionOrder(-100)] attribute so it runs before everything else.
//     Just do not add GameBootstrap to Edit -> Project Settings -> Script
//     Execution Order with a number above 0, which would override that.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Core
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Services (all on this same Systems object)")]
        [Tooltip("The SaveManager component. Drag the Systems object here.")]
        [SerializeField] private SaveManager saveManager;

        [Tooltip("The LocalizationService component. Drag the Systems object here.")]
        [SerializeField] private LocalizationService localization;

        [Tooltip("The component implementing IAudioService (AudioManager). " +
                 "Typed loosely so Core does not depend on the audio assembly's " +
                 "concrete class. Leave empty until that component exists.")]
        [SerializeField] private MonoBehaviour audioServiceSource;

        [Header("Device")]
        [Tooltip("Frame cap while playing. 60 on a cosy 2D game keeps the phone cool.")]
        [Range(30, 120)]
        [SerializeField] private int targetFrameRate = 60;

        [Tooltip("Stops the screen dimming during a long, tap-free restoration stage.")]
        [SerializeField] private bool keepScreenAwake = true;

        private IAudioService _audio;
        private bool _registered;

        /// <summary>The live bootstrap, or null before the first scene loads.</summary>
        public static GameBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A second Systems object arrived (a scene was authored with one,
                // or the Title scene was reloaded). Destroy the newcomer and leave
                // the running services untouched.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ResolveReferences();
            RegisterServices();
            ApplySavedSettings();
            ApplyDeviceSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (!_registered)
            {
                return;
            }

            // Identity-checked, so a duplicate that never registered cannot
            // unregister the survivor's services.
            ServiceLocator.Unregister<ISaveService>(saveManager);
            ServiceLocator.Unregister<ILocalizationService>(localization);
            ServiceLocator.Unregister<IAudioService>(_audio);

            _registered = false;
        }

        private void ResolveReferences()
        {
            // Falling back to the same object keeps a hand-built Systems object
            // working when someone forgets one of the drags.
            if (saveManager == null)
            {
                saveManager = GetComponent<SaveManager>();
            }

            if (localization == null)
            {
                localization = GetComponent<LocalizationService>();
            }

            if (audioServiceSource == null)
            {
                _audio = GetComponent<IAudioService>();
            }
            else
            {
                _audio = audioServiceSource as IAudioService;

                if (_audio == null)
                {
                    Debug.LogError($"[GameBootstrap] '{audioServiceSource.GetType().Name}' was " +
                                   "assigned to Audio Service Source but does not implement " +
                                   "IAudioService. The game will run silently.", this);
                }
            }

            if (saveManager == null)
            {
                Debug.LogError("[GameBootstrap] No SaveManager assigned or found on this object. " +
                               "Nothing will be saved. Add the component and drag it into the " +
                               "Save Manager field.", this);
            }

            if (localization == null)
            {
                Debug.LogError("[GameBootstrap] No LocalizationService assigned or found on this " +
                               "object. Every label will show its key. Add the component and drag " +
                               "it into the Localization field.", this);
            }
        }

        private void RegisterServices()
        {
            if (saveManager != null)
            {
                ServiceLocator.Register<ISaveService>(saveManager);
            }

            if (localization != null)
            {
                ServiceLocator.Register<ILocalizationService>(localization);
            }

            if (_audio != null)
            {
                ServiceLocator.Register<IAudioService>(_audio);
            }

            _registered = true;
        }

        private void ApplySavedSettings()
        {
            if (saveManager == null)
            {
                return;
            }

            // Reading Data loads the file if SaveManager's own Awake has not run
            // yet; the load is idempotent either way.
            var data = saveManager.Data;

            if (data == null)
            {
                return;
            }

            localization?.SetLocale(data.localeCode);

            if (_audio != null)
            {
                _audio.SetMusicVolume(Mathf.Clamp01(data.musicVolume));
                _audio.SetSfxVolume(Mathf.Clamp01(data.sfxVolume));
            }
        }

        private void ApplyDeviceSettings()
        {
            // vSync overrides targetFrameRate on the platforms that support it,
            // so it has to go first or the cap is silently ignored.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;

            Screen.sleepTimeout = keepScreenAwake
                ? SleepTimeout.NeverSleep
                : SleepTimeout.SystemSetting;
        }
    }
}
