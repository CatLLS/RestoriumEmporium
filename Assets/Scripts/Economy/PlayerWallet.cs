// ============================================================
// PlayerWallet — the coin balance, stored in SaveData.coins.
// WHAT & WHY: Implements IWallet. Restorations pay coins, the shop spends them,
//   and every coin label in the game listens to CoinsChanged. §4 of the build
//   spec: coins must survive an app kill at any instant, so every change is
//   written to disk synchronously before anyone is told about it.
// KEY DECISIONS:
//   - Plain C# with no UnityEngine reference, so the rules are unit-tested
//     (WalletTests). GameBootstrap passes Debug.LogWarning as 'warn'.
//   - Reads ISaveService.Data on every call rather than caching the SaveData,
//     because ResetProgress()/Load() swap that object. On ISaveService.Reloaded
//     it raises CoinsChanged(balance, 0) so labels refresh after a reset.
//   - Every change is a positive delta with a reason string; there is no "set
//     balance". That shape is what a later server-side currency (RevenueCat
//     Virtual Currency) needs, and the reason is logged-friendly for support.
//   - Save() happens BEFORE CoinsChanged fires, so a listener that crashes can
//     never leave the coins changed in memory but not on disk.
//   - Additions saturate at int.MaxValue instead of overflowing negative.
//   - A negative balance read from a hand-edited file is clamped to 0 (with a
//     warning) the first time it is noticed, rather than letting the shop think
//     the player owes money.
//   - Callers that must commit OTHER state in the same write (reward flags,
//     a purchased item) change that state in memory first and then call Add /
//     TrySpend: both touch the same SaveData, so this class's single Save()
//     writes everything together.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. GameBootstrap creates it and registers it as IWallet. Get it with
//     ServiceLocator.Get<IWallet>() in Start() and subscribe to CoinsChanged in
//     OnEnable / unsubscribe in OnDisable.
// ---------------------------------------------------------------

using System;
using RestoriumEmporium.Core;

namespace RestoriumEmporium.Economy
{
    public sealed class PlayerWallet : IWallet
    {
        private readonly ISaveService _save;
        private readonly Action<string> _warn;
        private readonly SaveData _fallback = new SaveData();

        /// <inheritdoc />
        public event Action<int, int> CoinsChanged;

        public PlayerWallet(ISaveService save, Action<string> warn)
        {
            _save = save;
            _warn = warn;

            if (_save == null)
            {
                Warn("No ISaveService given. Coins will not be saved.");
            }
            else
            {
                // Process-lifetime service on a process-lifetime save manager: the
                // subscription lives exactly as long as both, so no unsubscribe.
                _save.Reloaded += OnSaveReloaded;
            }
        }

        private SaveData Data
        {
            get
            {
                var data = _save?.Data ?? _fallback;

                if (data.coins < 0)
                {
                    Warn($"Saved balance was negative ({data.coins}). Resetting it to 0.");
                    data.coins = 0;
                }

                return data;
            }
        }

        /// <inheritdoc />
        public int Coins => Data.coins;

        /// <inheritdoc />
        public bool CanAfford(int amount)
        {
            return amount >= 0 && Data.coins >= amount;
        }

        /// <inheritdoc />
        public void Add(int amount, string reason)
        {
            if (amount <= 0)
            {
                Warn($"Add({amount}, '{reason}'): amounts must be positive. Ignored.");
                return;
            }

            var data = Data;
            var sum = (long)data.coins + amount;
            var next = sum > int.MaxValue ? int.MaxValue : (int)sum;
            var delta = next - data.coins;

            data.coins = next;
            _save?.Save();
            CoinsChanged?.Invoke(next, delta);
        }

        /// <inheritdoc />
        public bool TrySpend(int amount, string reason)
        {
            if (amount <= 0)
            {
                Warn($"TrySpend({amount}, '{reason}'): amounts must be positive. Nothing spent.");
                return false;
            }

            var data = Data;

            if (data.coins < amount)
            {
                return false;
            }

            data.coins -= amount;
            _save?.Save();
            CoinsChanged?.Invoke(data.coins, -amount);
            return true;
        }

        private void OnSaveReloaded()
        {
            CoinsChanged?.Invoke(Coins, 0);
        }

        private void Warn(string message)
        {
            _warn?.Invoke("[PlayerWallet] " + message);
        }
    }
}
