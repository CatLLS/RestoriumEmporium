// ============================================================
// RevenueCatCoinStore — ICoinStore backed by the RevenueCat Unity SDK.
// WHAT & WHY: The one place that talks to RevenueCat. It configures the SDK with
//   the key RevenueCatConfig allows for this build, reads the coin packs' prices
//   from the "coins" offering, runs purchases and lists past one-time
//   transactions. It never touches coins; CoinPurchaseService does that.
// KEY DECISIONS:
//   - Its own assembly (RestoriumEmporium.RevenueCat) that references both the
//     game runtime and the SDK assembly (revenuecat.purchases-unity). The game
//     never references the SDK directly, so the Editor and tests don't need it.
//   - Purchases (the SDK component) installs its native wrapper in its own
//     Start(). Configure() before that would hit a null wrapper, so this waits
//     one frame. "Use Runtime Setup" must be ticked on Purchases, or it would
//     configure itself from its own Inspector key fields, bypassing
//     StoreKeyPolicy.
//   - Nothing happens in the Editor: the SDK runs a no-op wrapper there, and
//     GameBootstrap uses SimulatedCoinStore instead.
//   - Packs are bought as Packages of the configured offering (falling back to
//     the Current offering), which is RevenueCat's recommended path. If a
//     product is missing from the offering, it falls back to PurchaseProduct
//     with type "inapp" (one-time, consumable).
//   - The transaction id comes from PurchaseResult.StoreTransaction. If that is
//     missing, it uses the newest matching NonSubscriptionTransaction in the
//     returned CustomerInfo, so the purchase ledger can still de-duplicate it.
//   - Debug-level SDK logs only in Development builds.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Run Restorium -> Store -> Add RevenueCat to Systems prefab. It adds the
//     "Purchases" component (Use Runtime Setup ticked) and this one to the
//     Systems prefab, with Config = Assets/Data/Config/RevenueCatConfig.asset.
// [ ] Paste the public Test Store key into that config asset (Docs/RevenueCat.md).
// [ ] Leave the key fields on the Purchases component itself EMPTY; this script
//     supplies the key at runtime.
// ---------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Economy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Purchases))]
    public class RevenueCatCoinStore : MonoBehaviour, ICoinStore
    {
        [Tooltip("Assets/Data/Config/RevenueCatConfig.asset: public SDK keys and the offering id.")]
        [SerializeField] private RevenueCatConfig config;

        private readonly Dictionary<string, Purchases.Package> _packages =
            new Dictionary<string, Purchases.Package>(StringComparer.Ordinal);

        private Purchases _purchases;
        private bool _ready;

        public bool IsReady => _ready;

        public event Action ReadyChanged;

        private IEnumerator Start()
        {
            if (Application.isEditor)
            {
                yield break; // the SDK cannot run here; GameBootstrap simulated instead
            }

            // Purchases.Start() installs the native wrapper; configure after it.
            yield return null;

            _purchases = GetComponent<Purchases>();

            if (config == null)
            {
                Debug.LogError("[RevenueCatCoinStore] 'Config' is empty. Run Restorium -> Store -> " +
                               "Add RevenueCat to Systems prefab.", this);
                yield break;
            }

            if (!_purchases.useRuntimeSetup)
            {
                Debug.LogError("[RevenueCatCoinStore] 'Use Runtime Setup' is not ticked on the Purchases " +
                               "component, so the SDK configured itself from its own fields. Tick it.", this);
                yield break;
            }

            if (!config.TryChooseKey(out var apiKey, out var problem))
            {
                Debug.LogError("[RevenueCatCoinStore] Store disabled: " + problem, this);
                yield break;
            }

            _purchases.Configure(Purchases.PurchasesConfiguration.Builder.Init(apiKey).Build());
            _purchases.SetLogLevel(Debug.isDebugBuild ? Purchases.LogLevel.Debug : Purchases.LogLevel.Warn);

            Debug.Log($"[RevenueCatCoinStore] Configured with a " +
                      $"{(StoreKeyPolicy.IsTestStoreKey(apiKey) ? "Test Store" : "platform")} key.", this);

            _ready = true;
            ReadyChanged?.Invoke();
        }

        public void FetchProducts(IReadOnlyList<string> productIds,
            Action<IReadOnlyList<CoinStoreProduct>, string> done)
        {
            done ??= (_, _) => { };

            if (!_ready)
            {
                done(Array.Empty<CoinStoreProduct>(), "RevenueCat is not configured.");
                return;
            }

            LoadPackages(error =>
            {
                var list = new List<CoinStoreProduct>();

                if (productIds != null)
                {
                    foreach (var id in productIds)
                    {
                        if (id != null && _packages.TryGetValue(id, out var package))
                        {
                            list.Add(new CoinStoreProduct(id, package.StoreProduct.PriceString));
                        }
                    }
                }

                done(list, error);
            });
        }

        public void Purchase(string productId, Action<CoinPurchaseResult> done)
        {
            done ??= _ => { };

            if (!_ready)
            {
                done(CoinPurchaseResult.Unavailable(productId, "RevenueCat is not configured."));
                return;
            }

            if (_packages.TryGetValue(productId, out var cached))
            {
                _purchases.PurchasePackage(cached, r => done(ToResult(productId, r)));
                return;
            }

            LoadPackages(_ =>
            {
                if (_packages.TryGetValue(productId, out var package))
                {
                    _purchases.PurchasePackage(package, r => done(ToResult(productId, r)));
                }
                else
                {
                    Debug.LogWarning($"[RevenueCatCoinStore] '{productId}' is not in offering " +
                                     $"'{config.OfferingId}'; buying the product directly.", this);
                    _purchases.PurchaseProduct(productId, r => done(ToResult(productId, r)), "inapp");
                }
            });
        }

        public void FetchPastTransactions(Action<IReadOnlyList<CoinTransaction>, string> done)
        {
            done ??= (_, _) => { };

            if (!_ready)
            {
                done(Array.Empty<CoinTransaction>(), "RevenueCat is not configured.");
                return;
            }

            _purchases.GetCustomerInfo((info, error) =>
            {
                if (error != null)
                {
                    done(Array.Empty<CoinTransaction>(), Describe(error));
                    return;
                }

                var list = new List<CoinTransaction>();

                if (info?.NonSubscriptionTransactions != null)
                {
                    foreach (var tx in info.NonSubscriptionTransactions)
                    {
                        if (tx != null)
                        {
                            list.Add(new CoinTransaction(tx.TransactionIdentifier, tx.ProductIdentifier));
                        }
                    }
                }

                done(list, null);
            });
        }

        /// <summary>Fills _packages from the configured offering. done(error or null).</summary>
        private void LoadPackages(Action<string> done)
        {
            _purchases.GetOfferings((offerings, error) =>
            {
                if (error != null)
                {
                    done(Describe(error));
                    return;
                }

                Purchases.Offering offering = null;

                if (offerings != null)
                {
                    if (offerings.All == null ||
                        !offerings.All.TryGetValue(config.OfferingId ?? string.Empty, out offering))
                    {
                        offering = offerings.Current;
                    }
                }

                if (offering?.AvailablePackages == null)
                {
                    done($"No offering '{config.OfferingId}' and no Current offering in RevenueCat.");
                    return;
                }

                foreach (var package in offering.AvailablePackages)
                {
                    var id = package?.StoreProduct?.Identifier;

                    if (!string.IsNullOrEmpty(id))
                    {
                        _packages[id] = package;
                    }
                }

                done(null);
            });
        }

        private static CoinPurchaseResult ToResult(string productId, Purchases.PurchaseResult result)
        {
            if (result == null)
            {
                return CoinPurchaseResult.Failed(productId, "RevenueCat returned no result.");
            }

            if (result.UserCancelled)
            {
                return CoinPurchaseResult.Cancelled(productId);
            }

            if (result.Error != null)
            {
                return CoinPurchaseResult.Failed(productId, Describe(result.Error));
            }

            var boughtId = string.IsNullOrEmpty(result.ProductIdentifier) ? productId : result.ProductIdentifier;
            var txId = result.StoreTransaction?.TransactionIdentifier;

            if (string.IsNullOrEmpty(txId))
            {
                txId = NewestTransactionId(result.CustomerInfo, boughtId);
            }

            return CoinPurchaseResult.Success(boughtId, txId);
        }

        private static string NewestTransactionId(Purchases.CustomerInfo info, string productId)
        {
            Purchases.StoreTransaction newest = null;

            if (info?.NonSubscriptionTransactions != null)
            {
                foreach (var tx in info.NonSubscriptionTransactions)
                {
                    if (tx != null && tx.ProductIdentifier == productId &&
                        (newest == null || tx.PurchaseDate > newest.PurchaseDate))
                    {
                        newest = tx;
                    }
                }
            }

            return newest?.TransactionIdentifier;
        }

        private static string Describe(Purchases.Error error) =>
            $"{error.ReadableErrorCode} ({error.Code}): {error.Message} {error.UnderlyingErrorMessage}".Trim();
    }
}
