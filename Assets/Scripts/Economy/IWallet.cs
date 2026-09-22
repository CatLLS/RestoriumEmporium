// ============================================================
// IWallet — the player's coin balance.
// WHAT & WHY: Coins are earned from restorations and spent in the shop. The
//   HUD labels, the shop, the finished-repair screen and (later) the IAP coin
//   packs all touch the same balance, so it sits behind one small interface.
// KEY DECISIONS:
//   - Balance lives in SaveData.coins and every change is saved immediately:
//     coins are exactly the kind of state §4 says must survive an app kill.
//   - Amounts must be positive; TrySpend refuses (and changes nothing) when the
//     balance is short. There is no "set balance" — every change is a delta with
//     a reason string, which is what makes a future server/RevenueCat
//     Virtual Currency backend a drop-in replacement.
//   - Plain C# implementation (PlayerWallet) so it is unit-testable.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by PlayerWallet, created and registered in
//     ServiceLocator by GameBootstrap.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Economy
{
    public interface IWallet
    {
        int Coins { get; }

        /// <summary>Raised after every change: (new balance, signed delta).</summary>
        event Action<int, int> CoinsChanged;

        bool CanAfford(int amount);

        /// <summary>Adds a positive amount and saves. Non-positive amounts are ignored with a warning.</summary>
        void Add(int amount, string reason);

        /// <summary>Removes a positive amount and saves. False (no change) when short.</summary>
        bool TrySpend(int amount, string reason);
    }
}
