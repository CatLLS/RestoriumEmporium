// ============================================================
// CoinPackCatalog — the coin packs sold in The Golden Vault (Figma BuyCoins 623:26).
// WHAT & WHY: How many coins a purchase is worth is decided HERE, by product id,
//   not by the store. The store only says "product X was paid for"; the price
//   the player sees comes from the store (localized), and this list says what
//   that product grants. Keeping the amount client-side means no server is
//   needed for the jam build.
// KEY DECISIONS:
//   - Product ids must match the products created in the RevenueCat dashboard
//     (Test Store now, Google Play / App Store later) character for character.
//   - fallbackPriceLabel is shown until the store answers, and forever in the
//     Editor (the RevenueCat SDK does not run there). It is the Figma copy.
//   - Order in the list = order of the rows on screen.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] The asset is Assets/Data/Catalogs/CoinPackCatalog.asset. To change a pack,
//     select it and edit the list. Create a new one with right-click ->
//     Create -> Restorium -> Coin Pack Catalog.
// [ ] It must be dragged into GameBootstrap -> "Coin Pack Catalog" on the
//     Systems prefab (Restorium/Store/Add RevenueCat to Systems prefab does it).
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Economy
{
    [CreateAssetMenu(fileName = "CoinPackCatalog", menuName = "Restorium/Coin Pack Catalog")]
    public class CoinPackCatalog : ScriptableObject
    {
        [SerializeField] private List<CoinPack> packs = new List<CoinPack>();

        public IReadOnlyList<CoinPack> Packs => packs;

        private void OnValidate()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var pack in packs)
            {
                if (pack == null || string.IsNullOrEmpty(pack.productId))
                {
                    Debug.LogWarning($"[CoinPackCatalog] '{name}' has a pack with no product id.", this);
                }
                else if (!seen.Add(pack.productId))
                {
                    Debug.LogWarning($"[CoinPackCatalog] '{name}' lists '{pack.productId}' twice.", this);
                }
                else if (pack.coins <= 0)
                {
                    Debug.LogWarning($"[CoinPackCatalog] '{pack.productId}' grants {pack.coins} coins.", this);
                }
            }
        }
    }

    [Serializable]
    public class CoinPack
    {
        [Tooltip("Store product id, e.g. coins_300. Must match the RevenueCat dashboard exactly.")]
        public string productId = string.Empty;

        [Tooltip("Coins granted once per successful purchase.")]
        public int coins;

        [Tooltip("Price shown until the store answers (and always in the Editor).")]
        public string fallbackPriceLabel = string.Empty;
    }
}
