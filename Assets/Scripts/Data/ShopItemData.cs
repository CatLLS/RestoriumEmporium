// ============================================================
// ShopItemData — one decoration the player can buy and place on the desk hub.
// WHAT & WHY: The shop must grow without code changes (build spec §1.3). Every
//   item is one asset holding everything about it: its shop icon, the art that
//   sits in the room, its price, name, description and default placement.
// KEY DECISIONS:
//   - Name and description are localisation KEYS, so the item reads correctly in
//     every language. You do not have to type keys by hand: the menu
//     "Restorium > Shop > New Shop Item" creates the asset AND its strings.
//   - itemId is the save-file identity and must never change once shipped.
//     displayOrder, not asset name, orders the shop grid.
//   - placedSize is in the 412x917 reference space (the same numbers Figma
//     shows), so authoring an item is "copy the size from Figma".
//   - defaultPosition is normalised 0..1 in the room (0,0 = bottom-left), the
//     same space OwnedDecoration uses; it is where preview mode first puts it.
//   - Items are discovered automatically: anything under Assets/Data/ShopItems/
//     is added to the ShopCatalog by the editor (see ShopCatalog).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Easiest: menu Restorium -> Shop -> New Shop Item, fill the window, press Create.
// [ ] Manual: right-click in Assets/Data/ShopItems -> Create -> Restorium -> Shop Item,
//     then fill Item Id (lowercase, no spaces, e.g. "lamp"), Shop Icon, Placed Sprite,
//     Price, Placed Size, and run Restorium -> Rebuild Catalogs & Locale Tables.
// [ ] Both sprites: Texture Type = Sprite (2D and UI). Keep Mesh Type "Tight" is fine
//     here (unlike the posters) — these are never scrubbed.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Data
{
    using RestoriumEmporium.Core;

    [CreateAssetMenu(menuName = "Restorium/Shop Item", fileName = "ShopItem")]
    public class ShopItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id written into the save file (e.g. \"lamp\"). Never change it once shipped.")]
        public string itemId = string.Empty;

        [Tooltip("Which shop tab lists this item.")]
        public ShopCategory category = ShopCategory.Decor;

        [Tooltip("Position in the shop grid. Lower comes first.")]
        public int displayOrder;

        [Header("Text (localisation keys; the New Shop Item window fills these)")]
        public string nameKey = string.Empty;
        public string descriptionKey = string.Empty;

        [Header("Economy")]
        [Min(0)] public int price = 50;

        [Header("Art")]
        [Tooltip("Picture on the shop card.")]
        public Sprite shopIcon;

        [Tooltip("What sits in the desk-hub room once bought.")]
        public Sprite placedSprite;

        [Tooltip("Size in the room, in 412x917 reference pixels (copy from Figma).")]
        public Vector2 placedSize = new Vector2(120f, 120f);

        [Tooltip("Where preview mode first places it: 0..1 inside the room, (0,0) = bottom-left.")]
        public Vector2 defaultPosition = new Vector2(0.5f, 0.5f);

        [Tooltip("Draw order among decorations. Higher draws in front.")]
        public int sortingOrder;

        public string NameKeyOrDefault => string.IsNullOrEmpty(nameKey) ? "shop.item." + itemId + ".name" : nameKey;
        public string DescriptionKeyOrDefault => string.IsNullOrEmpty(descriptionKey) ? "shop.item." + itemId + ".desc" : descriptionKey;
    }
}
