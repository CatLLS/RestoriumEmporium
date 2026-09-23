// ============================================================
// ShopScreen — "Tracy's Emporium Shop": tabs, coin balance, the item grid.
// WHAT & WHY: The decoration catalogue (GameScreen.Shop). It lists every
//   ShopItemData of the active tab as a ShopItemCard; tapping an item the player
//   doesn't own opens the desk hub in Preview mode with that item, where it can be
//   positioned and bought. It never spends coins itself.
// KEY DECISIONS:
//   - The grid is built from ShopCatalog at runtime by cloning ONE disabled
//     template card under a ScrollRect content that has a GridLayoutGroup (+
//     ContentSizeFitter). A new ShopItemData asset therefore appears in the shop
//     with no scene edit, and the grid scrolls once it outgrows the panel.
//   - One card per item, created lazily and kept (tabs just show/hide them), so a
//     card's tutorial anchor id "shop.item.<id>" never has to change.
//   - Owned state is refreshed on show and on IDecorationInventory.ItemPurchased.
//   - The layout is rebuilt immediately after (re)building the grid so the
//     tutorial hand, which looks the lamp card up on this same frame, gets the
//     right position instead of (0,0).
//   - "Remove ads" and "Buy More Coins" are visible but non-interactable this
//     build (IAP/ads come next build); this script forces that in Awake so a
//     scene edit can't accidentally enable a button that does nothing.
//   - Routing goes through GameFlowController (GoToDeskHub), and the item to
//     preview is handed to DeskHubScreen.RequestPreview BEFORE routing, as the
//     contract requires. Screens never call the router directly.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Full hierarchy with art files and Figma elements: Docs/Batch2/handoff/SHOP.md.
// [ ] Under Canvas create an empty object named exactly ShopScreen, anchor
//     stretch/stretch, offsets 0. Add Component -> Shop Screen.
// [ ] Build the children listed in SHOP.md (backgrounds, BackButton, balance,
//     RemoveAdsButton, BuyMoreCoinsButton, DecorTab, MiscTab, Grid ScrollRect
//     with Viewport/Content/CardTemplate, EmptyLabel).
// [ ] Drag into the Inspector:
//       Flow            <- GameFlow object (GameFlowController)
//       Desk Hub        <- DeskHubScreen object
//       Catalog         <- Assets/Data/Catalogs/ShopCatalog.asset
//       Grid Content    <- Grid/Viewport/Content (has GridLayoutGroup)
//       Card Template   <- Grid/Viewport/Content/CardTemplate (keep it UNTICKED)
//       Scroll          <- Grid (ScrollRect)
//       Decor Tab / Misc Tab, Back Button, Remove Ads Button, Buy More Coins Button
//       Empty Label     <- EmptyLabel (text "More items coming soon!")
// [ ] Add ShopScreen to the ScreenRouter "Screens" list on GameFlow.
// [ ] Tutorial Anchor on BackButton: shop.back (cards add theirs by themselves).
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Economy;

    [DisallowMultipleComponent]
    public class ShopScreen : ScreenView
    {
        [Header("Flow")]
        [Tooltip("GameFlowController on the GameFlow object.")]
        [SerializeField] private GameFlowController flow;

        [Tooltip("The desk hub screen; receives the item to preview.")]
        [SerializeField] private DeskHubScreen deskHub;

        [Header("Data")]
        [SerializeField] private ShopCatalog catalog;

        [Header("Grid")]
        [Tooltip("ScrollRect content with a GridLayoutGroup. Cards are cloned under it.")]
        [SerializeField] private RectTransform gridContent;

        [Tooltip("Disabled card under Grid Content; cloned once per item.")]
        [SerializeField] private ShopItemCard cardTemplate;

        [Tooltip("Optional. Scrolled back to the top when the tab changes.")]
        [SerializeField] private ScrollRect scroll;

        [Tooltip("Optional. Shown when the active tab has no items (the Misc. tab this build).")]
        [SerializeField] private GameObject emptyLabel;

        [Header("Tabs")]
        [SerializeField] private Button decorTab;
        [SerializeField] private Button miscTab;

        [Range(0f, 1f)]
        [Tooltip("Alpha of the tab that is NOT selected.")]
        [SerializeField] private float inactiveTabAlpha = 0.55f;

        [Header("Buttons")]
        [SerializeField] private Button backButton;

        [Tooltip("Visible but disabled this build.")]
        [SerializeField] private Button removeAdsButton;

        [Tooltip("Visible but disabled this build.")]
        [SerializeField] private Button buyMoreCoinsButton;

        private readonly Dictionary<string, ShopItemCard> _cards = new Dictionary<string, ShopItemCard>();
        private IDecorationInventory _inventory;
        private bool _subscribed;
        private ShopCategory _tab = ShopCategory.Decor;
        private CanvasGroup _decorTabGroup;
        private CanvasGroup _miscTabGroup;

        public override GameScreen Screen => GameScreen.Shop;

        /// <summary>The tab currently shown.</summary>
        public ShopCategory ActiveTab => _tab;

        private void Awake()
        {
            if (decorTab != null)
            {
                decorTab.onClick.AddListener(SelectDecor);
                _decorTabGroup = GetOrAddGroup(decorTab.gameObject);
            }

            if (miscTab != null)
            {
                miscTab.onClick.AddListener(SelectMisc);
                _miscTabGroup = GetOrAddGroup(miscTab.gameObject);
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(GoBack);
            }

            // Store features arrive next build: visible, not clickable.
            if (removeAdsButton != null)
            {
                removeAdsButton.interactable = false;
            }

            if (buyMoreCoinsButton != null)
            {
                buyMoreCoinsButton.interactable = false;
            }

            if (cardTemplate != null && cardTemplate.gameObject.activeSelf)
            {
                cardTemplate.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (decorTab != null)
            {
                decorTab.onClick.RemoveListener(SelectDecor);
            }

            if (miscTab != null)
            {
                miscTab.onClick.RemoveListener(SelectMisc);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(GoBack);
            }

            foreach (var pair in _cards)
            {
                if (pair.Value != null)
                {
                    pair.Value.Clicked -= OnCardClicked;
                }
            }

            Unsubscribe();
        }

        protected override void OnShown()
        {
            if (_inventory == null)
            {
                _inventory = ServiceLocator.Get<IDecorationInventory>();

                if (_inventory == null)
                {
                    Debug.LogWarning("[ShopScreen] No IDecorationInventory registered; every item shows " +
                                     "as not owned.", this);
                }
            }

            if (!_subscribed && _inventory != null)
            {
                _inventory.ItemPurchased += OnItemPurchased;
                _subscribed = true;
            }

            ShowTab(ShopCategory.Decor);
        }

        protected override void OnHidden()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_subscribed && _inventory != null)
            {
                _inventory.ItemPurchased -= OnItemPurchased;
            }

            _subscribed = false;
        }

        private void OnItemPurchased(string itemId)
        {
            if (_cards.TryGetValue(itemId, out var card) && card != null)
            {
                card.SetOwned(true);
            }
        }

        public void SelectDecor() => ShowTab(ShopCategory.Decor);

        public void SelectMisc() => ShowTab(ShopCategory.Misc);

        /// <summary>Shows one tab's items, in catalogue (displayOrder) order.</summary>
        public void ShowTab(ShopCategory category)
        {
            _tab = category;
            SetTabLook(_decorTabGroup, category == ShopCategory.Decor);
            SetTabLook(_miscTabGroup, category == ShopCategory.Misc);

            var shown = 0;

            if (catalog == null || gridContent == null || cardTemplate == null)
            {
                Debug.LogWarning("[ShopScreen] Catalog, Grid Content or Card Template is not assigned.", this);
            }
            else
            {
                // Hide everything, then show this tab's cards in catalogue order.
                foreach (var pair in _cards)
                {
                    if (pair.Value != null)
                    {
                        pair.Value.gameObject.SetActive(false);
                    }
                }

                var items = catalog.Items;

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];

                    if (item == null || string.IsNullOrEmpty(item.itemId) || item.category != category)
                    {
                        continue;
                    }

                    var card = GetOrCreateCard(item);
                    card.SetOwned(_inventory != null && _inventory.Owns(item.itemId));
                    card.transform.SetSiblingIndex(shown + 1); // +1: the template stays first
                    card.gameObject.SetActive(true);
                    shown++;
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(gridContent);
            }

            if (emptyLabel != null)
            {
                emptyLabel.SetActive(shown == 0);
            }

            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        private ShopItemCard GetOrCreateCard(ShopItemData item)
        {
            if (_cards.TryGetValue(item.itemId, out var existing) && existing != null)
            {
                return existing;
            }

            // The template is inactive, so the clone is too: Bind (and the tutorial
            // anchor id) happen before the card's OnEnable registers the anchor.
            var card = Instantiate(cardTemplate, gridContent, false);
            card.Bind(item, _inventory != null && _inventory.Owns(item.itemId));
            card.Clicked += OnCardClicked;
            _cards[item.itemId] = card;
            return card;
        }

        private void OnCardClicked(ShopItemData item)
        {
            if (item == null)
            {
                return;
            }

            if (_inventory != null && _inventory.Owns(item.itemId))
            {
                return; // owned items cannot be re-bought
            }

            if (deskHub == null || flow == null)
            {
                Debug.LogWarning("[ShopScreen] Desk Hub or Flow is not assigned; cannot open the preview.", this);
                return;
            }

            deskHub.RequestPreview(item);
            flow.GoToDeskHub();
        }

        private void GoBack()
        {
            if (flow != null)
            {
                flow.GoToDeskHub();
            }
            else
            {
                Debug.LogWarning("[ShopScreen] Flow is not assigned.", this);
            }
        }

        private void SetTabLook(CanvasGroup group, bool active)
        {
            if (group != null)
            {
                group.alpha = active ? 1f : inactiveTabAlpha;
            }
        }

        private static CanvasGroup GetOrAddGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }
    }
}
