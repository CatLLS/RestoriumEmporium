// ============================================================
// CoinPurchaseServiceTests — the paid-coin rules of CoinPurchaseService and
// the key rules of StoreKeyPolicy.
// WHAT & WHY: Real money. These tests pin: a paid pack credits its coins exactly
//   once per transaction; cancel / fail / unknown products credit nothing; a
//   purchase that was paid but never credited is recovered by Reconcile, and
//   never twice; the ledger survives a progress reset; a secret key or a Test
//   Store key in a release build is refused.
// KEY DECISIONS:
//   - Pure NUnit with a FakeStore (manual callbacks, so the test decides when the
//     "store sheet" answers) and the same kind of in-memory FakeSave as WalletTests.
//   - The real PlayerWallet is used, so the tests also prove the ledger and the
//     coins reach "disk" in the same save.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Window -> General -> Test Runner -> EditMode -> Run All.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RestoriumEmporium.Core;
using RestoriumEmporium.Economy;

namespace RestoriumEmporium.Tests
{
    public class CoinPurchaseServiceTests
    {
        private sealed class FakeSave : ISaveService
        {
            public SaveData Data { get; private set; } = new SaveData();
            public int SaveCount;
            public List<string> DiskLedger = new List<string>();
            public int DiskCoins;

            public event Action Reloaded;

            public void Save()
            {
                SaveCount++;
                DiskCoins = Data.coins;
                DiskLedger = new List<string>(Data.grantedIapTransactions);
            }

            public void SaveSoon() { }
            public void Load() => Reloaded?.Invoke();

            public void ResetProgress()
            {
                // Mirrors SaveManager: progress goes, the purchase ledger stays.
                Data = new SaveData { grantedIapTransactions = Data.grantedIapTransactions };
                Reloaded?.Invoke();
            }
        }

        private sealed class FakeStore : ICoinStore
        {
            public bool Ready = true;
            public Action<CoinPurchaseResult> PendingPurchase;
            public int PurchaseCalls;
            public List<CoinTransaction> History = new List<CoinTransaction>();

            public bool IsReady => Ready;
            public event Action ReadyChanged;

            public void SetReady(bool ready)
            {
                Ready = ready;
                ReadyChanged?.Invoke();
            }

            public void FetchProducts(IReadOnlyList<string> productIds,
                Action<IReadOnlyList<CoinStoreProduct>, string> done)
            {
                done(new[] { new CoinStoreProduct("coins_300", "R$ 5,90") }, null);
            }

            public void Purchase(string productId, Action<CoinPurchaseResult> done)
            {
                PurchaseCalls++;
                PendingPurchase = done;
            }

            public void FetchPastTransactions(Action<IReadOnlyList<CoinTransaction>, string> done)
            {
                done(History, null);
            }

            public void Answer(CoinPurchaseResult result)
            {
                var cb = PendingPurchase;
                PendingPurchase = null;
                cb(result);
            }
        }

        private static readonly CoinPack[] Packs =
        {
            new CoinPack { productId = "coins_300", coins = 300, fallbackPriceLabel = "$ 0.99" },
            new CoinPack { productId = "coins_900", coins = 900, fallbackPriceLabel = "$ 1.99" },
        };

        private FakeSave _save;
        private FakeStore _store;
        private PlayerWallet _wallet;
        private List<string> _warnings;

        [SetUp]
        public void SetUp()
        {
            _save = new FakeSave();
            _store = new FakeStore();
            _warnings = new List<string>();
            _wallet = new PlayerWallet(_save, _warnings.Add);
        }

        private CoinPurchaseService NewService() =>
            new CoinPurchaseService(_store, _wallet, _save, Packs, _warnings.Add);

        [Test]
        public void Buy_Success_CreditsPackCoinsOnce_AndSavesLedgerWithCoins()
        {
            var service = NewService();
            CoinPurchaseResult got = null;

            service.Buy("coins_300", r => got = r);
            Assert.IsTrue(service.IsPurchasing);
            _store.Answer(CoinPurchaseResult.Success("coins_300", "tx1"));

            Assert.AreEqual(CoinPurchaseStatus.Success, got.Status);
            Assert.IsFalse(service.IsPurchasing);
            Assert.AreEqual(300, _wallet.Coins);
            Assert.AreEqual(300, _save.DiskCoins);
            CollectionAssert.AreEqual(new[] { "tx1" }, _save.DiskLedger);
        }

        [Test]
        public void Buy_SameTransactionTwice_CreditsOnce()
        {
            var service = NewService();

            service.Buy("coins_300", null);
            _store.Answer(CoinPurchaseResult.Success("coins_300", "tx1"));
            service.Buy("coins_300", null);
            _store.Answer(CoinPurchaseResult.Success("coins_300", "tx1"));

            Assert.AreEqual(300, _wallet.Coins);
        }

        [Test]
        public void Buy_CreditsTheProductTheStoreReports()
        {
            var service = NewService();

            service.Buy("coins_300", null);
            _store.Answer(CoinPurchaseResult.Success("coins_900", "tx1"));

            Assert.AreEqual(900, _wallet.Coins);
        }

        [TestCase(CoinPurchaseStatus.Cancelled)]
        [TestCase(CoinPurchaseStatus.Failed)]
        [TestCase(CoinPurchaseStatus.Unavailable)]
        public void Buy_NotSuccessful_CreditsNothing(CoinPurchaseStatus status)
        {
            var service = NewService();
            CoinPurchaseResult got = null;

            service.Buy("coins_300", r => got = r);
            _store.Answer(status switch
            {
                CoinPurchaseStatus.Cancelled => CoinPurchaseResult.Cancelled("coins_300"),
                CoinPurchaseStatus.Failed => CoinPurchaseResult.Failed("coins_300", "x"),
                _ => CoinPurchaseResult.Unavailable("coins_300", "x"),
            });

            Assert.AreEqual(status, got.Status);
            Assert.AreEqual(0, _wallet.Coins);
            Assert.IsFalse(service.IsPurchasing);
        }

        [Test]
        public void Buy_UnknownProduct_NeverReachesTheStore()
        {
            var service = NewService();
            CoinPurchaseResult got = null;

            service.Buy("coins_999999", r => got = r);

            Assert.AreEqual(CoinPurchaseStatus.Failed, got.Status);
            Assert.AreEqual(0, _store.PurchaseCalls);
        }

        [Test]
        public void Buy_WhileAnotherIsInFlight_IsRefused()
        {
            var service = NewService();
            CoinPurchaseResult second = null;

            service.Buy("coins_300", null);
            service.Buy("coins_900", r => second = r);

            Assert.AreEqual(CoinPurchaseStatus.Failed, second.Status);
            Assert.AreEqual(1, _store.PurchaseCalls);
        }

        [Test]
        public void Buy_StoreNotReady_IsUnavailable()
        {
            _store.Ready = false;
            var service = NewService();
            CoinPurchaseResult got = null;

            service.Buy("coins_300", r => got = r);

            Assert.AreEqual(CoinPurchaseStatus.Unavailable, got.Status);
            Assert.AreEqual(0, _store.PurchaseCalls);
        }

        [Test]
        public void Reconcile_GrantsOnlyUncreditedKnownTransactions()
        {
            _save.Data.grantedIapTransactions.Add("old");
            _store.History.Add(new CoinTransaction("old", "coins_300"));        // already credited
            _store.History.Add(new CoinTransaction("lost", "coins_900"));       // paid, never credited
            _store.History.Add(new CoinTransaction("other", "remove_ads"));     // not a coin pack
            _store.History.Add(new CoinTransaction(null, "coins_300"));         // no id: cannot de-dupe

            NewService(); // store already ready -> reconciles in the constructor

            Assert.AreEqual(900, _wallet.Coins);
            CollectionAssert.AreEquivalent(new[] { "old", "lost" }, _save.Data.grantedIapTransactions);
        }

        [Test]
        public void Reconcile_RunsWhenStoreBecomesReady_AndNeverTwice()
        {
            _store.Ready = false;
            _store.History.Add(new CoinTransaction("lost", "coins_300"));
            var service = NewService();
            Assert.AreEqual(0, _wallet.Coins);

            _store.SetReady(true);
            service.Reconcile();

            Assert.AreEqual(300, _wallet.Coins);
        }

        [Test]
        public void Reconcile_ThenLateCallbackForSameTransaction_CreditsOnce()
        {
            var service = NewService();
            service.Buy("coins_300", null);

            // The history already shows the purchase before the callback arrives.
            _store.History.Add(new CoinTransaction("tx1", "coins_300"));
            service.Reconcile();
            _store.Answer(CoinPurchaseResult.Success("coins_300", "tx1"));

            Assert.AreEqual(300, _wallet.Coins);
        }

        [Test]
        public void Ledger_SurvivesResetProgress_SoOldPurchasesAreNotRegranted()
        {
            var service = NewService();
            service.Buy("coins_300", null);
            _store.Answer(CoinPurchaseResult.Success("coins_300", "tx1"));

            _save.ResetProgress();
            _store.History.Add(new CoinTransaction("tx1", "coins_300"));
            service.Reconcile();

            Assert.AreEqual(0, _wallet.Coins);
        }

        [Test]
        public void FetchPrices_ReturnsStorePricesById()
        {
            IReadOnlyDictionary<string, string> prices = null;

            NewService().FetchPrices(p => prices = p);

            Assert.AreEqual("R$ 5,90", prices["coins_300"]);
            Assert.IsFalse(prices.ContainsKey("coins_900"));
        }

        // ---- StoreKeyPolicy ----

        [Test]
        public void KeyPolicy_DebugBuild_PrefersTestStoreKey()
        {
            Assert.IsTrue(StoreKeyPolicy.TryChoose("test_abc", "goog_xyz", true, out var key, out _));
            Assert.AreEqual("test_abc", key);
        }

        [Test]
        public void KeyPolicy_ReleaseBuild_UsesPlatformKey()
        {
            Assert.IsTrue(StoreKeyPolicy.TryChoose("test_abc", "goog_xyz", false, out var key, out _));
            Assert.AreEqual("goog_xyz", key);
        }

        [Test]
        public void KeyPolicy_ReleaseBuild_RefusesTestStoreKey()
        {
            Assert.IsFalse(StoreKeyPolicy.TryChoose("test_abc", "", false, out var key, out var problem));
            Assert.IsNull(key);
            Assert.IsNotEmpty(problem);
            Assert.IsFalse(StoreKeyPolicy.TryChoose("", "test_abc", false, out _, out _));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void KeyPolicy_RefusesSecretKeys(bool debugBuild)
        {
            Assert.IsFalse(StoreKeyPolicy.TryChoose("sk_secret", "sk_secret", debugBuild, out var key, out var problem));
            Assert.IsNull(key);
            StringAssert.Contains("SECRET", problem);
        }

        [Test]
        public void KeyPolicy_NoKey_IsRefused()
        {
            Assert.IsFalse(StoreKeyPolicy.TryChoose(" ", null, true, out _, out var problem));
            Assert.IsNotEmpty(problem);
        }
    }
}
