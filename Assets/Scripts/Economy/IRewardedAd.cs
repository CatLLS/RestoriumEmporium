// ============================================================
// IRewardedAd — "watch an ad to double your coins", network-agnostic.
// WHAT & WHY: §5 of the build spec: ads are not a scene, the SDK draws its own
//   overlay, and the game must run with no ad network at all. Everything that
//   wants a rewarded ad talks to this interface; the AdMob implementation arrives
//   in a later build and replaces the placeholder in GameBootstrap only.
// KEY DECISIONS:
//   - Show() reports through a callback with a bool: true ONLY when the network
//     says the user earned the reward. Closing early, failing to load, or no
//     network all report false. The caller grants coins in that callback, never
//     before.
//   - IsReady gates the button. When no ad can be shown the "Double Reward"
//     button is hidden rather than shown-and-broken.
//   - This build ships two placeholders (see SimulatedRewardedAd): in the Editor
//     and Development builds it is always ready and "earns" after a short delay so
//     the double-reward path is testable; in a release build it is never ready.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing for this build. GameBootstrap registers the placeholder.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Economy
{
    public interface IRewardedAd
    {
        /// <summary>True when Show() would actually display an ad.</summary>
        bool IsReady { get; }

        /// <summary>Raised when IsReady changes.</summary>
        event Action ReadyChanged;

        /// <summary>Shows the ad. <paramref name="onFinished"/>(true) only when the reward was earned.</summary>
        void Show(Action<bool> onFinished);
    }
}
