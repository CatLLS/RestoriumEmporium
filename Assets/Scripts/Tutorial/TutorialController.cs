// ============================================================
// TutorialController — plays the ordered tutorial steps and can never soft-lock.
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
//   - EVERY step is waited on with a timeout, not just the ones the designer
//     remembered. autoAdvanceSeconds is used when set and a Fallback Timeout is used
//     when it is zero, so no combination of a bad asset, a missing anchor and a
//     misheard instruction can strand a playtester. Skip() and Restart() are public
//     for the same reason.
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
// [ ] First run the menu item Restorium -> Create Poster 1 Data. It creates every
//     data asset this component needs, including the tutorial steps.
// [ ] In the Hierarchy select the "GameFlow" GameObject (create it with
//     right-click -> Create Empty and rename it if it does not exist).
// [ ] Click "Add Component" and add this script (TutorialController).
// [ ] In the Project window open Assets/Data/Tutorial/. Select ALL of the
//     TutorialStep assets (click 01_Welcome, then shift-click the last one).
// [ ] In the Inspector set "Steps" Size to the number of assets, then drag them in
//     ONE BY ONE in numbered order: 01 into Element 0, 02 into Element 1, and so on.
//     ORDER MATTERS - the save file stores a position in this list.
// [ ] Drag the TracyHelpOverlay GameObject into "Overlay".
// [ ] Drag the HelpingHand GameObject into "Hand Pointer".
// [ ] Drag this same GameFlow GameObject into "Input Gate"
//     (TutorialInputGate lives on it too).
// [ ] Drag the GameObject holding RestorationController into "Restoration Source".
// [ ] Drag the GameObject holding ScreenRouter into "Screen Router Source".
//     Unity will pick the right component off each object.
// [ ] Leave "Run Automatically" ticked so the tutorial starts with the game.
// [ ] "Fallback Timeout" (default 120s) only applies to steps whose
//     Auto Advance Seconds is 0. Leave it alone unless you know why.
// [ ] To watch the tutorial again while testing, right-click this component's
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

        [Tooltip("Seconds of breathing room between steps.")]
        [Range(0f, 2f)]
        [SerializeField] private float betweenStepsDelay = 0.2f;

        [Tooltip("Safety timeout used for any step whose Auto Advance Seconds is 0. " +
                 "There is deliberately no way to wait forever.")]
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
        private WaitForSecondsRealtime _betweenSteps;

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

            _betweenSteps = new WaitForSecondsRealtime(Mathf.Max(0.01f, betweenStepsDelay));
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

                if (betweenStepsDelay > 0f)
                {
                    yield return _betweenSteps;
                }
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

            // A dialogue beat blocks the screen so a tap anywhere advances it. Every
            // other beat needs the player to reach the game underneath the overlay.
            var blocking = step.advance == TutorialAdvance.TapAnywhere;

            if (overlay != null)
            {
                if (string.IsNullOrEmpty(step.lineKey))
                {
                    overlay.Hide();
                }
                else
                {
                    overlay.Show(step.lineKey, step.mood, blocking);
                }
            }

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

            var timeout = step.autoAdvanceSeconds > 0f ? step.autoAdvanceSeconds : fallbackTimeout;
            var elapsed = 0f;
            var pointerShown = false;

            while (!_advanced && elapsed < timeout)
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

            if (!_advanced)
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
            }

            if (_restoration != null)
            {
                _restoration.StageStarted -= OnStageEvent;
                _restoration.StageCompleted -= OnStageEvent;
                _restoration.ToolSelected -= OnToolSelected;
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
