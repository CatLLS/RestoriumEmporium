// ============================================================
// RestorationController — the restoration state machine.
// WHAT & WHY: One place decides which stage is live, which tool is allowed, when
//   a stage is finished and what should happen next. Every other part of the
//   restoration (tool bar, poster surface, FX, tutorial, screen router) reads
//   that decision through IRestorationRuntime instead of keeping its own copy,
//   which is how a state machine avoids ending up with two owners.
// KEY DECISIONS:
//   - It raises a transition, it does not perform one. StageTransition says
//     "flip to back" or "go to finished repair"; actually flipping or routing
//     belongs to the presentation layer — RestorationPresenter — which
//     subscribes to TransitionRequested. Putting a CardFlipAnimator reference in
//     here would weld the rules to one particular scene layout.
//   - Because the presentation layer needs time to play that transition, the
//     controller does NOT advance immediately. It waits for
//     ContinueAfterTransition(). The single exception is when nothing is
//     listening: with no TransitionRequested subscribers the controller advances
//     on the spot, so a half-wired scene still plays through instead of
//     freezing on stage 3 with no clue why.
//   - Painting is gated by component, not by a flag inside the paint path. The
//     painter is disabled whenever there is no stage, no tool, or a transition
//     is pending; there is then no way for a stray drag to paint at the wrong
//     moment and no per-point permission check on the hot path.
//   - Coverage arrives as an event from IRevealSurface rather than being polled
//     in Update. This component has no Update at all.
//   - Per-stage bookkeeping lives in RestorationStageRunner so that this file
//     stays scene glue: components, events, audio, save.
//   - Progress is written to the save service with SaveSoon() on stage
//     boundaries only, never on coverage changes. Coverage changes many times a
//     second and disk writes do not belong on the drag path.
//   - A finished stage SETTLES before anything is told about it. The moment
//     coverage crosses the threshold the mask snaps full, and then the game holds
//     still for Stage Settle Seconds. Only after that does the completion sound
//     play, StageCompleted go out and the transition get raised — so the sound
//     arrives over the flip rather than on the frame the player's finger came up. Firing them on the same frame the player
//     lifted their finger meant Tracy's next line and the screen change landed on
//     top of the work being finished, which is the opposite of cozy. Holding here
//     rather than in each listener means one number covers dialogue, the tool bar
//     and the flip at once.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Create an empty object at the root of the Game scene: right-click in the
//     Hierarchy -> Create Empty, name it "GameFlow".
// [x] Select GameFlow -> Add Component -> Restoration Controller.
// [x] Wire its fields:
//       Poster        <- Assets/Data/Poster1/Poster01 (the PosterData asset)
//       Poster Stack  <- the "Poster" object under the Canvas
//       Painter       <- the same "Poster" object (its Reveal Mask Painter)
//       Tools         <- set Size to 6 and drag in, in any order, the six
//                        ToolData assets from Assets/Data/Tools/:
//                        ToolDustRemover, ToolWaterSpray, ToolDeacidifier,
//                        ToolSqueegee, ToolRoller, ToolPencil
// [x] Leave "Begin On Start" TICKED while you are testing a single poster on
//     its own. UNTICK it once the Journal screen exists, because the journal's
//     Restore button is what should call BeginPoster.
// [x] Add a Restoration Presenter to the same GameFlow object. It is what
//     listens to Transition Requested, plays the flips, and calls
//     ContinueAfterTransition(). WITHOUT IT THE RESTORATION FREEZES at the end
//     of stage 3, and "Auto Continue When Unhandled" will NOT save you: the tool
//     bar also subscribes to that event, so the handler is never null and the
//     fallback never runs. See RestorationPresenter.cs.
// [x] Leave "Auto Continue When Unhandled" TICKED. It only matters while you are
//     testing the poster on its own, with no tool bar and no presenter in the
//     scene at all.
// [ ] "Stage Settle Seconds" is the pause between a stage being finished and the
//     game reacting to it (1s by default). Set it to 0 to burn through the poster
//     while testing. Its companions are ScreenRouter's "Screen Change Delay
//     Seconds" and TutorialController's "Dialogue Delay Seconds".
// ---------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    [DisallowMultipleComponent]
    public class RestorationController : MonoBehaviour, IRestorationRuntime
    {
        [Header("Content")]
        [Tooltip("The poster to restore. The MVP ships a single one.")]
        [SerializeField] private PosterData poster;

        [Tooltip("Every tool that can appear, so brush sizes can be resolved by id.")]
        [SerializeField] private ToolData[] tools = new ToolData[0];

        [Header("Scene")]
        [Tooltip("The two-layer poster this controller drives.")]
        [SerializeField] private PosterLayerStack posterStack;

        [Tooltip("Input component; enabled only while a tool is held and a stage is live.")]
        [SerializeField] private RevealMaskPainter painter;

        [Header("Behaviour")]
        [Tooltip("Start the poster automatically in Start(). Untick once the journal drives it.")]
        [SerializeField] private bool beginOnStart = true;

        [Tooltip("Advance immediately when nothing is listening to Transition Requested.")]
        [SerializeField] private bool autoContinueWhenUnhandled = true;

        [Header("Pacing")]
        [Tooltip("Seconds of stillness after a stage's coverage threshold is crossed " +
                 "before anything else happens on screen. The mask snap and the " +
                 "completion sound land straight away; Tracy, the tool bar and the " +
                 "flip all wait this out. Set to 0 for the old immediate behaviour.")]
        [Range(0f, 3f)]
        [SerializeField] private float stageSettleSeconds = 1f;

        private readonly RestorationStageRunner _runner = new RestorationStageRunner();
        private readonly Dictionary<ToolId, ToolData> _toolsById = new Dictionary<ToolId, ToolData>();

        private IRevealSurface _surface;
        private IAudioService _audio;
        private ISaveService _save;

        private ToolId _activeTool = ToolId.None;
        private bool _awaitingTransition;
        private bool _posterFinished;
        private Coroutine _settle;

        /// <inheritdoc />
        public PosterData Poster => _runner.Poster;

        /// <inheritdoc />
        public int CurrentStageIndex => _runner.Index;

        /// <inheritdoc />
        public RestorationStageData CurrentStage => _runner.Stage;

        /// <inheritdoc />
        public ToolId ActiveTool => _activeTool;

        /// <inheritdoc />
        public float StageProgress01 => _runner.Progress01;

        /// <inheritdoc />
        public event Action<RestorationStageData, int> StageStarted;

        /// <inheritdoc />
        public event Action<RestorationStageData, int> StageCompleted;

        /// <inheritdoc />
        public event Action<float> StageProgressChanged;

        /// <inheritdoc />
        public event Action<ToolId> ToolSelected;

        /// <inheritdoc />
        public event Action PosterCompleted;

        /// <summary>
        /// Raised when a completed stage asks for choreography. The listener plays
        /// it and then calls ContinueAfterTransition(). Never raised for a stage
        /// whose onComplete is None.
        /// </summary>
        public event Action<StageTransition, RestorationStageData> TransitionRequested;

        /// <summary>True while the controller is waiting for ContinueAfterTransition().</summary>
        public bool IsAwaitingTransition => _awaitingTransition;

        /// <summary>True during the beat between a stage finishing and anyone being told.</summary>
        public bool IsSettling => _settle != null;

        /// <summary>Looks up an authored tool by id, or null when it is not in the list.</summary>
        public ToolData GetTool(ToolId id)
        {
            return _toolsById.TryGetValue(id, out ToolData tool) ? tool : null;
        }

        private void Awake()
        {
            _surface = posterStack;

            for (int i = 0; i < tools.Length; i++)
            {
                ToolData tool = tools[i];
                if (tool != null && tool.id != ToolId.None)
                {
                    _toolsById[tool.id] = tool;
                }
            }

            SetPaintingEnabled(false);
        }

        private void OnEnable()
        {
            if (_surface != null)
            {
                _surface.CoverageChanged += OnCoverageChanged;
            }

            if (painter != null)
            {
                painter.StrokeStarted += OnStrokeStarted;
                painter.StrokeEnded += OnStrokeEnded;
            }
        }

        private void OnDisable()
        {
            if (_surface != null)
            {
                _surface.CoverageChanged -= OnCoverageChanged;
            }

            if (painter != null)
            {
                painter.StrokeStarted -= OnStrokeStarted;
                painter.StrokeEnded -= OnStrokeEnded;
            }

            CancelSettle();
            _audio?.StopToolLoop();
        }

        private void Start()
        {
            _audio = ServiceLocator.Get<IAudioService>();
            _save = ServiceLocator.Get<ISaveService>();

            if (beginOnStart && poster != null)
            {
                int startIndex = ResolveResumeIndex();
                BeginPoster(poster, startIndex);
            }
        }

        /// <inheritdoc />
        public void BeginPoster(PosterData posterData, int startStageIndex)
        {
            if (posterData == null || posterData.StageCount == 0)
            {
                Debug.LogWarning("[RestorationController] BeginPoster called with no stages.", this);
                return;
            }

            poster = posterData;
            _posterFinished = false;
            _awaitingTransition = false;

            // A restart during the beat must not have the old stage's completion
            // arrive a second later, on top of the new stage.
            CancelSettle();

            _runner.Begin(posterData, startStageIndex);
            EnterCurrentStage();
        }

        /// <inheritdoc />
        public bool TrySelectTool(ToolId tool)
        {
            RestorationStageData stage = _runner.Stage;

            if (stage == null || _awaitingTransition || _runner.IsStageComplete)
            {
                return false;
            }

            if (tool != stage.requiredTool || tool == ToolId.None)
            {
                // A normal thing a player does, not an error. The tool bar turns
                // this false into a soft nudge.
                return false;
            }

            if (_activeTool == tool)
            {
                return true;
            }

            _activeTool = tool;

            ToolData toolData = GetTool(tool);
            if (_surface != null)
            {
                _surface.BrushRadiusUv = stage.ResolveBrushRadius(toolData);
            }

            SetPaintingEnabled(true);

            _audio?.PlaySfx(SfxId.ToolSelect);
            ToolSelected?.Invoke(_activeTool);
            return true;
        }

        /// <summary>
        /// Called by the presentation layer once the transition it was handed has
        /// finished playing. Advances to the next stage, or finishes the poster.
        /// Ignored when no transition is pending.
        /// </summary>
        public void ContinueAfterTransition()
        {
            if (!_awaitingTransition)
            {
                return;
            }

            _awaitingTransition = false;
            AdvanceOrFinish();
        }

        /// <summary>Test and debug hook: forces the current stage to complete.</summary>
        public void ForceCompleteCurrentStage()
        {
            if (_runner.Stage == null || _runner.IsStageComplete || _awaitingTransition)
            {
                return;
            }

            // Drive the runner directly rather than through the coverage event:
            // that path is deliberately gated on a tool being held, and this hook
            // has to work with no tool selected.
            _runner.ReportCoverage(1f);
            StageProgressChanged?.Invoke(1f);
            CompleteCurrentStage();
        }

        private void EnterCurrentStage()
        {
            RestorationStageData stage = _runner.Stage;
            if (stage == null)
            {
                SetPaintingEnabled(false);
                return;
            }

            _activeTool = ToolId.None;
            SetPaintingEnabled(false);

            if (posterStack != null)
            {
                posterStack.SetStage(stage);
            }

            ToolData toolData = GetTool(stage.requiredTool);
            if (_surface != null)
            {
                _surface.BrushRadiusUv = stage.ResolveBrushRadius(toolData);
                _surface.ResetMask();
            }

            WriteProgressToSave(stage);

            StageStarted?.Invoke(stage, _runner.Index);
            ToolSelected?.Invoke(ToolId.None);
            StageProgressChanged?.Invoke(0f);
        }

        private void OnCoverageChanged(float coverage)
        {
            if (_activeTool == ToolId.None || _awaitingTransition)
            {
                return;
            }

            if (!_runner.IsRunning)
            {
                return;
            }

            bool justCompleted = _runner.ReportCoverage(coverage);
            StageProgressChanged?.Invoke(_runner.Progress01);

            if (justCompleted)
            {
                CompleteCurrentStage();
            }
        }

        private void CompleteCurrentStage()
        {
            RestorationStageData stage = _runner.Stage;
            if (stage == null)
            {
                return;
            }

            int index = _runner.Index;

            _activeTool = ToolId.None;
            SetPaintingEnabled(false);

            _audio?.StopToolLoop();

            // Snap the mask so the stage's end state is exact, not 85%-scrubbed.
            _surface?.FillCompletely();

            // Everything above is the payoff for the stroke that just landed and
            // belongs on this frame. Everything below moves the game on, and the
            // player gets a beat to look at what they did first.
            if (stageSettleSeconds <= 0f || !isActiveAndEnabled)
            {
                AnnounceStageCompleted(stage, index);
                return;
            }

            CancelSettle();
            _settle = StartCoroutine(SettleThenAnnounce(stage, index));
        }

        private IEnumerator SettleThenAnnounce(RestorationStageData stage, int index)
        {
            // Unscaled: the beat is the same length whether or not the pause
            // overlay is up over it.
            yield return new WaitForSecondsRealtime(stageSettleSeconds);

            _settle = null;
            AnnounceStageCompleted(stage, index);
        }

        private void CancelSettle()
        {
            if (_settle != null)
            {
                StopCoroutine(_settle);
                _settle = null;
            }
        }

        private void AnnounceStageCompleted(RestorationStageData stage, int index)
        {
            // The completion sound rides the transition, not the last brush
            // stroke. On the stroke it sounded like the game buzzing the player
            // for finishing; over the flip it is the stage being put away.
            if (stage.completeSfx != SfxId.None)
            {
                _audio?.PlaySfx(stage.completeSfx);
            }

            StageCompleted?.Invoke(stage, index);
            ToolSelected?.Invoke(ToolId.None);

            if (stage.onComplete == StageTransition.None)
            {
                AdvanceOrFinish();
                return;
            }

            Action<StageTransition, RestorationStageData> handler = TransitionRequested;
            if (handler == null)
            {
                if (autoContinueWhenUnhandled)
                {
                    AdvanceOrFinish();
                }
                else
                {
                    _awaitingTransition = true;
                }

                return;
            }

            _awaitingTransition = true;
            handler.Invoke(stage.onComplete, stage);
        }

        private void AdvanceOrFinish()
        {
            if (_runner.Advance())
            {
                EnterCurrentStage();
                return;
            }

            FinishPoster();
        }

        private void FinishPoster()
        {
            if (_posterFinished)
            {
                return;
            }

            _posterFinished = true;
            SetPaintingEnabled(false);

            if (_save != null)
            {
                _save.Data.posterCompleted = true;
                _save.Data.currentStageIndex = _runner.Poster != null ? _runner.Poster.StageCount - 1 : -1;
                _save.Data.lastScreen = GameScreen.FinishedRepair;
                _save.Save();
            }

            _audio?.PlaySfx(SfxId.RestorationComplete);
            PosterCompleted?.Invoke();
        }

        private void OnStrokeStarted(Vector2 screenPosition)
        {
            RestorationStageData stage = _runner.Stage;
            if (stage == null)
            {
                return;
            }

            ToolData toolData = GetTool(stage.requiredTool);
            SfxId loop = toolData != null ? toolData.loopSfx : SfxId.None;

            if (loop != SfxId.None)
            {
                _audio?.StartToolLoop(loop);
            }
        }

        private void OnStrokeEnded()
        {
            _audio?.StopToolLoop();
        }

        private void SetPaintingEnabled(bool value)
        {
            if (painter != null)
            {
                painter.enabled = value;
            }
        }

        private void WriteProgressToSave(RestorationStageData stage)
        {
            if (_save == null || _runner.Poster == null)
            {
                return;
            }

            SaveData data = _save.Data;
            data.currentPosterId = _runner.Poster.posterId;
            data.currentStageIndex = _runner.Index;
            data.posterCompleted = false;
            data.lastScreen = stage.screen;
            _save.SaveSoon();
        }

        private int ResolveResumeIndex()
        {
            if (_save == null || poster == null)
            {
                return 0;
            }

            SaveData data = _save.Data;
            if (!data.IsRestorationInProgress || data.currentPosterId != poster.posterId)
            {
                return 0;
            }

            return Mathf.Clamp(data.currentStageIndex, 0, poster.StageCount - 1);
        }
    }
}
