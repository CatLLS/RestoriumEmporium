// ============================================================
// CoinPurchaseService — turns a paid coin-pack purchase into coins, exactly once.
// WHAT & WHY: The coin screen asks this service to buy a pack; it asks the
//   ICoinStore to take the money, then credits IWallet with the pack's coins
//   from CoinPackCatalog. Real money is involved, so the one rule that matters is
//   "every paid transaction gives its coins once — never zero times, never twice".
// KEY DECISIONS:
//   - A ledger of granted transaction ids lives in SaveData.grantedIapTransactions.
//     The id is added in memory FIRST and then IWallet.Add saves once, so the coins
//     and the ledger reach disk in the same write (the pattern PlayerWallet
//     documents for "commit other state together with coins").
//   - Reconcile(): when the store becomes ready (every launch), past one-time
//     transactions are fetched and any id not in the ledger is granted. That covers
//     an app killed between payment and callback. The same ledger check makes a
//     Reconcile racing a Buy callback harmless.
//   - The ledger survives SaveManager.ResetProgress, so wiping progress can never
//     be used to re-grant old purchases.
//   - A product id that is not in the catalog is never granted (logged instead):
//     the catalog decides what a product is worth, not the store.
//   - A Success without a transaction id is still granted (the player paid), but
//     logged; it cannot be de-duplicated against a later Reconcile.
//   - One purchase at a time: a second Buy while one is in flight reports Failed
//     immediately, so a double tap cannot open two store sheets.
//   - Plain C#, unit-tested in CoinPurchaseServiceTests.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. GameBootstrap creates it and registers it as ICoinPurchaseService.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using RestoriumEmporium.Core;

namespace RestoriumEmporium.Economy
{
    public interface ICoinPurchaseService
    {
        /// <summary>The packs on sale, in screen order.</summary>
        IReadOnlyList<CoinPack> Packs { get; }

        /// <summary>True when the store can sell right now.</summary>
        bool IsAvailable { get; }

        /// <summary>Raised when IsAvailable changes.</summary>
        event Action AvailabilityChanged;

        /// <summary>True between Buy() and its callback.</summary>
        bool IsPurchasing { get; }

        /// <summary>Raised after coins were credited: (productId, coins).</summary>
        event Action<string, int> CoinsGranted;

        /// <summary>Store prices by product id. Empty when the store has none (Editor, offline).</summary>
        void FetchPrices(Action<IReadOnlyDictionary<string, string>> done);

        /// <summary>Buys one pack. The callback fires exactly once, after any coins were credited.</summary>
        void Buy(string productId, Action<CoinPurchaseResult> done);
    }

    public sealed class CoinPurchaseService : ICoinPurchaseService
    {
        private readonly ICoinStore _store;
        private readonly IWallet _wallet;
        private readonly ISaveService _save;
        private readonly IReadOnlyList<CoinPack> _packs;
        private readonly Action<string> _warn;
        private readonly SaveData _fallback = new SaveData();
        private bool _reconciling;

        public event Action AvailabilityChanged;
        public event Action<string, int> CoinsGranted;

        public CoinPurchaseService(ICoinStore store, IWallet wallet, ISaveService save,
            IReadOnlyList<CoinPack> packs, Action<string> warn)
        {
            _store = store;
            _wallet = wallet;
            _save = save;
            _packs = packs ?? Array.Empty<CoinPack>();
            _warn = warn;

            if (_store == null)
            {
                Warn("No ICoinStore given. Coin packs cannot be bought.");
            }
            else
            {
                // Process-lifetime service on a process-lifetime store: no unsubscribe.
                _store.ReadyChanged += OnStoreReadyChanged;
            }

            if (_packs.Count == 0)
            {
                Warn("No coin packs configured (CoinPackCatalog missing or empty on GameBootstrap).");
            }

            if (IsAvailable)
            {
                Reconcile();
            }
        }

        public IReadOnlyList<CoinPack> Packs => _packs;

        public bool IsAvailable => _store != null && _store.IsReady && _wallet != null;

        public bool IsPurchasing { get; private set; }

        private SaveData Data
        {
            get
            {
                var data = _save?.Data ?? _fallback;
                data.grantedIapTransactions ??= new List<string>();
                return data;
            }
        }

        public void FetchPrices(Action<IReadOnlyDictionary<string, string>> done)
        {
            done ??= _ => { };
            var prices = new Dictionary<string, string>(StringComparer.Ordinal);

            if (!IsAvailable || _packs.Count == 0)
            {
                done(prices);
                return;
            }

            var ids = new List<string>(_packs.Count);

            foreach (var pack in _packs)
            {
                if (pack != null && !string.IsNullOrEmpty(pack.productId))
                {
                    ids.Add(pack.productId);
                }
            }

            _store.FetchProducts(ids, (products, error) =>
            {
                if (error != null)
                {
                    Warn($"Could not load prices: {error}");
                }

                if (products != null)
                {
                    foreach (var p in products)
                    {
                        if (!string.IsNullOrEmpty(p.ProductId) && !string.IsNullOrEmpty(p.PriceString))
                        {
                            prices[p.ProductId] = p.PriceString;
                        }
                    }
                }

                done(prices);
            });
        }

        public void Buy(string productId, Action<CoinPurchaseResult> done)
        {
            done ??= _ => { };

            if (FindPack(productId) == null)
            {
                done(CoinPurchaseResult.Failed(productId, $"'{productId}' is not in the coin pack catalog."));
                return;
            }

            if (!IsAvailable)
            {
                done(CoinPurchaseResult.Unavailable(productId, "The store is not ready."));
                return;
            }

            if (IsPurchasing)
            {
                done(CoinPurchaseResult.Failed(productId, "Another purchase is already in progress."));
                return;
            }

            IsPurchasing = true;

            _store.Purchase(productId, result =>
            {
                IsPurchasing = false;
                result ??= CoinPurchaseResult.Failed(productId, "The store returned no result.");

                if (result.Status == CoinPurchaseStatus.Success)
                {
                    Grant(result.TransactionId, string.IsNullOrEmpty(result.ProductId) ? productId : result.ProductId);
                }
                else if (result.Status != CoinPurchaseStatus.Cancelled)
                {
                    Warn($"Purchase did not complete: {result}");
                }

                done(result);
            });
        }

        /// <summary>Grants any past transaction whose coins were never credited.</summary>
        public void Reconcile()
        {
            if (!IsAvailable || _reconciling)
            {
                return;
            }

            _reconciling = true;

            _store.FetchPastTransactions((transactions, error) =>
            {
                _reconciling = false;

                if (error != null)
                {
                    Warn($"Could not check past purchases: {error}");
                    return;
                }

                if (transactions == null)
                {
                    return;
                }

                foreach (var tx in transactions)
                {
                    // Only ids we can de-duplicate, and only products we sell.
                    if (!string.IsNullOrEmpty(tx.TransactionId) && FindPack(tx.ProductId) != null &&
                        !Data.grantedIapTransactions.Contains(tx.TransactionId))
                    {
                        Warn($"Granting a purchase that was paid but never credited: {tx.ProductId} " +
                             $"(tx {tx.TransactionId}).");
                        Grant(tx.TransactionId, tx.ProductId);
                    }
                }
            });
        }

        /// <summary>True if the coins were credited by this call.</summary>
        private bool Grant(string transactionId, string productId)
        {
            var pack = FindPack(productId);

            if (pack == null)
            {
                Warn($"Paid product '{productId}' is not in the coin pack catalog; nothing granted " +
                     $"(tx {transactionId}). Add it to CoinPackCatalog.");
                return false;
            }

            if (_wallet == null)
            {
                Warn($"No IWallet; cannot credit {pack.coins} coins for '{productId}'.");
                return false;
            }

            if (string.IsNullOrEmpty(transactionId))
            {
                Warn($"Purchase of '{productId}' succeeded without a transaction id; granting anyway.");
            }
            else if (Data.grantedIapTransactions.Contains(transactionId))
            {
                return false; // already credited (Reconcile beat the callback, or a repeat)
            }
            else
            {
                // In memory first: IWallet.Add's single Save() writes both together.
                Data.grantedIapTransactions.Add(transactionId);
            }

            _wallet.Add(pack.coins, "iap:" + productId);
            CoinsGranted?.Invoke(productId, pack.coins);
            return true;
        }

        private CoinPack FindPack(string productId)
        {
            if (string.IsNullOrEmpty(productId))
            {
                return null;
            }

            for (var i = 0; i < _packs.Count; i++)
            {
                var pack = _packs[i];

                if (pack != null && pack.coins > 0 && string.Equals(pack.productId, productId, StringComparison.Ordinal))
                {
                    return pack;
                }
            }

            return null;
        }

        private void OnStoreReadyChanged()
        {
            AvailabilityChanged?.Invoke();

            if (IsAvailable)
            {
                Reconcile();
            }
        }

        private void Warn(string message)
        {
            _warn?.Invoke("[CoinPurchaseService] " + message);
        }
    }
}
