// ============================================================
// ShopItemCard — one tile in the shop grid: icon, price with coin, owned state.
// WHAT & WHY: The shop grid must grow with the catalogue without anyone editing
//   the scene. ShopScreen clones ONE template card (a disabled child of the grid)
//   per ShopItemData and calls Bind on it; this component only knows how to draw
//   one item and report a tap.
// KEY DECISIONS:
//   - The card does not route or buy anything: it raises Clicked(item) and
//     ShopScreen decides (open preview / ignore owned items).
//   - Each card registers the tutorial anchor "shop.item.<itemId>" at runtime,
//     so the tutorial can point at the Lamp card even though that card did not
//     exist in the scene.
//   - Owned items hide the price, show an "Owned" badge and become
//     non-interactable: owned items cannot be re-bought.
//   - The price is drawn as a plain number (the coin icon sits beside it), so it
//     needs no localisation; the "Owned" badge uses a LocalizedText in the scene.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// This lives on the CARD TEMPLATE inside the shop grid (see ShopScreen's
// checklist and Docs/Batch2/handoff/SHOP.md for the full hierarchy):
// [ ] ShopScreen/.../Grid/Viewport/Content/CardTemplate: a UI -> Button (delete
//     its Text child). Its Image = the card frame art. Add Component -> Shop Item Card
//     and Button Sfx (Sfx = Button Click).
// [ ] Children: Icon (Image), PriceGroup (empty) containing CoinIcon (Image) and
//     PriceLabel (TextMeshPro), OwnedBadge (TextMeshPro + Localized Text,
//     Key = ui.shop.owned).
// [ ] Drag them into Button, Icon, Price Label, Price Group and Owned Badge.
// [ ] Untick the CardTemplate object (inactive): ShopScreen clones it at runtime.
// ---------------------------------------------------------------

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Decor;

    [DisallowMultipleComponent]
    public class ShopItemCard : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("Shows ShopItemData.shopIcon.")]
        [SerializeField] private Image icon;

        [Tooltip("Shows ShopItemData.price.")]
        [SerializeField] private TMP_Text priceLabel;

        [Tooltip("Parent of the coin icon + price label; hidden when owned.")]
        [SerializeField] private GameObject priceGroup;

        [Tooltip("Shown instead of the price when the item is owned.")]
        [SerializeField] private GameObject ownedBadge;

        [Range(0f, 1f)]
        [Tooltip("Icon alpha when owned, so owned items read as 'done'.")]
        [SerializeField] private float ownedIconAlpha = 0.6f;

        private ShopItemData _item;
        private bool _owned;

        public ShopItemData Item => _item;
        public bool Owned => _owned;

        /// <summary>Raised when the (not owned) card is tapped.</summary>
        public event Action<ShopItemData> Clicked;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        /// <summary>
        /// Called once, on a freshly cloned (still inactive) card. Draws the item
        /// and registers the tutorial anchor "shop.item.&lt;itemId&gt;".
        /// </summary>
        public void Bind(ShopItemData item, bool owned)
        {
            _item = item;
            gameObject.name = "Card_" + (item != null ? item.itemId : "none");

            if (icon != null)
            {
                icon.sprite = item != null ? item.shopIcon : null;
                icon.enabled = icon.sprite != null;
                icon.preserveAspect = true;
            }

            if (priceLabel != null)
            {
                priceLabel.text = item != null ? item.price.ToString() : string.Empty;
            }

            if (item != null)
            {
                RuntimeTutorialAnchor.Attach(gameObject, "shop.item." + item.itemId);
            }

            _owned = !owned; // force a redraw
            SetOwned(owned);
        }

        public void SetOwned(bool owned)
        {
            if (owned == _owned)
            {
                return;
            }

            _owned = owned;

            if (priceGroup != null)
            {
                priceGroup.SetActive(!owned);
            }

            if (ownedBadge != null)
            {
                ownedBadge.SetActive(owned);
            }

            if (button != null)
            {
                button.interactable = !owned;
            }

            if (icon != null)
            {
                var c = icon.color;
                c.a = owned ? ownedIconAlpha : 1f;
                icon.color = c;
            }
        }

        private void HandleClick()
        {
            if (!_owned && _item != null)
            {
                Clicked?.Invoke(_item);
            }
        }
    }
}
