// ============================================================
// ScreenView — base class for every full-screen view in the Game scene.
// WHAT & WHY: All four gameplay screens live in one scene as sibling objects.
//   This gives ScreenRouter a uniform way to show and hide them without knowing
//   what any individual screen contains.
// KEY DECISIONS:
//   - Toggles the whole GameObject rather than a CanvasGroup. A hidden screen
//     must not receive raycasts or run Update; SetActive gives both for free,
//     and there are few enough screens that the enable/disable cost is nil.
//   - OnShown/OnHidden are protected virtuals rather than events, because a
//     screen's own setup is not something other objects should subscribe to.
//   - Screen is abstract, so a view cannot exist without declaring its identity;
//     ScreenRouter discovers views by this property instead of by inspector
//     ordering, which would silently break when someone reorders the hierarchy.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing directly — attach the concrete subclasses (JournalScreen,
//     CleaningScreen, LinenBackingScreen, FinishedRepairScreen) to their
//     corresponding root objects under the Canvas.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Core
{
    public abstract class ScreenView : MonoBehaviour
    {
        /// <summary>Which screen this view represents.</summary>
        public abstract GameScreen Screen { get; }

        public bool IsVisible => gameObject.activeSelf;

        public void Show()
        {
            if (IsVisible)
            {
                return;
            }

            gameObject.SetActive(true);
            OnShown();
        }

        public void Hide()
        {
            if (!IsVisible)
            {
                return;
            }

            OnHidden();
            gameObject.SetActive(false);
        }

        /// <summary>Called after the object is enabled. Refresh visuals here.</summary>
        protected virtual void OnShown() { }

        /// <summary>Called just before the object is disabled. Cancel work here.</summary>
        protected virtual void OnHidden() { }
    }
}
