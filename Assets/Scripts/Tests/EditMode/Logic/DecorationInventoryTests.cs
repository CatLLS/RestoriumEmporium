// ============================================================
// DecorationInventoryTests — purchase atomicity and placement rules.
// WHAT & WHY: Buying a decoration moves coins and grants an item; the two must
//   happen together or not at all, and the desk-hub tutorial depends on the
//   lamp (100) being exactly affordable with poster 1's reward (100). These
//   tests pin that, plus the refusal reasons the shop shows.
// KEY DECISIONS:
//   - Pure NUnit, no UnityEngine. A real PlayerWallet runs on the same in-memory
//     FakeSave, and FakeSave snapshots what one Save() would write, so "one
//     write, both changes" is asserted directly.
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
    public class DecorationInventoryTests
    {
        private sealed class FakeSave : ISaveService
        {
            public SaveData Data { get; private set; } = new SaveData();
            public int SaveCount;
            public int DiskCoins;
            public int DiskDecorationCount;

            public event Action Reloaded;

            public void Save()
            {
                SaveCount++;
                DiskCoins = Data.coins;
                DiskDecorationCount = Data.decorations.Count;
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
        private DecorationInventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _save = new FakeSave();
            _warnings = new List<string>();
            _wallet = new PlayerWallet(_save, _warnings.Add);
            _inventory = new DecorationInventory(_save, _wallet, _warnings.Add);
        }

        private void GiveCoins(int amount)
        {
            _save.Data.coins = amount;
        }

        [Test]
        public void Purchase_Success_SpendsRecordsAndSavesOnce()
        {
            GiveCoins(100);
            var purchased = new List<string>();
            _inventory.ItemPurchased += purchased.Add;

            var result = _inventory.TryPurchase("lamp", 100, 0.3f, 0.6f);

            Assert.AreEqual(PurchaseResult.Success, result);
            Assert.AreEqual(0, _wallet.Coins);
            Assert.IsTrue(_inventory.Owns("lamp"));
            Assert.AreEqual(0.3f, _inventory.Get("lamp").x, 1e-6f);
            Assert.AreEqual(0.6f, _inventory.Get("lamp").y, 1e-6f);
            CollectionAssert.AreEqual(new[] { "lamp" }, purchased);

            Assert.AreEqual(1, _save.SaveCount, "Exactly one disk write per purchase.");
            Assert.AreEqual(0, _save.DiskCoins);
            Assert.AreEqual(1, _save.DiskDecorationCount, "Coins and item must land in the same write.");
        }

        [Test]
        public void Purchase_NotEnoughCoins_ChangesNothing()
        {
            GiveCoins(99);
            var raised = false;
            _inventory.ItemPurchased += _ => raised = true;

            var result = _inventory.TryPurchase("lamp", 100, 0.5f, 0.5f);

            Assert.AreEqual(PurchaseResult.NotEnoughCoins, result);
            Assert.AreEqual(99, _wallet.Coins);
            Assert.IsFalse(_inventory.Owns("lamp"));
            Assert.AreEqual(0, _inventory.Owned.Count);
            Assert.AreEqual(0, _save.SaveCount);
            Assert.IsFalse(raised);
        }

        [Test]
        public void Purchase_AlreadyOwned_IsRefusedAndNotCharged()
        {
            GiveCoins(300);
            _inventory.TryPurchase("lamp", 100, 0.5f, 0.5f);
            var saves = _save.SaveCount;

            var result = _inventory.TryPurchase("lamp", 100, 0.1f, 0.1f);

            Assert.AreEqual(PurchaseResult.AlreadyOwned, result);
            Assert.AreEqual(200, _wallet.Coins);
            Assert.AreEqual(1, _inventory.Owned.Count);
            Assert.AreEqual(0.5f, _inventory.Get("lamp").x, 1e-6f, "The old placement is kept.");
            Assert.AreEqual(saves, _save.SaveCount);
        }

        [Test]
        public void Purchase_AlreadyOwned_WinsOverNotEnoughCoins()
        {
            GiveCoins(100);
            _inventory.TryPurchase("lamp", 100, 0.5f, 0.5f);

            Assert.AreEqual(PurchaseResult.AlreadyOwned, _inventory.TryPurchase("lamp", 100, 0.5f, 0.5f));
        }

        [TestCase("", 10)]
        [TestCase(null, 10)]
        [TestCase("lamp", -1)]
        public void Purchase_Invalid_IsRefused(string id, int price)
        {
            GiveCoins(100);

            Assert.AreEqual(PurchaseResult.InvalidItem, _inventory.TryPurchase(id, price, 0.5f, 0.5f));
            Assert.AreEqual(100, _wallet.Coins);
            Assert.AreEqual(0, _inventory.Owned.Count);
        }

        [Test]
        public void Purchase_Free_SavesWithoutWallet()
        {
            var inventory = new DecorationInventory(_save, null, _warnings.Add);

            Assert.AreEqual(PurchaseResult.Success, inventory.TryPurchase("gift", 0, 0.5f, 0.5f));
            Assert.AreEqual(1, _save.SaveCount);
            Assert.AreEqual(1, _save.DiskDecorationCount);
        }

        [Test]
        public void Purchase_NoWallet_PaidItemIsRefused()
        {
            var inventory = new DecorationInventory(_save, null, _warnings.Add);
            GiveCoins(1000);

            Assert.AreEqual(PurchaseResult.NotEnoughCoins, inventory.TryPurchase("lamp", 100, 0.5f, 0.5f));
            Assert.IsFalse(inventory.Owns("lamp"));
        }

        [Test]
        public void Purchase_ClampsPositionIntoRoom()
        {
            GiveCoins(100);

            _inventory.TryPurchase("lamp", 100, -2f, float.NaN);

            Assert.AreEqual(0f, _inventory.Get("lamp").x);
            Assert.AreEqual(0.5f, _inventory.Get("lamp").y);
        }

        [Test]
        public void SetPlacement_Owned_MovesSavesAndRaises()
        {
            GiveCoins(100);
            _inventory.TryPurchase("lamp", 100, 0.5f, 0.5f);
            var saves = _save.SaveCount;
            var moved = new List<string>();
            _inventory.PlacementChanged += moved.Add;

            Assert.IsTrue(_inventory.SetPlacement("lamp", 0.2f, 1.7f));

            Assert.AreEqual(0.2f, _inventory.Get("lamp").x, 1e-6f);
            Assert.AreEqual(1f, _inventory.Get("lamp").y);
            Assert.AreEqual(saves + 1, _save.SaveCount);
            CollectionAssert.AreEqual(new[] { "lamp" }, moved);
        }

        [Test]
        public void SetPlacement_NotOwned_ReturnsFalse()
        {
            Assert.IsFalse(_inventory.SetPlacement("lamp", 0.2f, 0.2f));
            Assert.AreEqual(0, _save.SaveCount);
            Assert.IsNull(_inventory.Get("lamp"));
        }

        [Test]
        public void Poster1Reward_AffordsTheLamp()
        {
            // The desk-hub tutorial relies on 100 (poster reward) >= 100 (lamp).
            _wallet.Add(100, "poster_complete:poster01");

            Assert.AreEqual(PurchaseResult.Success, _inventory.TryPurchase("lamp", 100, 0.5f, 0.5f));
        }
    }
}
