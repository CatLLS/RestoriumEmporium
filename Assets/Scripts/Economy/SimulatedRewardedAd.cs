// ============================================================
// SimulatedRewardedAd — stand-in IRewardedAd until AdMob arrives.
// WHAT & WHY: The Finished Repair screen's "Double Reward" button needs an
//   IRewardedAd to exist, and §1.6 of the build spec says the whole loop must
//   run with no ad network. This placeholder lets the double-reward path be
//   played end to end in the Editor and in Development builds, and hides the
//   button in release builds, where showing a fake ad would be dishonest.
// KEY DECISIONS:
//   - IsReady is Debug.isDebugBuild: true in the Editor and in builds with
//     "Development Build" ticked, false in a release build. There is nothing to
//     configure, so a release can never accidentally ship free double coins.
//   - "Showing" is just a wait of Earn Delay Seconds on UNSCALED time, then
//     onFinished(true). The delay is long enough to see the flow is async (the
//     button must not pay twice on a double tap) and short enough not to annoy.
//   - onFinished is called EXACTLY once per Show(): a second Show() while one is
//     running reports false straight away, and if this component is disabled or
//     destroyed mid-"ad" the pending one reports false.
//   - Added at runtime by GameBootstrap (AddComponent) when the Systems object
//     does not already have one, so it needs no Inspector wiring. When the real
//     AdMob wrapper exists, GameBootstrap registers that instead.
//   - ReadyChanged is never raised: readiness is fixed for the whole session.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing required. GameBootstrap adds this component to the "Systems"
//     object at runtime if it is missing.
// [ ] Optional: to tune it, add it yourself — select "Systems" in the Title
//     scene -> Add Component -> "Simulated Rewarded Ad" — and change
//     "Earn Delay Seconds", or untick "Simulate Earned" to test the path where
//     the player closes the ad early (no coins).
// ---------------------------------------------------------------

using System;
using System.Collections;
using UnityEngine;

namespace RestoriumEmporium.Economy
{
    [DisallowMultipleComponent]
    public class SimulatedRewardedAd : MonoBehaviour, IRewardedAd
    {
        [Tooltip("Seconds the pretend ad 'plays' before reporting. Real time, not game time.")]
        [Range(0f, 10f)]
        [SerializeField] private float earnDelaySeconds = 1.5f;

        [Tooltip("Ticked: the pretend ad reports the reward as earned. " +
                 "Unticked: it reports 'closed early' so the no-reward path can be tested.")]
        [SerializeField] private bool simulateEarned = true;

        private Action<bool> _pending;
        private Coroutine _routine;

        /// <inheritdoc />
        public bool IsReady => Debug.isDebugBuild && isActiveAndEnabled && _pending == null;

        /// <inheritdoc />
        public event Action ReadyChanged
        {
            // Readiness never changes during a session; nothing to store.
            add { }
            remove { }
        }

        /// <inheritdoc />
        public void Show(Action<bool> onFinished)
        {
            if (onFinished == null)
            {
                onFinished = _ => { };
            }

            if (!Debug.isDebugBuild || !isActiveAndEnabled)
            {
                onFinished(false);
                return;
            }

            if (_pending != null)
            {
                Debug.LogWarning("[SimulatedRewardedAd] Show() called while an ad is already " +
                                 "showing. Reporting 'not earned' for the second call.", this);
                onFinished(false);
                return;
            }

            Debug.Log($"[SimulatedRewardedAd] Pretending to show a rewarded ad for " +
                      $"{earnDelaySeconds:0.#}s (Editor/Development build only).", this);

            _pending = onFinished;
            _routine = StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            if (earnDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(earnDelaySeconds);
            }

            _routine = null;
            Complete(simulateEarned);
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            Complete(false);
        }

        private void Complete(bool earned)
        {
            var callback = _pending;
            _pending = null;
            callback?.Invoke(earned);
        }
    }
}
