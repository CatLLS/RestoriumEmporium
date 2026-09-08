// ============================================================
// TutorialController — plays the ordered tutorial steps at the player's pace.
// WHAT & WHY: The MVP's first playthrough is guided end to end: Tracy talks, a hand
//   points, input is gated to one target, and the step ends on the exact action it
//   asked for. This component walks a TutorialStepData[] and drives the three view
//   pieces (TracyOverlayView, HandPointer, TutorialInputGate) to do that, persisting
//   its position after every single step so an app kill mid-tutorial resumes exactly
//   where it left off.
// KEY DECISIONS:
//   - Written as ONE coroutine over the step list rather than an Update state
//     machine. A tutorial is a script, and a coroutine reads top to bottom in the
//     same order the player experiences it; an Update switch would scatter the same
//     sequence across a dozen enum cases and invite the double-advance bugs that
//     make tutorials lock up.
//   - A step waits for the player, with NO clock on it by default. An earlier
//     revision timed every step out; in play that meant the tutorial marched on
//     while the player was still reading, so Tracy ended up narrating a screen
//     they had not reached. Skip() and Restart() are the escape hatches instead,
//     and useSafetyTimeout puts the old behaviour back for a scripted test run.
//   - There is no delay BETWEEN steps. A step that has been satisfied should end
//     on the spot; padding the gap only made the tutorial feel sticky. The one
//     clock left on the normal path is dialogueDelaySeconds, which sits directly
//     in front of the panel appearing, where the abruptness actually was.
//   - The interfaces the tutorial listens to (IRestorationRuntime, IScreenRouter)
//     come from serialized MonoBehaviour fields cast at Awake, not from
//     ServiceLocator. They are scene objects, so a visible Inspector reference is
//     both debuggable and honest about the dependency; the locator is reserved for
//     the three process-lifetime services. A failed cast logs an explicit error
//     naming the field and the interface instead of NullReferencing later.
//   - The three services are resolved in Start, not Awake: the Systems object
//     registers itself in ITS Awake, and Awake order between two objects is not
//     defined. Start is guaranteed to be after every Awake in the scene.
//   - Advance conditions are re-checked ONCE when a step opens ("am I already on
//     that screen / already holding that tool?"). Resuming from a save frequently
//     lands mid-condition, and without that check the step would wait for an event
//     that already happened.
//   - Tap-on-target detection uses a TutorialTapProbe added to the anchored object
//     for the duration of the step. The tutorial therefore needs no knowledge of,
//     and no edit to, the buttons it points at.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] First run the menu item Restorium -> Create Poster 1 Data. It creates every
//     data asset this component needs, including the tutorial steps.
// [x] In the Hierarchy select the "GameFlow" GameObject (create it with
//     right-click -> Create Empty and rename it if it does not exist).
// [x] Click "Add Component" and add this script (TutorialController).
// [x] In the Project window open Assets/Data/Tutorial/. Select ALL of the
//     TutorialStep assets (click 01_Welcome, then shift-click the last one).
// [x] In the Inspector set "Steps" Size to the number of assets, then drag them in
//     ONE BY ONE in numbered order: 01 into Element 0, 02 into Element 1, and so on.
//     ORDER MATTERS - the save file stores a position in this list.
// [x] Drag the TracyHelpOverlay GameObject into "Overlay".
// [x] Drag the HelpingHand GameObject into "Hand Pointer".
// [x] Drag this same GameFlow GameObject into "Input Gate"
//     (TutorialInputGate lives on it too).
// [x] Drag the GameObject holding RestorationController into "Restoration Source".
// [x] Drag the GameObject holding ScreenRouter into "Screen Router Source".
//     Unity will pick the right component off each object.
// [x] Leave "Run Automatically" ticked so the tutorial starts with the game.
// [x] Leave "Use Safety Timeout" UNTICKED. Steps then wait for the player for as
//     long as it takes, which is the point. Tick it only to march through the
//     script while testing; "Fallback Timeout" is what it uses for steps whose
//     Auto Advance Seconds is 0.
// [ ] "Dialogue Delay Seconds" (1s) is the only clock in a step's normal path.
//     There is deliberately NO gap between steps: a step ends the instant the
//     player does what it asked, and the next one's beat is the one before its
//     line appears.
// [x] To watch the tutorial again while testing, right-click this component's
//     header in the Inspector and choose "Restart Tutorial".
// ---------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;

namespace RestoriumEmporium.Tutorial
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Localization;
    using RestoriumEmporium.Restoration;

    [DisallowMultipleComponent]
    public class TutorialController : MonoBehaviour
    {
        [Header("Steps (in play order)")]
        [Tooltip("Assets from Assets/Data/Tutorial/, numbered 01, 02, 03...")]
        [SerializeField] private TutorialStepData[] steps = new TutorialStepData[0];

        [Header("Scene pieces")]
        [SerializeField] private TracyOverlayView overlay;
        [SerializeField] private HandPointer handPointer;
        [SerializeField] private TutorialInputGate inputGate;

        [Header("Runtime sources (drag the GameObject holding the component)")]
        [Tooltip("Must have a component implementing IRestorationRuntime (RestorationController).")]
        [SerializeField] private MonoBehaviour restorationSource;

        [Tooltip("Must have a component implementing IScreenRouter (ScreenRouter).")]
        [SerializeField] private MonoBehaviour screenRouterSource;

        [Header("Behaviour")]
        [Tooltip("Start the tutorial automatically when the scene loads.")]
        [SerializeField] private bool runAutomatically = true;

        [Tooltip("Seconds of stillness before Tracy's panel appears. Nothing else " +
                 "is delayed: the step is already live, the hand and the input gate " +
                 "are unaffected. This is only so a line does not slam onto the " +
                 "screen the instant the thing it is reacting to happened.")]
        [Range(0f, 3f)]
        [SerializeField] private float dialogueDelaySeconds = 1f;

        [Tooltip("Let steps give up and advance on their own. OFF by design: a step " +
                 "that advances while the player is still reading teaches the wrong " +
                 "thing and strands them a screen behind. Skip Tutorial is the escape " +
                 "hatch instead. Tick it only to burn through the script while testing.")]
        [SerializeField] private bool useSafetyTimeout;

        [Tooltip("Only used when Use Safety Timeout is ticked, and only for steps " +
                 "whose Auto Advance Seconds is 0.")]
        [Range(5f, 300f)]
        [SerializeField] private float fallbackTimeout = 120f;

        /// <summary>Raised as each step opens. Argument is the step and its index.</summary>
        public event Action<TutorialStepData, int> StepStarted;

        /// <summary>Raised once the last step ends, or when Skip() is called.</summary>
        public event Action Finished;

        private ILocalizationService _localization;
        private ISaveService _save;
        private IAudioService _audio;
        private IRestorationRuntime _restoration;
        private IScreenRouter _router;

        private Coroutine _loop;
        private TutorialStepData _activeStep;
        private TutorialTapProbe _probe;
        private bool _probeIsOurs;
        private bool _advanced;
        private bool _overlayDismissed;

        /// <summary>Index of the step currently running. -1 when the tutorial is not running.</summary>
        public int CurrentIndex { get; private set; } = -1;

        /// <summary>True while the step coroutine is alive.</summary>
        public bool IsRunning => _loop != null;

        public int StepCount => steps != null ? steps.Length : 0;

        private void Awake()
        {
            // Scene-object dependencies: cast now and complain loudly, once, with the
            // exact Inspector field name, rather than NullReferencing three screens in.
            _restoration = ResolveSource<IRestorationRuntime>(restorationSource, nameof(restorationSource));
            _router = ResolveSource<IScreenRouter>(screenRouterSource, nameof(screenRouterSource));
        }

        private void Start()
        {
            // Process-lifetime services: resolved here because the Systems object
            // registers them in its own Awake and Awake order is undefined.
            _localization = ServiceLocator.Get<ILocalizationService>();
            _save = ServiceLocator.Get<ISaveService>();
            _audio = ServiceLocator.Get<IAudioService>();

            if (_localization == null)
            {
                Debug.LogWarning(
                    "[TutorialController] No ILocalizationService registered. Tracy will show " +
                    "raw localisation keys. Add the Systems object to the scene.", this);
            }

            if (!runAutomatically)
            {
                return;
            }

            if (_save != null && _save.Data != null && _save.Data.tutorialCompleted)
            {
                HideEverything();
                return;
            }

            var resume = 0;

            if (_save != null && _save.Data != null)
            {
                resume = Mathf.Clamp(_save.Data.tutorialStepIndex, 0, StepCount);
            }

            Run(resume);
        }

        private void OnDisable()
        {
            DetachProbe();
            UnsubscribeAll();
        }

        /// <summary>Starts (or restarts) the tutorial from <paramref name="startIndex"/>.</summary>
        public void Run(int startIndex)
        {
            if (StepCount == 0)
            {
                Debug.LogWarning("[TutorialController] Steps is empty; nothing to run.", this);
                return;
            }

            StopLoop();

            if (startIndex >= StepCount)
            {
                Finish();
                return;
            }

            _loop = StartCoroutine(RunFrom(Mathf.Max(0, startIndex)));
        }

        /// <summary>
        /// Abandons the tutorial immediately and marks it complete. Safe at any moment;
        /// this is the escape hatch a stuck playtester (or the human) reaches for.
        /// </summary>
        [ContextMenu("Skip Tutorial")]
        public void Skip()
        {
            StopLoop();
            Finish();
        }

        /// <summary>Clears the saved position and plays the tutorial again from the top.</summary>
        [ContextMenu("Restart Tutorial")]
        public void Restart()
        {
            StopLoop();

            if (_save != null && _save.Data != null)
            {
                _save.Data.tutorialCompleted = false;
                _save.Data.tutorialStepIndex = 0;
                _save.Save();
            }

            Run(0);
        }

        private IEnumerator RunFrom(int startIndex)
        {
            HideEverything();

            // One frame so every screen's TutorialAnchor has run its OnEnable and
            // registered itself before the first lookup.
            yield return null;

            for (var i = startIndex; i < StepCount; i++)
            {
                var step = steps[i];

                if (step == null)
                {
                    Debug.LogWarning($"[TutorialController] Steps element {i} is empty; skipping it.", this);
                    continue;
                }

                CurrentIndex = i;
                StepStarted?.Invoke(step, i);

                yield return RunStep(step);

                // Persist AFTER the step, pointing at the next one, so a kill here
                // resumes on the step the player has not done yet.
                Persist(i + 1);
            }

            _loop = null;
            Finish();
        }

        private IEnumerator RunStep(TutorialStepData step)
        {
            _activeStep = step;
            _advanced = false;

            Subscribe(step);

            // Some conditions are already true the moment the step opens, most often
            // right after resuming from a save. Honour them instead of waiting.
            if (IsConditionAlreadyMet(step))
            {
                _advanced = true;
            }

            // ---- Read phase: the player sets the pace --------------------------
            // Every line is dismissed by a tap, never by a timer, so nobody is
            // hurried through a sentence they are still reading. The panel blocks
            // the game underneath for exactly as long as it is up, which also keeps
            // the player from acting on an instruction they have not seen yet.
            if (overlay != null)
            {
                if (string.IsNullOrEmpty(step.lineKey))
                {
                    overlay.Hide();
                }
                else
                {
                    // A beat before the panel, so the line reads as Tracy noticing
                    // what just happened rather than as a pop-up interrupting it.
                    if (dialogueDelaySeconds > 0f)
                    {
                        yield return new WaitForSecondsRealtime(dialogueDelaySeconds);
                    }

                    _overlayDismissed = false;
                    overlay.Tapped += OnOverlayDismissed;
                    overlay.Show(step.lineKey, step.mood, true);

                    // No clock on a line. Reading speed is not something this
                    // component gets to have an opinion about.
                    var reading = 0f;

                    while (!_overlayDismissed
                           && (!useSafetyTimeout || reading < fallbackTimeout))
                    {
                        reading += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    overlay.Tapped -= OnOverlayDismissed;
                    overlay.Hide();
                }
            }

            // ---- Act phase: the screen belongs to the player -------------------
            if (inputGate != null)
            {
                if (step.gateInputToTarget)
                {
                    inputGate.GateTo(step.targetAnchorId);
                }
                else
                {
                    inputGate.ReleaseAll();
                }
            }

            // The hand has said its piece once the player starts working; leaving it
            // bobbing over a poster being scrubbed only covers the work.
            if (_restoration != null)
            {
                _restoration.StageProgressChanged += OnStageProgress;
            }

            var timeout = step.autoAdvanceSeconds > 0f ? step.autoAdvanceSeconds : fallbackTimeout;
            var elapsed = 0f;
            var pointerShown = false;

            while (!_advanced && (!useSafetyTimeout || elapsed < timeout))
            {
                elapsed += Time.unscaledDeltaTime;

                if (!pointerShown && elapsed >= step.pointerDelay)
                {
                    pointerShown = true;

                    if (handPointer != null && !string.IsNullOrEmpty(step.targetAnchorId))
                    {
                        handPointer.PointAt(step.targetAnchorId);
                    }
                }

                yield return null;
            }

            if (_restoration != null)
            {
                _restoration.StageProgressChanged -= OnStageProgress;
            }

            if (!_advanced && useSafetyTimeout)
            {
                Debug.Log(
                    $"[TutorialController] Step '{step.stepId}' timed out after {timeout:0.#}s " +
                    "and advanced on its safety valve.", this);
            }

            Unsubscribe(step);
            _activeStep = null;

            if (handPointer != null)
            {
                handPointer.Clear();
            }

            if (overlay != null)
            {
                overlay.Hide();
            }

            if (inputGate != null)
            {
                inputGate.ReleaseAll();
            }
        }

        private bool IsConditionAlreadyMet(TutorialStepData step)
        {
            switch (step.advance)
            {
                case TutorialAdvance.ScreenEntered:
                    return _router != null
                           && step.requiredScreen != GameScreen.None
                           && _router.Current == step.requiredScreen;

                case TutorialAdvance.ToolSelected:
                    return _restoration != null
                           && step.requiredTool != ToolId.None
                           && _restoration.ActiveTool == step.requiredTool;

                default:
                    return false;
            }
        }

        private void Subscribe(TutorialStepData step)
        {
            switch (step.advance)
            {
                case TutorialAdvance.TapAnywhere:
                    if (overlay != null)
                    {
                        overlay.Tapped += OnTapSignal;
                    }
                    break;

                case TutorialAdvance.TapTarget:
                    AttachProbe(step.targetAnchorId);
                    break;

                case TutorialAdvance.StageStarted:
                    if (_restoration != null)
                    {
                        _restoration.StageStarted += OnStageEvent;
                    }
                    break;

                case TutorialAdvance.StageCompleted:
                    if (_restoration != null)
                    {
                        _restoration.StageCompleted += OnStageEvent;
                    }
                    break;

                case TutorialAdvance.ScreenEntered:
                    if (_router != null)
                    {
                        _router.ScreenChanged += OnScreenChanged;
                    }
                    break;

                case TutorialAdvance.ToolSelected:
                    if (_restoration != null)
                    {
                        _restoration.ToolSelected += OnToolSelected;
                    }
                    break;
            }
        }

        private void Unsubscribe(TutorialStepData step)
        {
            if (step == null)
            {
                UnsubscribeAll();
                return;
            }

            switch (step.advance)
            {
                case TutorialAdvance.TapAnywhere:
                    if (overlay != null)
                    {
                        overlay.Tapped -= OnTapSignal;
                    }
                    break;

                case TutorialAdvance.TapTarget:
                    DetachProbe();
                    break;

                case TutorialAdvance.StageStarted:
                    if (_restoration != null)
                    {
                        _restoration.StageStarted -= OnStageEvent;
                    }
                    break;

                case TutorialAdvance.StageCompleted:
                    if (_restoration != null)
                    {
                        _restoration.StageCompleted -= OnStageEvent;
                    }
                    break;

                case TutorialAdvance.ScreenEntered:
                    if (_router != null)
                    {
                        _router.ScreenChanged -= OnScreenChanged;
                    }
                    break;

                case TutorialAdvance.ToolSelected:
                    if (_restoration != null)
                    {
                        _restoration.ToolSelected -= OnToolSelected;
                    }
                    break;
            }
        }

        /// <summary>Belt and braces: drop every handler regardless of which step was live.</summary>
        private void UnsubscribeAll()
        {
            if (overlay != null)
            {
                overlay.Tapped -= OnTapSignal;
                overlay.Tapped -= OnOverlayDismissed;
            }

            if (_restoration != null)
            {
                _restoration.StageStarted -= OnStageEvent;
                _restoration.StageCompleted -= OnStageEvent;
                _restoration.ToolSelected -= OnToolSelected;
                _restoration.StageProgressChanged -= OnStageProgress;
            }

            if (_router != null)
            {
                _router.ScreenChanged -= OnScreenChanged;
            }

            _activeStep = null;
        }

        private void AttachProbe(string anchorId)
        {
            DetachProbe();

            var anchor = TutorialAnchor.Find(anchorId);

            if (anchor == null)
            {
                // Fail open: without a probe this step rides its safety timeout out
                // instead of waiting for a tap that can never be detected.
                Debug.LogWarning(
                    $"[TutorialController] Cannot watch for a tap on '{anchorId}': no enabled " +
                    "TutorialAnchor has that id. The step will auto-advance instead.", this);
                return;
            }

            var host = anchor.gameObject;
            _probe = host.GetComponent<TutorialTapProbe>();

            if (_probe == null)
            {
                _probe = host.AddComponent<TutorialTapProbe>();
                _probe.hideFlags = HideFlags.DontSave;
                _probeIsOurs = true;
            }

            _probe.Clicked += OnTapSignal;
        }

        private void DetachProbe()
        {
            if (_probe == null)
            {
                _probeIsOurs = false;
                return;
            }

            _probe.Clicked -= OnTapSignal;

            if (_probeIsOurs)
            {
                Destroy(_probe);
            }

            _probe = null;
            _probeIsOurs = false;
        }

        private void OnTapSignal()
        {
            if (_audio != null)
            {
                _audio.PlaySfx(SfxId.ButtonClick);
            }

            _advanced = true;
        }

        private void OnStageEvent(RestorationStageData stage, int index)
        {
            _advanced = true;
        }

        /// <summary>Tap on Tracy's panel. Ends the reading pause, never the step.</summary>
        private void OnOverlayDismissed()
        {
            _overlayDismissed = true;
        }

        private void OnStageProgress(float progress)
        {
            // Any painting at all means the instruction landed. EnterCurrentStage
            // reports 0 on every stage open, so only real work clears the hand.
            if (progress > 0f)
            {
                handPointer?.Clear();
            }
        }

        private void OnScreenChanged(GameScreen screen)
        {
            if (_activeStep != null && screen == _activeStep.requiredScreen)
            {
                _advanced = true;
            }
        }

        private void OnToolSelected(ToolId tool)
        {
            if (_activeStep != null && tool == _activeStep.requiredTool)
            {
                _advanced = true;
            }
        }

        private void Persist(int nextIndex)
        {
            if (_save == null || _save.Data == null)
            {
                return;
            }

            _save.Data.tutorialStepIndex = Mathf.Clamp(nextIndex, 0, StepCount);
            _save.Save();
        }

        private void Finish()
        {
            UnsubscribeAll();
            DetachProbe();
            HideEverything();

            CurrentIndex = -1;

            if (_save != null && _save.Data != null)
            {
                _save.Data.tutorialStepIndex = StepCount;
                _save.Data.tutorialCompleted = true;
                _save.Save();
            }

            Finished?.Invoke();
        }

        private void StopLoop()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }

            UnsubscribeAll();
            DetachProbe();
        }

        private void HideEverything()
        {
            if (overlay != null)
            {
                overlay.Hide();
            }

            if (handPointer != null)
            {
                handPointer.Clear();
            }

            if (inputGate != null)
            {
                inputGate.ReleaseAll();
            }
        }

        private T ResolveSource<T>(MonoBehaviour source, string fieldName) where T : class
        {
            if (source == null)
            {
                Debug.LogWarning(
                    $"[TutorialController] '{fieldName}' is empty. Steps that wait on " +
                    $"{typeof(T).Name} will fall back to their safety timeout.", this);
                return null;
            }

            if (source is T typed)
            {
                return typed;
            }

            // The Inspector accepts any MonoBehaviour, so say exactly what went wrong
            // and exactly which slot to fix.
            var found = source.GetComponent<T>();

            if (found != null)
            {
                return found;
            }

            Debug.LogError(
                $"[TutorialController] The object in '{fieldName}' ('{source.name}', component " +
                $"{source.GetType().Name}) does not implement {typeof(T).Name}. Drag the GameObject " +
                $"that has the {typeof(T).Name} component into that slot instead.", this);
            return null;
        }
    }
}
