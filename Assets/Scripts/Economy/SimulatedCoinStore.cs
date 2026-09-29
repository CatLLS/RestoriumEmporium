// ============================================================
// SimulatedCoinStore — stand-in ICoinStore for the Editor (RevenueCat can't run there).
// WHAT & WHY: The RevenueCat Unity SDK does nothing inside the Editor, so without
//   this the coin screen could only be tested on a phone. This pretends to sell:
//   a short wait, then the outcome picked in the Inspector. Same idea as
//   SimulatedRewardedAd.
// KEY DECISIONS:
//   - Only works when Debug.isDebugBuild (Editor, or "Development Build" ticked).
//     A release build that somehow ends up with this store reports every
//     purchase as Unavailable, so free coins can never ship.
//   - Returns no prices: the coin screen keeps CoinPackCatalog's fallback labels,
//     which is exactly what the Figma shows.
//   - Each simulated success gets a fresh transaction id, so the purchase ledger
//     behaves exactly as it does with the real store.
//   - Every callback fires exactly once; disabling the component mid-purchase
//     reports Failed.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. GameBootstrap adds it to "Systems" at runtime in the Editor.
// [ ] Optional: while playing, select "Systems" (DontDestroyOnLoad) and change
//     "Outcome" to test the Cancelled / Failed paths.
// ---------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Economy
{
    [DisallowMultipleComponent]
    public class SimulatedCoinStore : MonoBehaviour, ICoinStore
    {
        [Tooltip("Seconds the pretend store sheet stays up. Real time, not game time.")]
        [Range(0f, 5f)]
        [SerializeField] private float purchaseDelaySeconds = 0.8f;

        [Tooltip("What every simulated purchase reports.")]
        [SerializeField] private CoinPurchaseStatus outcome = CoinPurchaseStatus.Success;

        private Action<CoinPurchaseResult> _pending;
        private string _pendingProduct;
        private Coroutine _routine;

        public bool IsReady => Debug.isDebugBuild;

        public event Action ReadyChanged
        {
            // Readiness never changes during a session; nothing to store.
            add { }
            remove { }
        }

        public void FetchProducts(IReadOnlyList<string> productIds,
            Action<IReadOnlyList<CoinStoreProduct>, string> done)
        {
            done?.Invoke(Array.Empty<CoinStoreProduct>(), null);
        }

        public void FetchPastTransactions(Action<IReadOnlyList<CoinTransaction>, string> done)
        {
            done?.Invoke(Array.Empty<CoinTransaction>(), null);
        }

        public void Purchase(string productId, Action<CoinPurchaseResult> done)
        {
            done ??= _ => { };

            if (!Debug.isDebugBuild || !isActiveAndEnabled)
            {
                done(CoinPurchaseResult.Unavailable(productId, "Simulated store is Editor/Development only."));
                return;
            }

            if (_pending != null)
            {
                done(CoinPurchaseResult.Failed(productId, "A simulated purchase is already running."));
                return;
            }

            Debug.Log($"[SimulatedCoinStore] Pretending to buy '{productId}' -> {outcome} " +
                      "(Editor/Development build only; no money is involved).", this);

            _pending = done;
            _pendingProduct = productId;
            _routine = StartCoroutine(PurchaseRoutine());
        }

        private IEnumerator PurchaseRoutine()
        {
            if (purchaseDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(purchaseDelaySeconds);
            }

            _routine = null;

            switch (outcome)
            {
                case CoinPurchaseStatus.Success:
                    Complete(CoinPurchaseResult.Success(_pendingProduct, "sim-" + Guid.NewGuid().ToString("N")));
                    break;
                case CoinPurchaseStatus.Cancelled:
                    Complete(CoinPurchaseResult.Cancelled(_pendingProduct));
                    break;
                case CoinPurchaseStatus.Unavailable:
                    Complete(CoinPurchaseResult.Unavailable(_pendingProduct, "Simulated: store unavailable."));
                    break;
                default:
                    Complete(CoinPurchaseResult.Failed(_pendingProduct, "Simulated: purchase failed."));
                    break;
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_pending != null)
            {
                Complete(CoinPurchaseResult.Failed(_pendingProduct, "Simulated store was disabled mid-purchase."));
            }
        }

        private void Complete(CoinPurchaseResult result)
        {
            var callback = _pending;
            _pending = null;
            _pendingProduct = null;
            callback?.Invoke(result);
        }
    }
}
