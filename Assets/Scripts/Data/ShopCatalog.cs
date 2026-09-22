// ============================================================
// ShopCatalog — every ShopItemData in the game, in one list.
// WHAT & WHY: The shop grid, the desk hub room and the tutorial all need to
//   look items up by id. A single catalogue asset means runtime code never
//   searches the project, and Addressables/asset bundles could replace it later.
// KEY DECISIONS:
//   - The list is filled by the EDITOR, not by hand: "Restorium > Rebuild
//     Catalogs & Locale Tables" (and an asset postprocessor on every import)
//     collects every ShopItemData under Assets/Data/ShopItems/ sorted by
//     displayOrder. Adding an item is therefore "create the asset", nothing else.
//   - Lookups build a dictionary lazily on first use; the list is tiny, but the
//     lookup runs every time the room redraws.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand. The asset lives at Assets/Data/Catalogs/ShopCatalog.asset and
//     is created/refreshed by Restorium -> Rebuild Catalogs & Locale Tables.
// [ ] Scene components that need it (ShopScreen, DeskHubScreen) have a
//     "Catalog" field: drag ShopCatalog.asset into it (the scene builder does this).
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Data
{
    using RestoriumEmporium.Core;

    [CreateAssetMenu(menuName = "Restorium/Shop Catalog", fileName = "ShopCatalog")]
    public class ShopCatalog : ScriptableObject
    {
        [Tooltip("Filled automatically by the editor. Order = shop grid order.")]
        public List<ShopItemData> items = new List<ShopItemData>();

        private Dictionary<string, ShopItemData> _byId;

        public IReadOnlyList<ShopItemData> Items => items;

        public ShopItemData Find(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            if (_byId == null)
            {
                _byId = new Dictionary<string, ShopItemData>();

                foreach (var item in items)
                {
                    if (item != null && !string.IsNullOrEmpty(item.itemId) && !_byId.ContainsKey(item.itemId))
                    {
                        _byId.Add(item.itemId, item);
                    }
                }
            }

            return _byId.TryGetValue(itemId, out var found) ? found : null;
        }

        /// <summary>Items of one tab, in display order. Allocates; call on tab change, not per frame.</summary>
        public List<ShopItemData> InCategory(ShopCategory category)
        {
            var result = new List<ShopItemData>();

            foreach (var item in items)
            {
                if (item != null && item.category == category)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        /// <summary>Called by the editor after it rewrites the list.</summary>
        public void Invalidate() => _byId = null;

        private void OnValidate() => Invalidate();
    }
}
