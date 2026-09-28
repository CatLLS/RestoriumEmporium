// ============================================================
// DecorationInventory — owned desk-hub decorations and where they sit.
// WHAT & WHY: Implements IDecorationInventory on SaveData.decorations. The shop
//   (buy), the desk hub's preview/edit modes (place, move) and the room view
//   (draw) all share one list, and a purchase must be all-or-nothing: coins out
//   AND item in, or neither.
// KEY DECISIONS:
//   - Plain C# with no UnityEngine reference, so purchase atomicity and the
//     refusal cases are unit-tested (DecorationInventoryTests).
//   - ONE disk write per purchase. The item record is added to SaveData in
//     memory first, then IWallet.TrySpend is called — PlayerWallet writes the
//     same SaveData synchronously, so the coins and the item hit disk together.
//     If the spend is refused, the record is removed again and nothing was
//     written. A free item (price 0) skips the wallet and saves itself.
//     (If a future wallet no longer writes this SaveData — a server currency —
//     this class must then call Save() itself after a successful spend.)
//   - Checks run in a fixed order: invalid item -> already owned -> not enough
//     coins. "Already owned" wins over "not enough coins" so the shop shows the
//     truthful reason.
//   - Positions are normalised 0..1 room coordinates, clamped here; NaN (a view
//     dividing by a zero-size rect) becomes the room centre instead of poisoning
//     the save file.
//   - Reads ISaveService.Data on every call (ResetProgress swaps the object).
//   - Events fire AFTER the write, so a listener can never observe an item that
//     would be lost by a kill.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. GameBootstrap creates it (after the wallet) and registers it as
//     IDecorationInventory. Get it with ServiceLocator.Get<IDecorationInventory>()
//     in Start().
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using RestoriumEmporium.Core;

namespace RestoriumEmporium.Economy
{
    public sealed class DecorationInventory : IDecorationInventory
    {
        private readonly ISaveService _save;
        private readonly IWallet _wallet;
        private readonly Action<string> _warn;
        private readonly SaveData _fallback = new SaveData();

        /// <inheritdoc />
        public event Action<string> ItemPurchased;

        /// <inheritdoc />
        public event Action<string> PlacementChanged;

        public DecorationInventory(ISaveService save, IWallet wallet, Action<string> warn)
        {
            _save = save;
            _wallet = wallet;
            _warn = warn;

            if (_save == null)
            {
                Warn("No ISaveService given. Decorations will not be saved.");
            }

            if (_wallet == null)
            {
                Warn("No IWallet given. Only free items can be bought.");
            }
        }

        private SaveData Data
        {
            get
            {
                var data = _save?.Data ?? _fallback;
                data.EnsureCollections();
                return data;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<OwnedDecoration> Owned => Data.decorations;

        /// <inheritdoc />
        public bool Owns(string itemId) => Data.FindDecoration(itemId) != null;

        /// <inheritdoc />
        public OwnedDecoration Get(string itemId) => Data.FindDecoration(itemId);

        /// <inheritdoc />
        public PurchaseResult TryPurchase(string itemId, int price, float x, float y)
        {
            if (string.IsNullOrEmpty(itemId) || price < 0)
            {
                Warn($"TryPurchase('{itemId}', {price}): invalid item or price. Nothing bought.");
                return PurchaseResult.InvalidItem;
            }

            var data = Data;

            if (data.FindDecoration(itemId) != null)
            {
                return PurchaseResult.AlreadyOwned;
            }

            if (price > 0 && (_wallet == null || !_wallet.CanAfford(price)))
            {
                return PurchaseResult.NotEnoughCoins;
            }

            var record = new OwnedDecoration { itemId = itemId, x = Clamp01(x), y = Clamp01(y) };
            data.decorations.Add(record);

            if (price > 0)
            {
                // TrySpend's Save() writes the record above in the same file write.
                if (!_wallet.TrySpend(price, "decoration:" + itemId))
                {
                    data.decorations.Remove(record);
                    return PurchaseResult.NotEnoughCoins;
                }
            }
            else
            {
                _save?.Save();
            }

            ItemPurchased?.Invoke(itemId);
            return PurchaseResult.Success;
        }

        /// <inheritdoc />
        public bool SetPlacement(string itemId, float x, float y)
        {
            var record = Data.FindDecoration(itemId);

            if (record == null)
            {
                Warn($"SetPlacement('{itemId}'): item is not owned. Ignored.");
                return false;
            }

            record.x = Clamp01(x);
            record.y = Clamp01(y);
            _save?.Save();
            PlacementChanged?.Invoke(itemId);
            return true;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value))
            {
                return 0.5f;
            }

            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        private void Warn(string message)
        {
            _warn?.Invoke("[DecorationInventory] " + message);
        }
    }
}
