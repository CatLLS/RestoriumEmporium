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
// ---------------------------------------------------------------

using System;
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

        private readonly Dictionary<GameScreen, ScreenView> _views =
            new Dictionary<GameScreen, ScreenView>();

        private ScreenView _currentView;

        /// <inheritdoc />
        public GameScreen Current { get; private set; } = GameScreen.None;

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
            if (screen == Current)
            {
                // Idempotent by contract: several systems can ask for the same
                // screen in one frame and none of them should replay its entry.
                return;
            }

            if (!_views.TryGetValue(screen, out var next) || next == null)
            {
                Debug.LogError(
                    $"[ScreenRouter] No ScreenView registered for {screen}. Staying on {Current}. " +
                    "Add the screen object to the 'Screens' list on this component, and make sure " +
                    "its ScreenView subclass returns the right value from its Screen property.",
                    this);
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

        /// <summary>True when a view is registered for <paramref name="screen"/>.</summary>
        public bool Has(GameScreen screen) => _views.ContainsKey(screen);

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
            foreach (var pair in _views)
            {
                pair.Value.Hide();
            }

            _currentView = null;
            Current = GameScreen.None;
        }
    }
}
