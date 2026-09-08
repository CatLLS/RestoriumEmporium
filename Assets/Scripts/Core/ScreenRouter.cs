// ============================================================
// ScreenRouter — owns "which screen of the Game scene is visible".
// WHAT & WHY: Journal, Cleaning, the three LinenBacking views and FinishedRepair
//   all live in one scene so the poster layer stack survives between them and no
//   flip animation ever straddles a scene load. This is the IScreenRouter
//   implementation that shows exactly one of them at a time.
// KEY DECISIONS:
//   - Views are looked up by their own ScreenView.Screen property into a
//     Dictionary built once in Awake, never by Inspector array order. Ordering
//     is the kind of thing someone breaks by dragging objects in the Hierarchy,
//     and it would fail silently at runtime.
//   - The serialized array is the source of truth, with GetComponentsInChildren
//     as a fallback when it is left empty. The explicit list keeps the wiring
//     visible and diffable in the scene file; the fallback means a freshly built
//     scene works before anyone remembers to fill it in.
//   - Asking for an unregistered screen logs an error and leaves the current one
//     up. Hiding the old screen first and then discovering there is nothing to
//     show would strand the player on a black screen with no way forward — a
//     visible wrong screen plus a console error is a far cheaper failure.
//   - Awake hides every registered view. Screens are left enabled in the Editor
//     so they can be authored, and without this the first frame would show all
//     six stacked on top of each other.
//   - ScreenChanged fires after Show(), so listeners that read layout get valid
//     rects instead of last frame's.
//   - Go() HOLDS for Screen Change Delay Seconds before it swaps anything. This
//     is a cozy game and a screen that changes on the same frame the player lifts
//     their finger reads as the game snatching the work away from them. The hold
//     lives here rather than at the four call sites so there is one number to
//     tune and no way to add a fifth call site that forgets it.
//   - The hold is skipped when nothing is on screen yet (Current is None). That
//     is the scene's first route, where there is no transition to soften — only a
//     second of black.
//   - A Go() during a pending hold REPLACES it rather than queueing. The pending
//     screen is also what Go() compares against for its idempotence check, so the
//     several-systems-ask-for-the-same-screen case still collapses to one change.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] Open the Game scene. In the Hierarchy create an empty object via
//     GameObject -> Create Empty and rename it to exactly "GameFlow".
// [x] Select "GameFlow", click "Add Component" in the Inspector, type
//     "ScreenRouter", press Enter.
// [x] Each screen root object (JournalScreen, CleaningScreen,
//     LinenBackingFrontScreen, LinenBackingBackScreen, LinenBackingFinalScreen,
//     FinishedRepairScreen) must have a component deriving from ScreenView on it.
//     Those components are built by the UI work; this router only needs them to
//     exist.
// [x] On the ScreenRouter component, set "Screens" Size to the number of screen
//     objects you have, then drag each screen root from the Hierarchy into one
//     of the empty slots. Order does not matter.
//     (If you leave Size at 0, the router instead finds every ScreenView that is
//     a CHILD of "GameFlow" — only useful if you parent the screens under it.)
// [x] Leave all screen objects ticked/active in the Hierarchy while you author
//     them. The router hides them all on the first frame.
// [ ] "Screen Change Delay Seconds" is the beat before EVERY screen change (1s by
//     default). Turn it down to 0 while you are testing a long flow; turn it up
//     if the game still feels like it is rushing you.
// ---------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Core
{
    [DisallowMultipleComponent]
    public class ScreenRouter : MonoBehaviour, IScreenRouter
    {
        [Header("Views")]
        [Tooltip("Every full-screen view this router can show. Drag the screen root " +
                 "objects here. Leave empty to auto-find ScreenViews under this object.")]
        [SerializeField] private ScreenView[] screens = Array.Empty<ScreenView>();

        [Header("Pacing")]
        [Tooltip("Seconds the current screen is held before a Go() actually swaps " +
                 "it. The beat is what keeps a finished stage from being whisked " +
                 "away the instant the player lifts their finger. Set to 0 for the " +
                 "old immediate behaviour. Not applied to the scene's first screen.")]
        [Range(0f, 3f)]
        [SerializeField] private float screenChangeDelaySeconds = 1f;

        private readonly Dictionary<GameScreen, ScreenView> _views =
            new Dictionary<GameScreen, ScreenView>();

        private ScreenView _currentView;
        private Coroutine _pending;
        private GameScreen _pendingScreen = GameScreen.None;

        /// <inheritdoc />
        public GameScreen Current { get; private set; } = GameScreen.None;

        /// <summary>The screen a held Go() is on its way to, or Current when none is.</summary>
        public GameScreen Requested => _pending != null ? _pendingScreen : Current;

        /// <inheritdoc />
        public event Action<GameScreen> ScreenChanged;

        private void Awake()
        {
            if (screens == null || screens.Length == 0)
            {
                // true = include inactive, so a screen already hidden in the
                // scene is still discovered.
                screens = GetComponentsInChildren<ScreenView>(true);
            }

            BuildIndex();
            HideAll();
        }

        /// <inheritdoc />
        public void Go(GameScreen screen)
        {
            if (screen == Requested)
            {
                // Idempotent by contract: several systems can ask for the same
                // screen in one frame and none of them should replay its entry.
                return;
            }

            if (!_views.ContainsKey(screen))
            {
                // Reported here rather than after the hold, so a mis-wired screen
                // still shows up in the console on the frame it was asked for.
                LogUnregistered(screen);
                return;
            }

            CancelPending();

            // Nothing on screen yet means this is the scene's first route: a hold
            // there is just a second of black, not a beat.
            if (screenChangeDelaySeconds <= 0f || Current == GameScreen.None)
            {
                Apply(screen);
                return;
            }

            _pendingScreen = screen;
            _pending = StartCoroutine(GoAfterDelay(screen));
        }

        /// <summary>
        /// Changes screen with no hold, cancelling any pending one. For the rare
        /// caller that must not wait — a quit or an error path.
        /// </summary>
        public void GoNow(GameScreen screen)
        {
            CancelPending();

            if (screen != Current)
            {
                Apply(screen);
            }
        }

        private IEnumerator GoAfterDelay(GameScreen screen)
        {
            // Unscaled, so the beat is the same length whether or not something
            // has paused the game underneath it.
            yield return new WaitForSecondsRealtime(screenChangeDelaySeconds);

            _pending = null;
            _pendingScreen = GameScreen.None;
            Apply(screen);
        }

        private void Apply(GameScreen screen)
        {
            if (!_views.TryGetValue(screen, out var next) || next == null)
            {
                LogUnregistered(screen);
                return;
            }

            if (_currentView != null)
            {
                _currentView.Hide();
            }

            _currentView = next;
            Current = screen;
            next.Show();

            ScreenChanged?.Invoke(screen);
        }

        private void CancelPending()
        {
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }

            _pendingScreen = GameScreen.None;
        }

        private void LogUnregistered(GameScreen screen)
        {
            Debug.LogError(
                $"[ScreenRouter] No ScreenView registered for {screen}. Staying on {Current}. " +
                "Add the screen object to the 'Screens' list on this component, and make sure " +
                "its ScreenView subclass returns the right value from its Screen property.",
                this);
        }

        /// <summary>True when a view is registered for <paramref name="screen"/>.</summary>
        public bool Has(GameScreen screen) => _views.ContainsKey(screen);

        /// <summary>
        /// Looks up the view serving <paramref name="screen"/>. Lets the
        /// presentation layer read a screen's layout (the root the poster is
        /// parented under, say) without holding six Inspector references that
        /// would duplicate this component's list.
        /// </summary>
        public bool TryGetView(GameScreen screen, out ScreenView view)
        {
            return _views.TryGetValue(screen, out view) && view != null;
        }

        private void BuildIndex()
        {
            _views.Clear();

            if (screens == null)
            {
                return;
            }

            for (var i = 0; i < screens.Length; i++)
            {
                var view = screens[i];

                if (view == null)
                {
                    Debug.LogWarning($"[ScreenRouter] 'Screens' slot {i} is empty. " +
                                     "Drag a screen object into it or shrink the list.", this);
                    continue;
                }

                var id = view.Screen;

                if (id == GameScreen.None)
                {
                    Debug.LogError($"[ScreenRouter] '{view.name}' reports GameScreen.None and " +
                                   "can never be routed to.", view);
                    continue;
                }

                if (_views.TryGetValue(id, out var existing))
                {
                    Debug.LogError($"[ScreenRouter] Both '{existing.name}' and '{view.name}' claim " +
                                   $"{id}. Keeping '{existing.name}'; remove or re-tag the other.",
                                   view);
                    continue;
                }

                _views.Add(id, view);
            }

            if (_views.Count == 0)
            {
                Debug.LogError("[ScreenRouter] No screens registered. Every Go() call will fail. " +
                               "Fill in the 'Screens' list on this component.", this);
            }
        }

        private void HideAll()
        {
            CancelPending();

            foreach (var pair in _views)
            {
                pair.Value.Hide();
            }

            _currentView = null;
            Current = GameScreen.None;
        }
    }
}
