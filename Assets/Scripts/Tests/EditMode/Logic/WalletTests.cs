// ============================================================
// WalletTests — the coin balance rules of PlayerWallet.
// WHAT & WHY: Coins are the one piece of state players notice instantly when
//   it goes wrong. These tests pin: positive-only deltas, refusal when short,
//   save-before-notify, saturation instead of overflow, and reset handling.
// KEY DECISIONS:
//   - Pure NUnit, no UnityEngine; an in-memory FakeSave counts writes and
//     records whether the balance was already on "disk" when the event fired.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Window -> General -> Test Runner, pick the "EditMode" tab and click
//     "Run All". Nothing else to set up.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RestoriumEmporium.Core;
using RestoriumEmporium.Economy;

namespace RestoriumEmporium.Tests
{
    public class WalletTests
    {
        private sealed class FakeSave : ISaveService
        {
            public SaveData Data { get; private set; } = new SaveData();
            public int SaveCount;
            public int DiskCoins;

            public event Action Reloaded;

            public void Save()
            {
                SaveCount++;
                DiskCoins = Data.coins;
            }

            public void SaveSoon() { }
            public void Load() => Reloaded?.Invoke();

            public void ResetProgress()
            {
                Data = new SaveData();
                Reloaded?.Invoke();
            }
        }

        private FakeSave _save;
        private List<string> _warnings;
        private PlayerWallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _save = new FakeSave();
            _warnings = new List<string>();
            _wallet = new PlayerWallet(_save, _warnings.Add);
        }

        [Test]
        public void Add_IncreasesBalance_SavesAndRaises()
        {
            var events = new List<(int balance, int delta)>();
            _wallet.CoinsChanged += (b, d) => events.Add((b, d));

            _wallet.Add(100, "test");

            Assert.AreEqual(100, _wallet.Coins);
            Assert.AreEqual(1, _save.SaveCount);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual((100, 100), events[0]);
        }

        [Test]
        public void Add_SavesBeforeNotifying()
        {
            var diskAtEvent = -1;
            _wallet.CoinsChanged += (b, d) => diskAtEvent = _save.DiskCoins;

            _wallet.Add(40, "test");

            Assert.AreEqual(40, diskAtEvent);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Add_NonPositive_IsIgnoredWithWarning(int amount)
        {
            _wallet.Add(amount, "test");

            Assert.AreEqual(0, _wallet.Coins);
            Assert.AreEqual(0, _save.SaveCount);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void TrySpend_WithEnough_Deducts()
        {
            _wallet.Add(100, "test");
            var events = new List<int>();
            _wallet.CoinsChanged += (b, d) => events.Add(d);

            Assert.IsTrue(_wallet.TrySpend(60, "test"));

            Assert.AreEqual(40, _wallet.Coins);
            Assert.AreEqual(2, _save.SaveCount);
            CollectionAssert.AreEqual(new[] { -60 }, events);
        }

        [Test]
        public void TrySpend_ExactBalance_Succeeds()
        {
            _wallet.Add(100, "test");

            Assert.IsTrue(_wallet.TrySpend(100, "lamp"));
            Assert.AreEqual(0, _wallet.Coins);
        }

        [Test]
        public void TrySpend_Short_ChangesNothing()
        {
            _wallet.Add(50, "test");
            var saves = _save.SaveCount;
            var raised = false;
            _wallet.CoinsChanged += (b, d) => raised = true;

            Assert.IsFalse(_wallet.TrySpend(51, "test"));

            Assert.AreEqual(50, _wallet.Coins);
            Assert.AreEqual(saves, _save.SaveCount);
            Assert.IsFalse(raised);
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void TrySpend_NonPositive_IsRefused(int amount)
        {
            _wallet.Add(50, "test");

            Assert.IsFalse(_wallet.TrySpend(amount, "test"));
            Assert.AreEqual(50, _wallet.Coins);
        }

        [Test]
        public void CanAfford_MatchesBalance()
        {
            _wallet.Add(100, "test");

            Assert.IsTrue(_wallet.CanAfford(0));
            Assert.IsTrue(_wallet.CanAfford(100));
            Assert.IsFalse(_wallet.CanAfford(101));
            Assert.IsFalse(_wallet.CanAfford(-1));
        }

        [Test]
        public void Add_SaturatesInsteadOfOverflowing()
        {
            _save.Data.coins = int.MaxValue - 10;

            _wallet.Add(100, "test");

            Assert.AreEqual(int.MaxValue, _wallet.Coins);
        }

        [Test]
        public void NegativeSavedBalance_IsClampedToZero()
        {
            _save.Data.coins = -30;

            Assert.AreEqual(0, _wallet.Coins);
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void ResetProgress_RaisesRefreshWithZeroDelta()
        {
            _wallet.Add(70, "test");
            var events = new List<(int balance, int delta)>();
            _wallet.CoinsChanged += (b, d) => events.Add((b, d));

            _save.ResetProgress();

            Assert.AreEqual(0, _wallet.Coins);
            CollectionAssert.AreEqual(new[] { (0, 0) }, events);
        }
    }
}
