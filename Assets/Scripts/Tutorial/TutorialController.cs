// ============================================================
// TutorialController — starts and plays the guided sequences (contract §6).
// WHAT & WHY: Batch 2 teaches things in several places at different times
//   (first restoration, the desk hub's lamp, the journal's second page, poster
//   2's stickers) instead of one linear script. Each is a TutorialSequenceData:
//   a trigger, prerequisites and an ordered TutorialStepData list. This
//   component decides WHEN a sequence starts (screen entered / stage kind
//   started / TryStart from code), plays it one step at a time with the same
//   read-then-act choreography the MVP used, and persists position after every
//   single step so an app kill mid-sequence resumes exactly where it left off.
// KEY DECISIONS:
//   - ONE sequence runs at a time. Triggers are only evaluated while nothing is
//     running (_loop == null); "first match wins" happens inside
//     TutorialTriggerRules, this component just calls it on ScreenChanged and
//     on StageStarted. This is also why two sequences can never talk over each
//     other.
//   - A relaunch is handled BEFORE any trigger fires: Start() reads
//     SaveData.activeTutorialSequence/activeTutorialStepIndex directly and
//     resumes it. Triggers alone would not cover a relaunch that lands back on
//     the SAME screen the player already had a sequence running on (no new
//     ScreenChanged/StageStarted event would ever fire).
//   - Per-step advance conditions (ScreenEntered, StageStarted, StageCompleted,
//     ToolSelected, ItemPurchased, PreviewOpened, StickerPeeled) are each a
//     small ALWAYS-ON handler that only acts when it matches the currently
//     open step (_activeStep). This is simpler than the MVP's per-step
//     Subscribe/Unsubscribe switch AND lets the very same handlers double as
//     the sequence-trigger checks (ScreenChanged/StageStarted) with no risk of
//     a handler being left subscribed after a step ends.
//   - TapAnywhere / TapTarget are the two advance kinds that genuinely need
//     per-step setup (which TracyOverlayView to listen to, which anchor to
//     probe), so those two alone keep an explicit subscribe/unsubscribe around
//     each RunStep call.
//   - StickerPeeled only advances the step when remaining <= 0. The hand still
//     points at "sticker.next" the whole time (TutorialAnchor for that id moves
//     itself to the next un-peeled sticker — see TutorialAnchor/HandPointer),
//     so one step naturally covers "peel them all", not one step per sticker.
//   - Waits while ICutscenePlayer.IsPlaying or OverlayController.AnyOpen
//     (IsSuspended): both while READING a line (the timer pauses so a safety
//     timeout can never fire while the player cannot even see the screen) and
//     while ACTING (the hand pointer hides itself via HandPointer.SetSuspended,
//     and the elapsed-time / pointer-delay clocks pause too). Raycasts are
//     already handled without this component's help: both a playing cutscene
//     and an open overlay sit on a Canvas sorted ABOVE Tracy's panel and have a
//     full-screen raycast-blocking background, so nothing underneath can be
//     tapped regardless.
//   - The two TracyOverlayView instances (Portrait / HubFullBody) are picked
//     per step from TutorialStepData.presentation, never hard-coded per
//     sequence, so a sequence can mix presentations if a future one needs to.
//   - Every advance condition remains a single exit, exactly as the MVP: two
//     listeners setting the same _advanced flag cannot double-advance, they can
//     only both agree.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] First run Restorium -> Tutorial -> Rebuild Tutorial Sequences (creates /
//     updates the sequence assets under Assets/Data/Tutorial/Sequences/, reusing
//     the 16 existing first_restoration step assets).
// [ ] Select the "GameFlow" GameObject. Add Component -> Tutorial Controller
//     (this script) if it is not already there.
// [ ] In the Project window open Assets/Data/Tutorial/Sequences/. Set
//     "Sequences" Size to 4 and drag in, in this order: first_restoration,
//     deskhub_lamp, journal_page2, stickers. (Order only matters as the
//     priority TutorialTriggerRules uses when two triggers could fire on the
//     very same event; the table above lists the intended order.)
// [ ] Drag the TracyHelpOverlay GameObject into "Portrait Overlay".
// [ ] Drag the HubTracyOverlay GameObject (see TracyOverlayView.cs's second
//     checklist) into "Hub Overlay".
// [ ] Drag the HelpingHand GameObject into "Hand Pointer".
// [ ] Drag this same GameFlow GameObject into "Input Gate" (TutorialInputGate
//     lives on it too).
// [ ] Drag the "Overlays" object (its OverlayController) into "Overlays".
// [ ] Drag the GameObject holding RestorationController into "Restoration
//     Source". Drag the GameObject holding ScreenRouter into "Screen Router
//     Source". Unity will pick the right component off each object.
// [ ] Leave "Run Automatically" ticked.
// [ ] Leave "Use Safety Timeout" UNTICKED (steps wait for the player). Tick it
//     only to burn through the script while testing.
// [ ] To watch a sequence again while testing, right-click this component's
//     header and choose "Restart Tutorial" (restarts whichever sequence is
//     currently active, or the first one in the list when none is running).
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace RestoriumEmporium.Tutorial
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Cinematics;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Economy;
    using RestoriumEmporium.Localization;
    using RestoriumEmporium.Restoration;
    using RestoriumEmporium.UI;

    [DisallowMultipleComponent]
    public class TutorialController : MonoBehaviour
    {
        [Header("Sequences (contract §6)")]
        [Tooltip("Every tutorial sequence this controller can play. Built by " +
                 "Restorium -> Tutorial -> Rebuild Tutorial Sequences.")]
        [SerializeField] private TutorialSequenceData[] sequences = new TutorialSequenceData[0];

        [Header("Views")]
        [Tooltip("Portrait bust + dialogue box (restoration screens, the journal).")]
        [SerializeField] private TracyOverlayView portraitOverlay;

        [Tooltip("Full-body Tracy in the desk hub (TracyPresentation.HubFullBody steps).")]
        [SerializeField] private TracyOverlayView hubOverlay;

        [SerializeField] private HandPointer handPointer;
        [SerializeField] private TutorialInputGate inputGate;

        [Tooltip("The Overlays object's OverlayController. A sequence waits while any " +
                 "modal (Pause / Settings) is open.")]
        [SerializeField] private OverlayController overlays;

        [Header("Runtime sources (drag the GameObject holding the component)")]
        [Tooltip("Must have a component implementing IRestorationRuntime (RestorationController).")]
        [SerializeField] private MonoBehaviour restorationSource;

        [Tooltip("Must have a component implementing IScreenRouter (ScreenRouter).")]
        [SerializeField] private MonoBehaviour screenRouterSource;

        [Header("Behaviour")]
        [SerializeField] private bool runAutomatically = true;

        [Tooltip("Seconds of stillness before Tracy's panel appears. Nothing else is " +
                 "delayed by this: the step is already live, only the line's arrival is " +
                 "softened so it reads as Tracy noticing what just happened.")]
        [Range(0f, 3f)]
        [SerializeField] private float dialogueDelaySeconds = 1f;

        [Tooltip("Let steps give up and advance on their own. OFF by design (see the " +
                 "MVP TutorialController history): a step that advances while the " +
                 "player is still reading strands them a screen behind. Tick only to " +
                 "burn through the script while testing.")]
        [SerializeField] private bool useSafetyTimeout;

        [Tooltip("Only used when Use Safety Timeout is ticked, and only for steps whose " +
                 "Auto Advance Seconds is 0.")]
        [Range(5f, 300f)]
        [SerializeField] private float fallbackTimeout = 120f;

        /// <summary>Raised as each step opens.</summary>
        public event Action<TutorialSequenceData, TutorialStepData, int> StepStarted;

        /// <summary>Raised once a sequence finishes (naturally or via Skip()).</summary>
        public event Action<string> SequenceFinished;

        private ILocalizationService _localization;
        private ISaveService _save;
        private IAudioService _audio;
        private ICutscenePlayer _cutscenes;
        private IDecorationInventory _inventory;
        private IPosterProgress _progress;
        private IRestorationRuntime _restoration;
        private IScreenRouter _router;

        private TriggerCandidate[] _candidates;
        private TutorialSequenceData _activeSequence;
        private TutorialStepData _activeStep;
        private TracyOverlayView _activeOverlay;
        private Coroutine _loop;
        private TutorialTapProbe _probe;
        private bool _probeIsOurs;
        private bool _advanced;
        private bool _overlayDismissed;
        private bool _inventorySubscribed;

        /// <summary>The sequence currently playing, or null.</summary>
        public TutorialSequenceData ActiveSequence => _activeSequence;

        /// <summary>Index of the step currently running within ActiveSequence, or -1.</summary>
        public int CurrentStepIndex { get; private set; } = -1;

        /// <summary>True while a sequence coroutine is alive.</summary>
        public bool IsRunning => _loop != null;

        /// <summary>True while a cutscene is playing or a modal overlay is open.</summary>
        private bool IsSuspended =>
            (_cutscenes != null && _cutscenes.IsPlaying) || (overlays != null && overlays.AnyOpen);

        private void Awake()
        {
            _restoration = ResolveSource<IRestorationRuntime>(restorationSource, nameof(restorationSource));
            _router = ResolveSource<IScreenRouter>(screenRouterSource, nameof(screenRouterSource));
        }

        private void OnEnable()
        {
            if (_router != null)
            {
                _router.ScreenChanged += OnScreenChanged;
            }

            if (_restoration != null)
            {
                _restoration.StageStarted += OnStageStarted;
                _restoration.StageCompleted += OnStageCompletedGlobal;
                _restoration.ToolSelected += OnToolSelectedGlobal;
            }

            // Safe here per contract amendment A1: SceneLoader clears GameSignals
            // just BEFORE this scene's objects run Awake/OnEnable, so subscribing
            // in OnEnable can never pick up a stale listener from the last scene.
            GameSignals.StickerPeeled += OnStickerPeeled;
            GameSignals.PreviewOpened += OnPreviewOpened;
        }

        private void Start()
        {
            // Process-lifetime services: resolved here, never Awake, because the
            // Systems object registers them in ITS Awake and Awake order between
            // two different objects is not guaranteed.
            _localization = ServiceLocator.Get<ILocalizationService>();
            _save = ServiceLocator.Get<ISaveService>();
            _audio = ServiceLocator.Get<IAudioService>();
            _cutscenes = ServiceLocator.Get<ICutscenePlayer>();
            _inventory = ServiceLocator.Get<IDecorationInventory>();
            _progress = ServiceLocator.Get<IPosterProgress>();

            if (_localization == null)
            {
                Debug.LogWarning("[TutorialController] No ILocalizationService registered. Tracy will " +
                                 "show raw localisation keys. Add the Systems object to the scene.", this);
            }

            if (_inventory != null)
            {
                _inventory.ItemPurchased += OnItemPurchased;
                _inventorySubscribed = true;
            }

            BuildCandidates();

            if (!runAutomatically)
            {
                return;
            }

            ResumeIfNeeded();
        }

        private void OnDisable()
        {
            if (_router != null)
            {
                _router.ScreenChanged -= OnScreenChanged;
            }

            if (_restoration != null)
            {
                _restoration.StageStarted -= OnStageStarted;
                _restoration.StageCompleted -= OnStageCompletedGlobal;
                _restoration.ToolSelected -= OnToolSelectedGlobal;
            }

            GameSignals.StickerPeeled -= OnStickerPeeled;
            GameSignals.PreviewOpened -= OnPreviewOpened;

            if (_inventorySubscribed && _inventory != null)
            {
                _inventory.ItemPurchased -= OnItemPurchased;
            }

            _inventorySubscribed = false;
            StopLoop();
        }

        // ---- Public API ---------------------------------------------------------------

        /// <summary>
        /// Starts <paramref name="sequenceId"/> right now, ignoring its trigger. False
        /// when another sequence is already running, the id is unknown, or it has
        /// already been completed.
        /// </summary>
        public bool TryStart(string sequenceId)
        {
            if (_loop != null)
            {
                return false;
            }

            var seq = FindSequence(sequenceId);

            if (seq == null)
            {
                Debug.LogWarning($"[TutorialController] TryStart('{sequenceId}'): no such sequence in " +
                                 "'Sequences'.", this);
                return false;
            }

            if (IsCompleted(sequenceId))
            {
                return false;
            }

            RunSequence(seq, 0);
            return true;
        }

        /// <summary>
        /// Abandons the running sequence immediately and marks it complete. Safe at
        /// any moment; the escape hatch a stuck playtester (or the human) reaches for.
        /// </summary>
        [ContextMenu("Skip Tutorial")]
        public void Skip()
        {
            var seq = _activeSequence;
            StopLoop();

            if (seq != null)
            {
                FinishSequence(seq);
            }
        }

        /// <summary>Clears the saved position and plays the active (or first) sequence again from the top.</summary>
        [ContextMenu("Restart Tutorial")]
        public void Restart()
        {
            var id = _activeSequence != null ? _activeSequence.sequenceId
                : (sequences != null && sequences.Length > 0 && sequences[0] != null ? sequences[0].sequenceId : null);

            RestartSequence(id);
        }

        /// <summary>Clears the saved position for <paramref name="sequenceId"/> and plays it from the top.</summary>
        public void RestartSequence(string sequenceId)
        {
            var seq = FindSequence(sequenceId);

            if (seq == null)
            {
                return;
            }

            StopLoop();

            if (_save != null && _save.Data != null)
            {
                _save.Data.EnsureCollections();
                _save.Data.completedTutorialSequences.Remove(sequenceId);
                _save.Data.activeTutorialSequence = string.Empty;
                _save.Data.activeTutorialStepIndex = 0;
                _save.Save();
            }

            RunSequence(seq, 0);
        }

        // ---- Resuming / triggering ----------------------------------------------------

        private void ResumeIfNeeded()
        {
            var data = _save != null ? _save.Data : null;

            if (data == null || string.IsNullOrEmpty(data.activeTutorialSequence))
            {
                return;
            }

            if (data.completedTutorialSequences != null &&
                data.completedTutorialSequences.Contains(data.activeTutorialSequence))
            {
                // Stale: finished but the pointer was not cleared (should not happen,
                // but never trust a save file more than the code that reads it).
                data.activeTutorialSequence = string.Empty;
                data.activeTutorialStepIndex = 0;
                _save.Save();
                return;
            }

            var seq = FindSequence(data.activeTutorialSequence);

            if (seq == null)
            {
                Debug.LogWarning($"[TutorialController] Saved sequence '{data.activeTutorialSequence}' is " +
                                 "not in 'Sequences'. Clearing it so the game is not stuck waiting for it.", this);
                data.activeTutorialSequence = string.Empty;
                data.activeTutorialStepIndex = 0;
                _save.Save();
                return;
            }

            var resume = TutorialTriggerRules.ClampResumeIndex(data.activeTutorialStepIndex, seq.StepCount);

            if (resume >= seq.StepCount)
            {
                FinishSequence(seq);
                return;
            }

            RunSequence(seq, resume);
        }

        private void TryStartForScreen(GameScreen screen)
        {
            if (_loop != null || _candidates == null)
            {
                return;
            }

            var index = TutorialTriggerRules.FindForScreen(_candidates, screen, CompletedIds(), IsPosterCompleted);

            if (index >= 0)
            {
                RunSequence(sequences[index], 0);
            }
        }

        private void TryStartForStageKind(StageKind kind)
        {
            if (_loop != null || _candidates == null)
            {
                return;
            }

            var index = TutorialTriggerRules.FindForStageKind(_candidates, kind, CompletedIds(), IsPosterCompleted);

            if (index >= 0)
            {
                RunSequence(sequences[index], 0);
            }
        }

        private void BuildCandidates()
        {
            var count = sequences != null ? sequences.Length : 0;
            _candidates = new TriggerCandidate[count];

            for (var i = 0; i < count; i++)
            {
                var seq = sequences[i];

                if (seq == null)
                {
                    continue;
                }

                _candidates[i] = new TriggerCandidate
                {
                    SequenceId = seq.sequenceId,
                    TriggerScreen = seq.trigger == TutorialTrigger.ScreenEntered ? seq.triggerScreen : GameScreen.None,
                    HasStageKind = seq.trigger == TutorialTrigger.StageKindStarted,
                    TriggerStageKind = seq.triggerStageKind,
                    RequiresSequence = seq.requiresSequence,
                    RequiresPosterCompleted = seq.requiresPosterCompleted
                };
            }
        }

        private List<string> CompletedIds() =>
            _save != null && _save.Data != null ? _save.Data.completedTutorialSequences : null;

        private bool IsCompleted(string sequenceId)
        {
            var list = CompletedIds();
            return list != null && list.Contains(sequenceId);
        }

        private bool IsPosterCompleted(string posterId) =>
            _progress != null && _progress.IsCompleted(posterId);

        private TutorialSequenceData FindSequence(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId) || sequences == null)
            {
                return null;
            }

            for (var i = 0; i < sequences.Length; i++)
            {
                if (sequences[i] != null && sequences[i].sequenceId == sequenceId)
                {
                    return sequences[i];
                }
            }

            return null;
        }

        // ---- Running a sequence ---------------------------------------------------------

        private void RunSequence(TutorialSequenceData seq, int startIndex)
        {
            if (seq == null || seq.StepCount == 0)
            {
                Debug.LogWarning($"[TutorialController] '{(seq != null ? seq.sequenceId : "?")}' has no " +
                                 "steps; not starting it.", this);
                return;
            }

            StopLoop();
            _activeSequence = seq;
            _loop = StartCoroutine(RunSequenceCoroutine(seq, Mathf.Max(0, startIndex)));
        }

        private IEnumerator RunSequenceCoroutine(TutorialSequenceData seq, int startIndex)
        {
            HideEverything();

            // One frame so every screen's TutorialAnchor has run its OnEnable and
            // registered itself before the first lookup.
            yield return null;

            for (var i = startIndex; i < seq.StepCount; i++)
            {
                var step = seq.steps[i];

                if (step == null)
                {
                    Debug.LogWarning($"[TutorialController] '{seq.sequenceId}' step {i} is empty; skipping.", this);
                    continue;
                }

                CurrentStepIndex = i;
                StepStarted?.Invoke(seq, step, i);

                yield return RunStep(seq, step);

                // Persist AFTER the step, pointing at the next one, so a kill here
                // resumes on the step the player has not done yet.
                Persist(seq, i + 1);
            }

            _loop = null;
            FinishSequence(seq);
        }

        private IEnumerator RunStep(TutorialSequenceData seq, TutorialStepData step)
        {
            _activeStep = step;
            _advanced = false;
            _overlayDismissed = false;

            var overlay = step.presentation == TracyPresentation.HubFullBody && hubOverlay != null
                ? hubOverlay
                : portraitOverlay;
            _activeOverlay = overlay;

            if (step.advance == TutorialAdvance.TapAnywhere && overlay != null)
            {
                overlay.Tapped += OnTapSignal;
            }
            else if (step.advance == TutorialAdvance.TapTarget)
            {
                AttachProbe(step.targetAnchorId);
            }

            // Some conditions are already true the moment the step opens, most often
            // right after resuming from a save. Honour them instead of waiting.
            if (IsConditionAlreadyMet(step))
            {
                _advanced = true;
            }

            // ---- Read phase: the player sets the pace --------------------------
            if (overlay != null)
            {
                if (string.IsNullOrEmpty(step.lineKey))
                {
                    overlay.Hide();
                }
                else
                {
                    yield return WaitWhileSuspended();
                    yield return WaitRealtimeUnlessSuspended(dialogueDelaySeconds);

                    overlay.Tapped += OnOverlayDismissed;
                    overlay.Show(step.lineKey, step.mood, true);

                    var reading = 0f;

                    while (!_overlayDismissed && (!useSafetyTimeout || reading < fallbackTimeout))
                    {
                        if (!IsSuspended)
                        {
                            reading += Time.unscaledDeltaTime;
                        }

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

            if (_restoration != null)
            {
                _restoration.StageProgressChanged += OnStageProgress;
            }

            var timeout = step.autoAdvanceSeconds > 0f ? step.autoAdvanceSeconds : fallbackTimeout;
            var elapsed = 0f;
            var pointerShown = false;

            while (!_advanced && (!useSafetyTimeout || elapsed < timeout))
            {
                var suspended = IsSuspended;
                handPointer?.SetSuspended(suspended);

                if (!suspended)
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
                }

                yield return null;
            }

            if (_restoration != null)
            {
                _restoration.StageProgressChanged -= OnStageProgress;
            }

            if (!_advanced && useSafetyTimeout)
            {
                Debug.Log($"[TutorialController] '{seq.sequenceId}'.'{step.stepId}' timed out after " +
                          $"{timeout:0.#}s and advanced on its safety valve.", this);
            }

            if (overlay != null)
            {
                overlay.Tapped -= OnTapSignal;
            }

            DetachProbe();

            _activeStep = null;
            _activeOverlay = null;

            handPointer?.Clear();
            handPointer?.SetSuspended(false);

            if (overlay != null)
            {
                overlay.Hide();
            }

            inputGate?.ReleaseAll();
        }

        private IEnumerator WaitWhileSuspended()
        {
            while (IsSuspended)
            {
                yield return null;
            }
        }

        private IEnumerator WaitRealtimeUnlessSuspended(float seconds)
        {
            var t = 0f;

            while (t < seconds)
            {
                if (!IsSuspended)
                {
                    t += Time.unscaledDeltaTime;
                }

                yield return null;
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

        private void FinishSequence(TutorialSequenceData seq)
        {
            if (seq == null)
            {
                return;
            }

            _activeSequence = null;
            CurrentStepIndex = -1;

            if (_save != null && _save.Data != null)
            {
                _save.Data.EnsureCollections();

                if (!_save.Data.completedTutorialSequences.Contains(seq.sequenceId))
                {
                    _save.Data.completedTutorialSequences.Add(seq.sequenceId);
                }

                _save.Data.activeTutorialSequence = string.Empty;
                _save.Data.activeTutorialStepIndex = 0;
                _save.Save();
            }

            SequenceFinished?.Invoke(seq.sequenceId);
        }

        private void Persist(TutorialSequenceData seq, int nextIndex)
        {
            if (_save == null || _save.Data == null)
            {
                return;
            }

            _save.Data.activeTutorialSequence = seq.sequenceId;
            _save.Data.activeTutorialStepIndex = Mathf.Clamp(nextIndex, 0, seq.StepCount);
            _save.Save();
        }

        private void StopLoop()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }

            if (_activeOverlay != null)
            {
                _activeOverlay.Tapped -= OnTapSignal;
                _activeOverlay.Tapped -= OnOverlayDismissed;
            }

            DetachProbe();

            if (_restoration != null)
            {
                _restoration.StageProgressChanged -= OnStageProgress;
            }

            _activeStep = null;
            _activeOverlay = null;
            CurrentStepIndex = -1;

            HideEverything();
        }

        private void HideEverything()
        {
            portraitOverlay?.Hide();
            hubOverlay?.Hide();
            handPointer?.Clear();
            handPointer?.SetSuspended(false);
            inputGate?.ReleaseAll();
        }

        // ---- Always-on trigger / advance handlers --------------------------------------

        private void OnScreenChanged(GameScreen screen)
        {
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.ScreenEntered &&
                screen == _activeStep.requiredScreen)
            {
                _advanced = true;
            }

            TryStartForScreen(screen);
        }

        private void OnStageStarted(RestorationStageData stage, int index)
        {
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.StageStarted)
            {
                _advanced = true;
            }

            if (stage != null)
            {
                TryStartForStageKind(stage.kind);
            }
        }

        private void OnStageCompletedGlobal(RestorationStageData stage, int index)
        {
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.StageCompleted)
            {
                _advanced = true;
            }
        }

        private void OnToolSelectedGlobal(ToolId tool)
        {
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.ToolSelected &&
                tool == _activeStep.requiredTool)
            {
                _advanced = true;
            }
        }

        private void OnItemPurchased(string itemId)
        {
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.ItemPurchased &&
                TutorialTriggerRules.ItemMatches(_activeStep.requiredItemId, itemId))
            {
                _advanced = true;
            }
        }

        private void OnPreviewOpened(string itemId)
        {
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.PreviewOpened &&
                TutorialTriggerRules.ItemMatches(_activeStep.requiredItemId, itemId))
            {
                _advanced = true;
            }
        }

        private void OnStickerPeeled(int remaining)
        {
            // Only the LAST peel ends the step: the hand keeps following
            // "sticker.next" (which TutorialAnchor itself moves to the next
            // un-peeled sticker) for every peel before that one.
            if (_activeStep != null && _activeStep.advance == TutorialAdvance.StickerPeeled && remaining <= 0)
            {
                _advanced = true;
            }
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

        private void OnTapSignal()
        {
            _audio?.PlaySfx(SfxId.ButtonClick);
            _advanced = true;
        }

        /// <summary>Tap on Tracy's panel. Ends the reading pause, never the step by itself.</summary>
        private void OnOverlayDismissed()
        {
            _overlayDismissed = true;
        }

        // ---- Tap-target probe -----------------------------------------------------------

        private void AttachProbe(string anchorId)
        {
            DetachProbe();

            var anchor = TutorialAnchor.Find(anchorId);

            if (anchor == null)
            {
                // Fail open: without a probe this step rides its safety timeout out
                // instead of waiting for a tap that can never be detected.
                Debug.LogWarning($"[TutorialController] Cannot watch for a tap on '{anchorId}': no enabled " +
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

        // ---- Wiring -----------------------------------------------------------------

        private T ResolveSource<T>(MonoBehaviour source, string fieldName) where T : class
        {
            if (source == null)
            {
                Debug.LogWarning($"[TutorialController] '{fieldName}' is empty. Steps that wait on " +
                                 $"{typeof(T).Name} will fall back to their safety timeout.", this);
                return null;
            }

            if (source is T typed)
            {
                return typed;
            }

            var found = source.GetComponent<T>();

            if (found != null)
            {
                return found;
            }

            Debug.LogError($"[TutorialController] The object in '{fieldName}' ('{source.name}', component " +
                           $"{source.GetType().Name}) does not implement {typeof(T).Name}. Drag the GameObject " +
                           $"that has the {typeof(T).Name} component into that slot instead.", this);
            return null;
        }
    }
}
