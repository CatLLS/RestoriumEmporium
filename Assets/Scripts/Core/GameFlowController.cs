// ============================================================
// GameFlowController — the Game scene's orchestrator.
// WHAT & WHY: Something has to decide which screen the player lands on, when a
//   restoration starts, which video plays when, and what gets written to the
//   save at each milestone. Splitting that across the screens themselves is how
//   a flow ends up with several owners and a soft-lock nobody can reproduce.
//   This is the one owner: UI calls into it, it calls the router, the cutscene
//   player and the progress/wallet services.
//   Batch 2 flow: first launch -> intro video -> Journal -> poster stages ->
//   completion (coins, optional video) -> Finished Repair -> Desk Hub; the desk
//   hub's book plays the book video and opens the Journal; relaunch resumes the
//   bench, else the desk hub (once unlocked), else the journal.
// KEY DECISIONS:
//   - Strictly one-directional: this never touches a UI component. Screens hold
//     a reference to this and call StartOrContinue / OpenJournal / GoToDeskHub /
//     ... or read Posters / ActivePoster / LastCompletedPoster / CanDoubleReward.
//     Everything else is reactions to IRestorationRuntime events.
//   - It is the ONLY writer of poster progress (through IPosterProgress);
//     RestorationController no longer touches the save.
//   - Screen routing is driven by StageStarted, not by StageCompleted. The
//     stage asset already says which screen it plays on, so resume, a fresh
//     start and an ordinary advance all take the same path.
//   - StageStarted -> SetStage(index, SaveSoon); StageCompleted -> SetStage(
//     index + 1, Save). If the process dies between "stage done" and "next stage
//     started", the player resumes at the work they have not done. Clamped, so
//     the last stage cannot write an out-of-range index.
//   - Poster completion order: MarkCompleted (Save) -> pay the base reward ->
//     completion cutscene (if the poster has one and it is unseen; marked seen
//     after it ends) -> Finished Repair. The reward flag is set in memory and
//     the wallet's Add() writes flag + coins in ONE disk write, so a kill can
//     neither lose nor double the reward. Start() also pays any reward found
//     pending (completed but unclaimed) — the crash-safety half of the same rule.
//   - Videos use router.GoNow() when they finish: the video already was the
//     beat the router's hold exists to provide, and CutscenePlayer calls back
//     while it still covers the screen, so the new screen is revealed by its
//     fade-out. Everything else uses router.Go() and keeps the cosy hold.
//   - The first-launch intro is forced (not skippable) and is marked seen only
//     when it ends; a kill mid-video replays it. The book transition plays every
//     time and is skippable (Book Cutscene Skippable).
//   - A cutscene in flight sets a busy flag so a double tap on the book (or the
//     Restore button under a fading video) cannot start two flows.
//   - The restoration runtime and the cutscene player are MonoBehaviour fields
//     cast to their interfaces, because Unity cannot serialise an interface
//     field. The cutscene player falls back to ServiceLocator (it registers
//     itself), so leaving that field empty still works.
//   - Missing services (the Game scene opened directly in the Editor) are
//     warnings; the flow still routes screens so the scene can be worked on.
//   - GoToThanksForPlaying() is kept only so old button wiring does not break;
//     the Batch 2 flow never calls it.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Open the Game scene. Select the "GameFlow" object (it already has
//     ScreenRouter and GameFlowController on it — KEEP this component; do not
//     remove and re-add it, other objects reference it).
// [ ] With "GameFlow" selected, fill the GameFlowController fields:
//       Router              <- drag "GameFlow" (its ScreenRouter).
//       Restoration Source  <- drag "GameFlow" (its RestorationController).
//       Posters             <- Assets/Data/Catalogs/PosterCatalog (the asset
//                              built by Restorium -> Rebuild Catalogs & Locale
//                              Tables).
//       Cutscene Source     <- drag the "CutscenePlayer" object under the Canvas
//                              (may be left empty: it is then found at runtime).
//       Intro Cutscene      <- Assets/videos/tracysc1.mp4
//       Book Open Cutscene  <- Assets/videos/openBookTransition.mp4
//     Poster 1's completion video (tracysc2) is NOT assigned here: it lives on
//     the Poster01 asset's "Completion Cutscene" field.
// [ ] Leave "Book Cutscene Skippable" ticked (tap to skip the book video).
// [ ] Buttons are wired by the screens' own scripts (JournalScreen,
//     FinishedRepairScreen, DeskHubScreen, PauseMenuOverlay), which hold a
//     reference to this component. Remove any old On Click () entry that still
//     points at GameFlowController -> StartRestoration or GoToThanksForPlaying.
// [ ] To test a first launch, delete save.json (see SaveManager's checklist):
//     the intro video should play, then the Journal. To test the resume path:
//     start a poster, finish one stage, stop play mode, press Play again.
// ---------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Video;

namespace RestoriumEmporium.Core
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Cinematics;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Economy;
    using RestoriumEmporium.Restoration;

    [DisallowMultipleComponent]
    public class GameFlowController : MonoBehaviour
    {
        [Header("Scene wiring")]
        [Tooltip("The ScreenRouter for this scene. Usually the same GameFlow object.")]
        [SerializeField] private ScreenRouter router;

        [Tooltip("The component implementing IRestorationRuntime (RestorationController). " +
                 "Typed loosely because Unity cannot serialise an interface field.")]
        [SerializeField] private MonoBehaviour restorationSource;

        [Tooltip("The component implementing ICutscenePlayer (the 'CutscenePlayer' object). " +
                 "Optional: when empty it is found through ServiceLocator.")]
        [SerializeField] private MonoBehaviour cutsceneSource;

        [Header("Content")]
        [Tooltip("Every poster, in journal/unlock order. Assets/Data/Catalogs/PosterCatalog.")]
        [SerializeField] private PosterCatalog posters;

        [Tooltip("Plays once, on the very first launch, before the Journal. Assets/videos/tracysc1.")]
        [SerializeField] private VideoClip introCutscene;

        [Tooltip("Plays every time the desk hub's book is tapped. Assets/videos/openBookTransition.")]
        [SerializeField] private VideoClip bookOpenCutscene;

        [Tooltip("Lets a tap skip the book transition. The story videos are never skippable.")]
        [SerializeField] private bool bookCutsceneSkippable = true;

        private IRestorationRuntime _restoration;
        private ICutscenePlayer _cutscenes;
        private ISaveService _save;
        private IPosterProgress _progress;
        private IWallet _wallet;
        private IRewardedAd _rewardedAd;
        private IAudioService _audio;

        private PosterData _activePoster;
        private PosterData _lastCompleted;
        private bool _subscribed;
        private bool _cutsceneBusy;
        private bool _adInFlight;

        // ---- Public read API (for screens) -----------------------------------------

        /// <summary>Every poster, in journal order.</summary>
        public PosterCatalog Posters => posters;

        /// <summary>The poster on the workbench, or null when none is in progress.</summary>
        public PosterData ActivePoster
        {
            get
            {
                if (_activePoster != null)
                {
                    return _activePoster;
                }

                var id = _progress?.ActivePosterId;
                return !string.IsNullOrEmpty(id) && _progress.IsInProgress(id) && posters != null
                    ? posters.Find(id)
                    : null;
            }
        }

        /// <summary>
        /// The poster Finished Repair shows: the one just completed, or (after a
        /// relaunch) the last completed poster in catalogue order.
        /// </summary>
        public PosterData LastCompletedPoster
        {
            get
            {
                if (_lastCompleted != null || posters == null || _progress == null)
                {
                    return _lastCompleted;
                }

                for (var i = posters.Count - 1; i >= 0; i--)
                {
                    var poster = posters.Get(i);

                    if (poster != null && _progress.IsCompleted(poster.posterId))
                    {
                        return poster;
                    }
                }

                return null;
            }
        }

        /// <summary>True once any poster is completed.</summary>
        public bool DeskHubUnlocked => _progress != null && _progress.AnyCompleted;

        /// <summary>Last completed poster not yet doubled, and an ad is ready to show.</summary>
        public bool CanDoubleReward
        {
            get
            {
                var poster = LastCompletedPoster;

                return poster != null && poster.coinReward > 0 &&
                       !_adInFlight &&
                       _progress != null && _wallet != null && _rewardedAd != null &&
                       _progress.IsCompleted(poster.posterId) &&
                       !_progress.IsRewardDoubled(poster.posterId) &&
                       _rewardedAd.IsReady;
            }
        }

        /// <summary>Raised whenever this controller asks the router for a screen.</summary>
        public event Action<GameScreen> FlowScreenRequested;

        // ---- Lifecycle ----------------------------------------------------------------

        private void Awake()
        {
            if (router == null)
            {
                router = GetComponent<ScreenRouter>();
            }

            if (router == null)
            {
                Debug.LogError("[GameFlowController] No ScreenRouter assigned or found. The game " +
                               "cannot change screens. Drag the object holding ScreenRouter into " +
                               "the Router field.", this);
            }

            if (posters == null || posters.Count == 0)
            {
                Debug.LogError("[GameFlowController] No PosterCatalog assigned (or it is empty). Drag " +
                               "Assets/Data/Catalogs/PosterCatalog into the Posters field, and run " +
                               "Restorium -> Rebuild Catalogs & Locale Tables.", this);
            }

            ResolveRestoration();

            if (cutsceneSource != null)
            {
                _cutscenes = cutsceneSource as ICutscenePlayer
                             ?? cutsceneSource.GetComponent<ICutscenePlayer>();

                if (_cutscenes == null)
                {
                    Debug.LogError($"[GameFlowController] The object in Cutscene Source " +
                                   $"('{cutsceneSource.name}') has no CutscenePlayer component.", this);
                }
            }
        }

        private void Start()
        {
            // ServiceLocator is populated by GameBootstrap (execution order -100)
            // and CutscenePlayer registers in its Awake; Start is late enough.
            _save = ServiceLocator.Get<ISaveService>();
            _progress = ServiceLocator.Get<IPosterProgress>();
            _wallet = ServiceLocator.Get<IWallet>();
            _rewardedAd = ServiceLocator.Get<IRewardedAd>();
            _audio = ServiceLocator.Get<IAudioService>();
            _cutscenes ??= ServiceLocator.Get<ICutscenePlayer>();

            if (_save == null || _progress == null || _wallet == null)
            {
                Debug.LogWarning("[GameFlowController] Save / progress / wallet services are not " +
                                 "registered. Progress and coins will not be saved. This is expected " +
                                 "when the Game scene is opened directly in the Editor instead of " +
                                 "starting from Title.", this);
            }

            if (_cutscenes == null)
            {
                Debug.LogWarning("[GameFlowController] No CutscenePlayer in the scene. Videos will " +
                                 "be skipped.", this);
            }

            Subscribe();

            var data = _save?.Data;

            if (data != null && !data.hasPlayedBefore)
            {
                data.hasPlayedBefore = true;
                _save.Save();
            }

            PayAllPendingRewards();

            if (introCutscene != null && !HasSeen(CutsceneIds.Intro))
            {
                PlayCutscene(introCutscene, false, () =>
                {
                    MarkSeen(CutsceneIds.Intro);
                    RouteInitialScreen();
                });
                return;
            }

            RouteInitialScreen();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ---- Public entry points for UI ---------------------------------------------

        /// <summary>
        /// Journal's Restore / Continue button. Begins the poster at stage 0, or
        /// resumes it at its saved stage.
        /// </summary>
        public void StartOrContinue(PosterData poster)
        {
            if (_cutsceneBusy)
            {
                return;
            }

            if (poster == null)
            {
                Debug.LogError("[GameFlowController] StartOrContinue called with no poster.", this);
                return;
            }

            if (poster.StageCount == 0)
            {
                Debug.LogError($"[GameFlowController] '{poster.name}' has no stages. Drag the " +
                               "RestorationStage assets into its Stages list.", poster);
                return;
            }

            if (_progress != null && posters != null)
            {
                if (!_progress.IsUnlocked(poster.posterId, posters.OrderedIds))
                {
                    Debug.LogWarning($"[GameFlowController] '{poster.posterId}' is still locked. " +
                                     "Ignoring the request.", this);
                    return;
                }

                if (_progress.IsCompleted(poster.posterId))
                {
                    Debug.LogWarning($"[GameFlowController] '{poster.posterId}' is already restored. " +
                                     "Ignoring the request.", this);
                    return;
                }
            }

            var stage = _progress != null ? _progress.BeginOrResume(poster.posterId) : 0;
            BeginPosterAt(poster, Mathf.Clamp(stage, 0, poster.StageCount - 1));
        }

        /// <summary>
        /// Pause menu's Journal button. The restoration stays saved at its current
        /// stage; the journal then offers Continue on that poster.
        /// </summary>
        public void OpenJournal()
        {
            if (_cutsceneBusy)
            {
                return;
            }

            _audio?.StopToolLoop();
            _save?.Save();
            Route(GameScreen.Journal, immediate: true);
        }

        /// <summary>Desk hub's book. Plays the book video, then shows the Journal.</summary>
        public void OpenJournalFromDesk()
        {
            if (_cutsceneBusy)
            {
                return;
            }

            if (bookOpenCutscene == null || _cutscenes == null)
            {
                Route(GameScreen.Journal, immediate: false);
                return;
            }

            PlayCutscene(bookOpenCutscene, bookCutsceneSkippable,
                () => Route(GameScreen.Journal, immediate: true));
        }

        /// <summary>Finished Repair's Continue and the Journal's back button.</summary>
        public void GoToDeskHub()
        {
            if (_cutsceneBusy)
            {
                return;
            }

            if (!DeskHubUnlocked && _progress != null)
            {
                Debug.LogWarning("[GameFlowController] GoToDeskHub before any poster is completed. " +
                                 "Showing the Journal instead.", this);
                Route(GameScreen.Journal, immediate: false);
                return;
            }

            Route(GameScreen.DeskHub, immediate: false);
        }

        /// <summary>Desk hub's Shop button.</summary>
        public void GoToShop()
        {
            if (_cutsceneBusy)
            {
                return;
            }

            Route(GameScreen.Shop, immediate: false);
        }

        /// <summary>Pause menu's Quit. Saves, then loads the Title scene.</summary>
        public void QuitToTitle()
        {
            _audio?.StopToolLoop();
            _save?.Save();
            SceneLoader.LoadTitle();
        }

        /// <summary>
        /// Finished Repair's Double Reward button. Shows the rewarded ad; on a
        /// reward, pays the poster's coinReward once more. onDone(true) only when
        /// coins were actually added.
        /// </summary>
        public void RequestDoubleReward(Action<bool> onDone)
        {
            if (!CanDoubleReward)
            {
                onDone?.Invoke(false);
                return;
            }

            var poster = LastCompletedPoster;
            _adInFlight = true;

            _rewardedAd.Show(earned =>
            {
                // The ad can report after this scene is gone.
                if (this == null)
                {
                    return;
                }

                _adInFlight = false;
                var paid = false;

                // Flag first (in memory), then Add(): one disk write for both.
                if (earned && _progress.TryMarkRewardDoubled(poster.posterId))
                {
                    _wallet.Add(poster.coinReward, "double_reward:" + poster.posterId);
                    paid = true;
                }

                onDone?.Invoke(paid);
            });
        }

        /// <summary>Legacy MVP end card. Kept so old button wiring compiles; unused in Batch 2.</summary>
        public void GoToThanksForPlaying()
        {
            // A scene load is the one place a deferred write could be lost.
            _save?.Save();
            SceneLoader.Load(SceneLoader.ThanksForPlayingScene);
        }

        // ---- Flow -------------------------------------------------------------------

        private void RouteInitialScreen()
        {
            var activeId = _progress?.ActivePosterId;

            if (!string.IsNullOrEmpty(activeId) && _progress.IsInProgress(activeId))
            {
                var poster = posters != null ? posters.Find(activeId) : null;

                if (poster != null && poster.StageCount > 0)
                {
                    var index = Mathf.Clamp(_progress.GetResumeStage(activeId), 0, poster.StageCount - 1);
                    BeginPosterAt(poster, index);
                    return;
                }

                // A stage index from a poster that is no longer in the catalogue
                // would index into the wrong sequence, so treat it as no progress.
                Debug.LogWarning($"[GameFlowController] Save holds progress for poster '{activeId}', " +
                                 "which is not in the Poster Catalog. Not resuming it.", this);
            }

            // The very first route of the scene is immediate anyway (the router
            // skips its hold while nothing is on screen).
            Route(DeskHubUnlocked ? GameScreen.DeskHub : GameScreen.Journal, immediate: false);
        }

        private void BeginPosterAt(PosterData poster, int stageIndex)
        {
            _activePoster = poster;

            if (_restoration != null)
            {
                // The runtime raises StageStarted, and OnStageStarted does the routing.
                _restoration.BeginPoster(poster, stageIndex);
                return;
            }

            // No restoration runtime (not yet wired, or the scene was opened on
            // its own): still put the player on the right screen so the rest of
            // the scene can be worked on.
            var stage = poster.GetStage(stageIndex);
            Route(stage != null ? stage.screen : GameScreen.Journal, immediate: false);
        }

        private void OnStageStarted(RestorationStageData stage, int index)
        {
            if (stage == null)
            {
                return;
            }

            var poster = CurrentPoster();

            if (poster != null)
            {
                _progress?.SetStage(poster.posterId, index, immediate: false);
            }

            var data = _save?.Data;

            if (data != null)
            {
                data.lastScreen = stage.screen;
            }

            Route(stage.screen, immediate: false);
        }

        private void OnStageCompleted(RestorationStageData stage, int index)
        {
            var poster = CurrentPoster();

            if (poster == null || _progress == null)
            {
                return;
            }

            // Persist where the player should resume, which is the stage AFTER
            // the one they just finished. Clamped so the final stage stays in range;
            // PosterCompleted is what actually ends the sequence.
            var resumeIndex = Mathf.Clamp(index + 1, 0, Mathf.Max(0, poster.StageCount - 1));
            _progress.SetStage(poster.posterId, resumeIndex, immediate: true);
        }

        private void OnPosterCompleted()
        {
            var poster = CurrentPoster();

            if (poster == null)
            {
                _audio?.PlaySfx(SfxId.RestorationComplete);
                Route(GameScreen.FinishedRepair, immediate: false);
                return;
            }

            _progress?.MarkCompleted(poster.posterId);
            _lastCompleted = poster;
            _activePoster = null;

            var data = _save?.Data;

            if (data != null)
            {
                data.lastScreen = GameScreen.FinishedRepair;
            }

            PayRewardIfPending(poster);

            var cutsceneId = CutsceneIds.PosterComplete(poster.posterId);

            if (poster.completionCutscene != null && _cutscenes != null && !HasSeen(cutsceneId))
            {
                // No completion sound here: the video is the payoff, and a chime
                // over its first frames only muddies the transition.
                PlayCutscene(poster.completionCutscene, false, () =>
                {
                    MarkSeen(cutsceneId);
                    Route(GameScreen.FinishedRepair, immediate: true);
                });
                return;
            }

            _audio?.PlaySfx(SfxId.RestorationComplete);
            Route(GameScreen.FinishedRepair, immediate: false);
        }

        // ---- Rewards ------------------------------------------------------------------

        private void PayAllPendingRewards()
        {
            if (posters == null || _progress == null)
            {
                return;
            }

            for (var i = 0; i < posters.Count; i++)
            {
                var poster = posters.Get(i);

                if (poster != null && _progress.IsRewardPending(poster.posterId))
                {
                    Debug.Log($"[GameFlowController] Paying the reward for '{poster.posterId}' that " +
                              "a previous session completed but did not pay.", this);
                    PayRewardIfPending(poster);
                }
            }
        }

        private void PayRewardIfPending(PosterData poster)
        {
            if (_progress == null || !_progress.IsRewardPending(poster.posterId))
            {
                return;
            }

            if (poster.coinReward > 0 && _wallet == null)
            {
                // Leave it pending: a later session with a wallet pays it.
                return;
            }

            // Flag first (in memory + SaveSoon), then Add() writes both at once.
            if (!_progress.TryMarkRewardClaimed(poster.posterId))
            {
                return;
            }

            if (poster.coinReward > 0)
            {
                _wallet.Add(poster.coinReward, "poster_complete:" + poster.posterId);
            }
            else
            {
                _save?.Save();
            }
        }

        // ---- Cutscenes ----------------------------------------------------------------

        private void PlayCutscene(VideoClip clip, bool skippable, Action then)
        {
            if (_cutscenes == null || clip == null)
            {
                then();
                return;
            }

            _cutsceneBusy = true;

            _cutscenes.Play(clip, skippable, () =>
            {
                // The player can call back while the scene is being torn down.
                if (this == null)
                {
                    return;
                }

                _cutsceneBusy = false;
                then();
            });
        }

        private bool HasSeen(string cutsceneId)
        {
            var seen = _save?.Data?.seenCutscenes;
            return seen != null && seen.Contains(cutsceneId);
        }

        private void MarkSeen(string cutsceneId)
        {
            var data = _save?.Data;

            if (data == null)
            {
                return;
            }

            data.EnsureCollections();

            if (!data.seenCutscenes.Contains(cutsceneId))
            {
                data.seenCutscenes.Add(cutsceneId);
                _save.Save();
            }
        }

        // ---- Helpers ----------------------------------------------------------------

        private PosterData CurrentPoster()
        {
            return _restoration?.Poster != null ? _restoration.Poster : _activePoster;
        }

        private void Route(GameScreen screen, bool immediate)
        {
            if (router == null)
            {
                return;
            }

            if (immediate)
            {
                router.GoNow(screen);
            }
            else
            {
                router.Go(screen);
            }

            FlowScreenRequested?.Invoke(screen);
        }

        // ---- Wiring -----------------------------------------------------------------

        private void ResolveRestoration()
        {
            if (restorationSource == null)
            {
                _restoration = GetComponent<IRestorationRuntime>();

                if (_restoration == null)
                {
                    Debug.LogError("[GameFlowController] No Restoration Source assigned. Drag the " +
                                   "object holding RestorationController into the Restoration " +
                                   "Source field.", this);
                }

                return;
            }

            // Dropping a GameObject on a MonoBehaviour field makes Unity keep that
            // object's FIRST component, which is rarely the intended one. Look along
            // the whole object before calling it a mis-wiring.
            _restoration = restorationSource as IRestorationRuntime
                           ?? restorationSource.GetComponent<IRestorationRuntime>();

            if (_restoration == null)
            {
                Debug.LogError($"[GameFlowController] The object in Restoration Source " +
                               $"('{restorationSource.name}') has no component implementing " +
                               "IRestorationRuntime. Add RestorationController to it.",
                               this);
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _restoration == null)
            {
                return;
            }

            _restoration.StageStarted += OnStageStarted;
            _restoration.StageCompleted += OnStageCompleted;
            _restoration.PosterCompleted += OnPosterCompleted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _restoration == null)
            {
                return;
            }

            _restoration.StageStarted -= OnStageStarted;
            _restoration.StageCompleted -= OnStageCompleted;
            _restoration.PosterCompleted -= OnPosterCompleted;
            _subscribed = false;
        }
    }
}
