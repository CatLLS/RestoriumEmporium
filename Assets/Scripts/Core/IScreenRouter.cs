// ============================================================
// IScreenRouter — moves the player between the views of the Game scene.
// WHAT & WHY: Journal, Cleaning, LinenBacking and FinishedRepair all live in
//   one scene so that the poster layer stack survives across them and the flip
//   animations never straddle a scene load. Something has to own "which one is
//   visible"; this is that contract.
// KEY DECISIONS:
//   - Go() is idempotent: routing to the current screen does nothing and does
//     not re-raise ScreenChanged. Transitions are triggered from several places
//     (stage completion, tutorial, buttons) and a duplicate call must not
//     replay a screen's entry animation.
//   - ScreenChanged fires after the new screen is shown, so listeners that
//     query layout get valid rects rather than a frame-old one.
//   - No Back(). The MVP flow is strictly forward, and a history stack that is
//     never popped is just a leak with extra steps.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by ScreenRouter on the GameFlow object in Game.unity.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Core
{
    public interface IScreenRouter
    {
        GameScreen Current { get; }

        /// <summary>Raised after the new screen has been shown.</summary>
        event Action<GameScreen> ScreenChanged;

        /// <summary>Shows <paramref name="screen"/> and hides the previous one. Idempotent.</summary>
        void Go(GameScreen screen);
    }
}
