// ============================================================
// IDecorationInventory — which decorations the player owns and where they sit.
// WHAT & WHY: Buying in the shop, placing in preview mode, moving in edit mode
//   and drawing the room all read and write the same list. One owner keeps the
//   purchase atomic (coins out + item in, one save) and the rules testable.
// KEY DECISIONS:
//   - Items are referenced by itemId string (ShopItemData.itemId), never by asset
//     reference, so the implementation is plain C# and the save never holds a
//     Unity object.
//   - TryPurchase is the ONLY way to acquire an item: it checks ownership and
//     balance, spends through IWallet, records the placement, and saves. A failed
//     purchase changes nothing.
//   - Positions are normalised 0..1 room coordinates (see OwnedDecoration). The
//     view converts to/from pixels; this service never sees a RectTransform.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by DecorationInventory, created and registered in
//     ServiceLocator by GameBootstrap.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Economy
{
    using RestoriumEmporium.Core;

    public enum PurchaseResult
    {
        Success = 0,
        AlreadyOwned = 1,
        NotEnoughCoins = 2,
        InvalidItem = 3
    }

    public interface IDecorationInventory
    {
        /// <summary>Raised with the itemId after a successful purchase.</summary>
        event Action<string> ItemPurchased;

        /// <summary>Raised with the itemId after its placement changes.</summary>
        event Action<string> PlacementChanged;

        IReadOnlyList<OwnedDecoration> Owned { get; }

        bool Owns(string itemId);

        /// <summary>Spends <paramref name="price"/> via IWallet and records the item at (x, y). Saves.</summary>
        PurchaseResult TryPurchase(string itemId, int price, float x, float y);

        /// <summary>Moves an owned item. False when not owned. Saves.</summary>
        bool SetPlacement(string itemId, float x, float y);

        /// <summary>The owned record, or null.</summary>
        OwnedDecoration Get(string itemId);
    }
}
