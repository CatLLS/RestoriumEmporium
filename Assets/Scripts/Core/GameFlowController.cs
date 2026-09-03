// ============================================================
// GameFlowController — the Game scene's orchestrator.
// WHAT & WHY: Something has to decide which screen the player lands on, when a
//   restoration starts, and what gets written to the save at each milestone.
//   Splitting that across the screens themselves is how a flow ends up with
//   several owners and a soft-lock nobody can reproduce. This is the one owner:
//   UI calls into it, it calls the router and the save service.
// KEY DECISIONS:
//   - Strictly one-directional: this never touches a UI component, and no UI
//     component sets state on the restoration runtime. Buttons call
//     StartRestoration()/GoToThanksForPlaying(); everything else is reactions to
//     IRestorationRuntime events. That is the whole rule, and it is what keeps
//     the flow readable.
//   - Screen routing is driven by StageStarted, not by StageCompleted. The
//     stage asset already says which screen it plays on, so reacting to "a stage
//     began" means resume, a fresh start and an ordinary advance all take the
//     same path. Routing on completion would need to look ahead to the next
//     stage and special-case the last one.
//   - The restoration runtime is a MonoBehaviour field cast to
//     IRestorationRuntime at runtime. Unity cannot serialise an interface, and
//     Core must not reference RestorationController's concrete type. The cast is
//     done once in Awake with a loud error, so the failure is a console message
//     on launch rather than a null reference three screens in.
//   - StageCompleted persists the NEXT stage index, not the one just finished.
//     If the process dies in the window between "stage done" and "next stage
//     started", the player resumes at the work they have not done rather than
//     replaying a stage they already finished. It is clamped, so the last stage
//     cannot write an out-of-range index.
//   - StageStarted uses SaveSoon() and StageCompleted uses Save(). A stage
//     beginning is recoverable (the completion of the previous stage was already
//     written); a stage completing is the milestone worth a synchronous write.
//   - Resume is refused when the saved posterId does not match the assigned
//     poster. The MVP has one poster, but a stage index from a different poster
//     would index into the wrong array and either crash or start mid-sequence.
//   - GoToThanksForPlaying saves before leaving. A scene load is the one moment
//     where a pending SaveSoon could be lost to a kill during the load.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open the Game scene. Select the "GameFlow" object you created for
//     ScreenRouter (or create it: GameObject -> Create Empty, rename to
//     exactly "GameFlow").
// [ ] With "GameFlow" selected, click "Add Component", type
//     "GameFlowController", press Enter.
// [ ] Wire its three fields:
//       Router             <- drag the "GameFlow" object from the Hierarchy
//                             (it holds the ScreenRouter component).
//       Restoration Source <- drag the object holding the RestorationController
//                             component (also "GameFlow", if that is where it
//                             was added).
//       Poster             <- drag the Poster01 asset from
//                             Assets/Data/Poster1/ in the Project window.
// [ ] Hook up the two buttons. Select the journal's "Restore" button in the
//     Hierarchy, find its "Button" component, and under "On Click ()" click "+".
//     Drag "GameFlow" into the empty object slot, then open the function
//     dropdown and choose GameFlowController -> StartRestoration ().
// [ ] Do the same for the FinishedRepair screen's "Continue" button, choosing
//     GameFlowController -> GoToThanksForPlaying ().
// [ ] Make sure the "ThanksForPlaying" scene is in File -> Build Profiles ->
//     Scenes In Build, or the Continue button will log an error and do nothing.
// [ ] To test the resume path: play, start the restoration, finish one stage,
//     stop play mode, press play again. You should land back mid-restoration.
//     To test the fresh path, delete save.json (see SaveManager's checklist).
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Core
{
    using RestoriumEmporium.Data;
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

        [Header("Content")]
        [Tooltip("The poster this scene restores. The MVP ships exactly one.")]
        [SerializeField] private PosterData poster;

        private IRestorationRuntime _restoration;
        private ISaveService _save;
        private bool _subscribed;

        /// <summary>The poster this scene is configured to restore.</summary>
        public PosterData Poster => poster;

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

            if (poster == null)
            {
                Debug.LogError("[GameFlowController] No PosterData assigned. Drag Poster01 from " +
                               "Assets/Data/Poster1/ into the Poster field.", this);
            }

            ResolveRestoration();
        }

        private void Start()
        {
            // ServiceLocator is populated by GameBootstrap, which runs at
            // execution order -100; Start is late enough regardless.
            _save = ServiceLocator.Get<ISaveService>();

            if (_save == null)
            {
                Debug.LogWarning("[GameFlowController] No ISaveService registered. Progress will " +
                                 "not be saved. This is expected when the Game scene is opened " +
                                 "directly in the Editor instead of starting from Title.", this);
            }

            Subscribe();
            RouteInitialScreen();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // ---- Public entry points for UI ---------------------------------------------

        /// <summary>Starts poster #1 from stage zero. Wired to the journal's Restore button.</summary>
        public void StartRestoration()
        {
            if (poster == null)
            {
                Debug.LogError("[GameFlowController] StartRestoration called with no PosterData " +
                               "assigned.", this);
                return;
            }

            if (poster.StageCount == 0)
            {
                Debug.LogError($"[GameFlowController] '{poster.name}' has no stages. Drag the " +
                               "RestorationStage assets into its Stages list.", poster);
                return;
            }

            var data = _save?.Data;

            if (data != null)
            {
                data.currentPosterId = poster.posterId;
                data.currentStageIndex = 0;
                data.posterCompleted = false;
                data.hasPlayedBefore = true;
                _save.Save();
            }

            BeginPosterAt(0);
        }

        /// <summary>Leaves for the end card. Wired to the FinishedRepair Continue button.</summary>
        public void GoToThanksForPlaying()
        {
            // A scene load is the one place a deferred write could be lost.
            _save?.Save();
            SceneLoader.Load(SceneLoader.ThanksForPlayingScene);
        }

        // ---- Flow -------------------------------------------------------------------

        private void RouteInitialScreen()
        {
            var data = _save?.Data;

            if (data != null && data.IsRestorationInProgress && CanResume(data))
            {
                var index = Mathf.Clamp(data.currentStageIndex, 0, poster.StageCount - 1);
                BeginPosterAt(index);
                return;
            }

            router?.Go(GameScreen.Journal);
        }

        private bool CanResume(SaveData data)
        {
            if (poster == null || poster.StageCount == 0)
            {
                return false;
            }

            if (string.Equals(data.currentPosterId, poster.posterId, System.StringComparison.Ordinal))
            {
                return true;
            }

            // A stage index authored against a different poster would index into
            // the wrong sequence, so treat it as no progress at all.
            Debug.LogWarning($"[GameFlowController] Save holds progress for poster " +
                             $"'{data.currentPosterId}' but this scene serves " +
                             $"'{poster.posterId}'. Starting from the journal.", this);
            return false;
        }

        private void BeginPosterAt(int stageIndex)
        {
            if (_restoration != null)
            {
                // The runtime raises StageStarted, and OnStageStarted does the routing.
                _restoration.BeginPoster(poster, stageIndex);
                return;
            }

            // No restoration runtime (not yet wired, or the scene was opened on
            // its own): still put the player on the right screen so the rest of
            // the scene can be worked on.
            var stage = poster != null ? poster.GetStage(stageIndex) : null;
            router?.Go(stage != null ? stage.screen : GameScreen.Journal);
        }

        private void OnStageStarted(RestorationStageData stage, int index)
        {
            if (stage == null)
            {
                return;
            }

            var data = _save?.Data;

            if (data != null && poster != null)
            {
                data.currentPosterId = poster.posterId;
                data.currentStageIndex = index;
                data.lastScreen = stage.screen;
                _save.SaveSoon();
            }

            router?.Go(stage.screen);
        }

        private void OnStageCompleted(RestorationStageData stage, int index)
        {
            var data = _save?.Data;

            if (data == null || poster == null)
            {
                return;
            }

            // Persist where the player should resume, which is the stage AFTER
            // the one they just finished. Clamped so the final stage stays in range;
            // PosterCompleted is what actually ends the sequence.
            var resumeIndex = Mathf.Clamp(index + 1, 0, Mathf.Max(0, poster.StageCount - 1));

            data.currentPosterId = poster.posterId;
            data.currentStageIndex = resumeIndex;
            _save.Save();
        }

        private void OnPosterCompleted()
        {
            var data = _save?.Data;

            if (data != null)
            {
                if (poster != null)
                {
                    data.currentPosterId = poster.posterId;
                }

                data.posterCompleted = true;
                data.lastScreen = GameScreen.FinishedRepair;
                _save.Save();
            }

            router?.Go(GameScreen.FinishedRepair);
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

            _restoration = restorationSource as IRestorationRuntime;

            if (_restoration == null)
            {
                Debug.LogError($"[GameFlowController] '{restorationSource.GetType().Name}' was " +
                               "assigned to Restoration Source but does not implement " +
                               "IRestorationRuntime. Assign the RestorationController component.",
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
