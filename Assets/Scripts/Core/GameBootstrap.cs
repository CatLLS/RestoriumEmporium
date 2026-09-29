// ============================================================
// GameBootstrap — brings the process-lifetime services online, once.
// WHAT & WHY: SaveManager, LocalizationService and AudioManager all live on one
//   persistent "Systems" object and must be reachable from scenes loaded later.
//   Batch 2 adds four more services that live exactly as long: poster progress,
//   the coin wallet, the decoration inventory and the rewarded ad; the coin
//   shop adds the coin-pack store and the purchase service on top. This
//   component owns that object's lifetime, publishes every service into
//   ServiceLocator in a fixed order, restores the player's saved settings, and
//   applies the device-level settings a mobile game needs.
// KEY DECISIONS:
//   - Registration happens here rather than in each service's own Awake, so
//     there is exactly one place that decides what is registered and in what
//     order: ISaveService -> ILocalizationService -> IAudioService ->
//     IPosterProgress -> IWallet -> IDecorationInventory -> IRewardedAd ->
//     ICoinPurchaseService. Each
//     later service is built from the earlier ones (the inventory spends
//     through the wallet, everything saves through the save service), and that
//     dependency order is only obvious when it is written out.
//   - The three rule services (PosterProgressService, PlayerWallet,
//     DecorationInventory) are plain C# objects created with 'new' — no
//     component, no Inspector field — and receive Debug.LogWarning as their
//     warning sink. That is what keeps them unit-testable outside Unity.
//   - The rewarded ad is a component because it needs a coroutine. If the
//     Systems object has no IRewardedAd component, a SimulatedRewardedAd is
//     added at runtime, so nothing new has to be wired in the Inspector. When
//     the AdMob wrapper exists it is simply put on Systems and found instead.
//   - The coin-pack store follows the rewarded-ad pattern. The RevenueCat bridge
//     (RevenueCatCoinStore, its own assembly) is a component on Systems and is
//     found through ICoinStore. In the Editor, where the RevenueCat SDK cannot
//     run, or when the bridge is missing, a SimulatedCoinStore is added instead;
//     it only sells in Development builds. The store itself is not registered:
//     everything goes through ICoinPurchaseService, which owns the grant rules.
//   - GameSignals (the static tutorial notification hub) is cleared on every
//     scene change as a leak safety net — but by SceneLoader just BEFORE the new
//     scene activates, not here on SceneManager.sceneLoaded. Unity runs the new
//     scene's Awake/OnEnable before sceneLoaded fires, so a Clear() at that
//     point would wipe the subscriptions the new scene had just made.
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
// [x] Open the Title scene (Assets/Scenes/Title.unity).
// [x] If it does not exist yet, create the systems object: menu
//     GameObject -> Create Empty, then rename it to exactly "Systems"
//     (select it, press F2, type Systems, press Enter).
// [x] With "Systems" selected, click "Add Component" and add all four of these,
//     one at a time: "GameBootstrap", "SaveManager", "LocalizationService",
//     "SceneLoader". Add the audio manager component too once it exists.
// [x] Now wire the GameBootstrap fields. Everything you drag in is on this SAME
//     "Systems" object, so drag the "Systems" object itself from the Hierarchy
//     into each slot:
//       Save Manager          <- drag "Systems"  (picks up SaveManager)
//       Localization          <- drag "Systems"  (picks up LocalizationService)
//       Audio Service Source  <- drag the object holding the audio manager
//                                component; leave empty until it exists.
//     If a drag offers you a list of components, choose the one named in
//     brackets above.
// [x] Leave "Target Frame Rate" at 60 and "Keep Screen Awake" ticked.
// [x] Turn "Systems" into a prefab: drag it from the Hierarchy into the Project
//     window folder Assets/Prefabs/ (right-click -> Create -> Folder to make it).
// [x] Open the Game scene and the ThanksForPlaying scene and make sure NEITHER
//     of them contains a "Systems" object. The one from Title survives scene
//     loads on its own; a second copy would be destroyed on arrival and is just
//     confusing.
// [x] Startup ordering needs no setup: this script carries a
//     [DefaultExecutionOrder(-100)] attribute so it runs before everything else.
//     Just do not add GameBootstrap to Edit -> Project Settings -> Script
//     Execution Order with a number above 0, which would override that.
// [ ] Batch 2: NOTHING new to wire. Poster progress, the wallet and the
//     decoration inventory are created in code, and the rewarded-ad placeholder
//     ("Simulated Rewarded Ad") is added to "Systems" automatically when you
//     press Play. You may add it by hand instead if you want to tweak it.
// [ ] Coin shop: run Restorium -> Store -> Add RevenueCat to Systems prefab. It
//     adds the RevenueCat components and fills "Coin Pack Catalog" below with
//     Assets/Data/Catalogs/CoinPackCatalog.asset. See Docs/RevenueCat.md.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Core
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Economy;
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

        [Header("Coin shop")]
        [Tooltip("The coin packs on sale (Assets/Data/Catalogs/CoinPackCatalog.asset).")]
        [SerializeField] private CoinPackCatalog coinPackCatalog;

        [Header("Device")]
        [Tooltip("Frame cap while playing. 60 on a cosy 2D game keeps the phone cool.")]
        [Range(30, 120)]
        [SerializeField] private int targetFrameRate = 60;

        [Tooltip("Stops the screen dimming during a long, tap-free restoration stage.")]
        [SerializeField] private bool keepScreenAwake = true;

        private IAudioService _audio;
        private IPosterProgress _progress;
        private IWallet _wallet;
        private IDecorationInventory _inventory;
        private IRewardedAd _rewardedAd;
        private ICoinPurchaseService _coinPurchases;
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
            // unregister the survivor's services. Reverse order of registration.
            ServiceLocator.Unregister(_coinPurchases);
            ServiceLocator.Unregister(_rewardedAd);
            ServiceLocator.Unregister(_inventory);
            ServiceLocator.Unregister(_wallet);
            ServiceLocator.Unregister(_progress);
            ServiceLocator.Unregister<IAudioService>(_audio);
            ServiceLocator.Unregister<ILocalizationService>(localization);
            ServiceLocator.Unregister<ISaveService>(saveManager);

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
                // Dropping a GameObject on a MonoBehaviour field makes Unity keep that
                // object's FIRST component, which is rarely the intended one. Look
                // along the whole object before calling it a mis-wiring.
                _audio = audioServiceSource as IAudioService
                         ?? audioServiceSource.GetComponent<IAudioService>();

                if (_audio == null)
                {
                    Debug.LogError($"[GameBootstrap] The object in Audio Service Source " +
                                   $"('{audioServiceSource.name}') has no component implementing " +
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
            // Order matters: every service below is built from the ones above it.
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

            System.Action<string> warn = message => Debug.LogWarning(message);

            _progress = new PosterProgressService(saveManager, warn);
            ServiceLocator.Register(_progress);

            _wallet = new PlayerWallet(saveManager, warn);
            ServiceLocator.Register(_wallet);

            _inventory = new DecorationInventory(saveManager, _wallet, warn);
            ServiceLocator.Register(_inventory);

            _rewardedAd = GetComponent<IRewardedAd>() ?? gameObject.AddComponent<SimulatedRewardedAd>();
            ServiceLocator.Register(_rewardedAd);

            // The RevenueCat SDK does not run in the Editor: simulate there. Explicit
            // Unity null checks, not ??: in the Editor a missing GetComponent result
            // is a "fake null" object that ?? would happily return.
            var coinStore = Application.isEditor ? null : GetComponent<ICoinStore>();

            if (!(coinStore is UnityEngine.Object storeObject) || storeObject == null)
            {
                var simulated = GetComponent<SimulatedCoinStore>();
                coinStore = simulated != null ? simulated : gameObject.AddComponent<SimulatedCoinStore>();
            }

            if (coinPackCatalog == null)
            {
                Debug.LogWarning("[GameBootstrap] 'Coin Pack Catalog' is empty; the coin shop has nothing to " +
                                 "sell. Run Restorium -> Store -> Add RevenueCat to Systems prefab.", this);
            }

            _coinPurchases = new CoinPurchaseService(coinStore, _wallet, saveManager,
                coinPackCatalog != null ? coinPackCatalog.Packs : null, warn);
            ServiceLocator.Register(_coinPurchases);

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
