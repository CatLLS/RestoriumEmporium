// ============================================================
// BuildShop — creates/updates "ShopScreen" (Tracy's Emporium Shop).
// WHAT & WHY: Full hierarchy per Docs/Batch2/handoff/SHOP.md §3 and
//   Docs/Batch2/FigmaLayout.md §6 (Shop 455:42).
// KEY DECISIONS:
//   - Every coordinate below is Figma-absolute to the 412x917 ShopScreen frame
//     (ShopScreen itself stretches to fill Canvas, so no ancestor subtracts an
//     offset) — see BuildDeskHub.cs's header comment for the general rule this
//     builder follows throughout.
//   - Purely decorative Figma nodes with NO serialized field on ShopScreen or
//     ShopItemCard (sparkle stars, the scrollbar track/thumb, the "Buy More
//     Coins"/"Remove ads" play-triangle icon) are skipped — SHOP.md's own
//     handoff explicitly says these may be skipped ("neither has a serialized
//     field"), so skipping them does not lose any wiring, only cosmetic polish
//     a human can add by hand later if desired.
//   - CardTemplate stays INACTIVE (ShopScreen.Awake force-disables it too, but
//     the builder leaves it that way from the start so the Scene view doesn't
//     show a stray extra "item" the moment the screen is opened for editing).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — run Restorium/Scene/Build Batch 2 Scene Objects with the
//     Game scene open. See Docs/Batch2/handoff/SCENE_BUILDER.md.
// ---------------------------------------------------------------

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.EditorTools
{
    internal static class BuildShop
    {
        public static RestoriumEmporium.UI.ShopScreen Build(Transform canvas,
            RestoriumEmporium.Core.GameFlowController flow, RestoriumEmporium.UI.DeskHubScreen deskHub,
            RestoriumEmporium.Data.ShopCatalog shopCatalog)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "ShopScreen");
            SceneBuilderCore.Stretch(root);

            var screenFill = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "ScreenFill", 0);
            SceneBuilderCore.Stretch(screenFill);
            SceneBuilderCore.SetColorShape(screenFill, SceneBuilderCore.Hex("1f0b07"));

            var background = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Background", 1);
            SceneBuilderCore.FigmaRect(background, -14f, 359f, 441f, 567f);
            SceneBuilderCore.SetImage(background, "Assets/Art/Shop/leatherBG.png");

            var headerImage = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "HeaderImage", 2);
            SceneBuilderCore.Stretch(headerImage);
            var bookshelf = SceneBuilderCore.FindOrCreateChildOrdered(headerImage.transform, "Bookshelf", 0);
            SceneBuilderCore.FigmaRect(bookshelf, 16f, 48f, 380f, 336f);
            SceneBuilderCore.SetImage(bookshelf, "Assets/Art/Shop/shopBookshelfBG.png");
            var headerPanel = SceneBuilderCore.FindOrCreateChildOrdered(headerImage.transform, "HeaderPanel", 1);
            SceneBuilderCore.FigmaRect(headerPanel, 15.5f, 0f, 380.5f, 157.3f);
            SceneBuilderCore.SetImage(headerPanel, "Assets/Art/Shop/shopHeaderPanel.png");
            var tracyShop = SceneBuilderCore.FindOrCreateChildOrdered(headerImage.transform, "TracyShop", 2);
            SceneBuilderCore.FigmaRect(tracyShop, 208f, 140f, 208f, 253f);
            SceneBuilderCore.SetImage(tracyShop, "Assets/Art/Shop/tracyShop.png");

            var titleGroup = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "TitleGroup", 3);
            SceneBuilderCore.FigmaRect(titleGroup, 43.58f, 64f, 320.42f, 64.95f);
            var titleTop = SceneBuilderCore.FindOrCreateChild(titleGroup.transform, "TitleTop");
            SceneBuilderCore.FigmaRect(titleTop, 59.47f - 43.58f, 82.68f - 64f, 73.5f, 17.1f);
            SceneBuilderCore.SetupText(titleTop, SceneBuilderCore.FontChoice.SpecialElite, 20f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.shop.titleTop", "Tracy's");
            var titleMain = SceneBuilderCore.FindOrCreateChild(titleGroup.transform, "TitleMain");
            SceneBuilderCore.FigmaRect(titleMain, 43.58f - 43.58f, 94.68f - 64f, 302f, 34.27f);
            SceneBuilderCore.SetupText(titleMain, SceneBuilderCore.FontChoice.Rye, 32f,
                SceneBuilderCore.Hex("e4c191"), TextAlignmentOptions.Center, "ui.shop.titleMain",
                "Emporium Shop");

            var backGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "BackButton", 4);
            // Same arrow as the Journal's back button: backArrowIcon.png already points left,
            // so no rotation (a 180° turn about the top-left pivot threw it off-screen).
            SceneBuilderCore.FigmaRect(backGo, 24.6f, 40.2f, 21.4f, 29f);
            SceneBuilderCore.SetImage(backGo, "Assets/Art/UI/backArrowIcon.png");
            backGo.transform.localEulerAngles = Vector3.zero;
            var backButton = SceneBuilderCore.SetupButton(backGo);
            SceneBuilderCore.SetupAnchor(backGo, "shop.back");
            var backLabelGo = SceneBuilderCore.FindOrCreateChild(backGo.transform, "Label");
            var backLabelRect = SceneBuilderCore.Rect(backLabelGo);
            backLabelRect.anchorMin = new Vector2(1f, 0.5f);
            backLabelRect.anchorMax = new Vector2(1f, 0.5f);
            backLabelRect.pivot = new Vector2(0f, 0.5f);
            backLabelRect.sizeDelta = new Vector2(224.9f, 20f);
            backLabelRect.anchoredPosition = new Vector2(16.25f, 1.37f);
            backLabelGo.transform.localEulerAngles = Vector3.zero;
            SceneBuilderCore.SetupText(backLabelGo, SceneBuilderCore.FontChoice.SpecialElite, 18f,
                new Color(0.78f, 0.784f, 0.737f, 0.74f), TextAlignmentOptions.Left, "ui.shop.back",
                "Go back to workbench");

            var decorTabGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "DecorTab", 5);
            SceneBuilderCore.FigmaRect(decorTabGo, 26f, 391f, 87f, 21.3f);
            SceneBuilderCore.SetImage(decorTabGo, "Assets/Art/Shop/categoryTab.png");
            var decorTab = SceneBuilderCore.SetupButton(decorTabGo);
            var decorLabel = SceneBuilderCore.FindOrCreateChild(decorTabGo.transform, "Label");
            SceneBuilderCore.Stretch(decorLabel);
            SceneBuilderCore.SetupText(decorLabel, SceneBuilderCore.FontChoice.SpecialElite, 13f,
                SceneBuilderCore.Hex("e4c191"), TextAlignmentOptions.Center, "ui.shop.tabDecor", "Décor");

            var miscTabGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "MiscTab", 6);
            SceneBuilderCore.FigmaRect(miscTabGo, 117f, 391f, 87f, 21.3f);
            SceneBuilderCore.SetImage(miscTabGo, "Assets/Art/Shop/categoryTab.png");
            var miscTab = SceneBuilderCore.SetupButton(miscTabGo);
            var miscLabel = SceneBuilderCore.FindOrCreateChild(miscTabGo.transform, "Label");
            SceneBuilderCore.Stretch(miscLabel);
            SceneBuilderCore.SetupText(miscLabel, SceneBuilderCore.FontChoice.SpecialElite, 13f,
                SceneBuilderCore.Hex("99805f"), TextAlignmentOptions.Center, "ui.shop.tabMisc", "Misc.");

            var balanceRow = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "CurrentBalanceRow", 7);
            SceneBuilderCore.FigmaRect(balanceRow, 27f, 192f, 163f, 33f);
            SceneBuilderCore.SetColorShape(balanceRow, SceneBuilderCore.Hex("44151c"));
            var balanceLabelGo = SceneBuilderCore.FindOrCreateChild(balanceRow.transform, "BalanceLabel");
            SceneBuilderCore.Stretch(balanceLabelGo);
            var balanceText = SceneBuilderCore.SetupText(balanceLabelGo, SceneBuilderCore.FontChoice.SpecialElite,
                20f, Color.white, TextAlignmentOptions.Center, null, "0");
            var balanceBinding = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.CoinBalanceLabel>(balanceLabelGo);
            var balanceSo = new SerializedObject(balanceBinding);
            SceneBuilderCore.SetField(balanceSo, "label", balanceText, "ShopScreen/BalanceLabel");
            balanceSo.ApplyModifiedProperties();

            var removeAdsGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "RemoveAdsButton", 8);
            SceneBuilderCore.FigmaRect(removeAdsGo, 27f, 245f, 163f, 33f);
            SceneBuilderCore.SetColorShape(removeAdsGo, SceneBuilderCore.Hex("44151c"));
            var removeAdsButton = SceneBuilderCore.SetupButton(removeAdsGo);
            var removeAdsLabel = SceneBuilderCore.FindOrCreateChild(removeAdsGo.transform, "Label");
            SceneBuilderCore.Stretch(removeAdsLabel);
            SceneBuilderCore.SetupText(removeAdsLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f, Color.white,
                TextAlignmentOptions.Left, "ui.shop.removeAds", "Remove ads");

            var buyCoinsGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "BuyMoreCoinsButton", 9);
            SceneBuilderCore.FigmaRect(buyCoinsGo, 27f, 299f, 163f, 33f);
            SceneBuilderCore.SetColorShape(buyCoinsGo, SceneBuilderCore.Hex("44151c"));
            var buyCoinsButton = SceneBuilderCore.SetupButton(buyCoinsGo);
            var buyCoinsLabel = SceneBuilderCore.FindOrCreateChild(buyCoinsGo.transform, "Label");
            SceneBuilderCore.Stretch(buyCoinsLabel);
            SceneBuilderCore.SetupText(buyCoinsLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f, Color.white,
                TextAlignmentOptions.Left, "ui.shop.buyMoreCoins", "Buy More Coins");

            var emptyLabelGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "EmptyLabel", 10);
            SceneBuilderCore.FigmaRect(emptyLabelGo, 26f, 500f, 362f, 60f);
            SceneBuilderCore.SetupText(emptyLabelGo, SceneBuilderCore.FontChoice.SpecialElite, 18f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.shop.empty",
                "More items coming soon!");
            SceneBuilderCore.SetActive(emptyLabelGo, false);

            var grid = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Grid", 11);
            SceneBuilderCore.FigmaRect(grid, 26f, 411f, 362f, 475f);
            var scroll = SceneBuilderCore.AddOrGet<ScrollRect>(grid);
            scroll.horizontal = false;
            scroll.vertical = true;

            var viewport = SceneBuilderCore.FindOrCreateChild(grid.transform, "Viewport");
            SceneBuilderCore.Stretch(viewport);
            SceneBuilderCore.SetColorShape(viewport, new Color(0f, 0f, 0f, 0f));
            SceneBuilderCore.AddOrGet<RectMask2D>(viewport);
            scroll.viewport = SceneBuilderCore.Rect(viewport);

            var content = SceneBuilderCore.FindOrCreateChild(viewport.transform, "Content");
            var contentRect = SceneBuilderCore.Rect(content);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            var gridLayout = SceneBuilderCore.AddOrGet<GridLayoutGroup>(content);
            gridLayout.cellSize = new Vector2(82f, 110f);
            gridLayout.spacing = new Vector2(14f, 14f);
            // The grid rect starts under the tab row; the padding drops the first
            // row of cards clear of the tabs.
            gridLayout.padding = new RectOffset(0, 0, 20, 0);
            gridLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = SceneBuilderCore.AddOrGet<ContentSizeFitter>(content);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contentRect;

            var cardTemplate = BuildCardTemplate(content.transform);

            var screen = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.ShopScreen>(root);
            var so = new SerializedObject(screen);
            SceneBuilderCore.SetField(so, "flow", flow, "ShopScreen");
            SceneBuilderCore.SetField(so, "deskHub", deskHub, "ShopScreen");
            SceneBuilderCore.SetField(so, "catalog", shopCatalog, "ShopScreen");
            SceneBuilderCore.SetField(so, "gridContent", contentRect, "ShopScreen");
            SceneBuilderCore.SetField(so, "cardTemplate", cardTemplate, "ShopScreen");
            SceneBuilderCore.SetField(so, "scroll", scroll, "ShopScreen");
            SceneBuilderCore.SetField(so, "emptyLabel", emptyLabelGo, "ShopScreen");
            SceneBuilderCore.SetField(so, "decorTab", decorTab, "ShopScreen");
            SceneBuilderCore.SetField(so, "miscTab", miscTab, "ShopScreen");
            SceneBuilderCore.SetField(so, "backButton", backButton, "ShopScreen");
            SceneBuilderCore.SetField(so, "removeAdsButton", removeAdsButton, "ShopScreen");
            SceneBuilderCore.SetField(so, "buyMoreCoinsButton", buyCoinsButton, "ShopScreen");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return screen;
        }

        private static RestoriumEmporium.UI.ShopItemCard BuildCardTemplate(Transform content)
        {
            var cardGo = SceneBuilderCore.FindOrCreateChild(content, "CardTemplate");
            var cardRect = SceneBuilderCore.Rect(cardGo);
            cardRect.sizeDelta = new Vector2(82f, 107f);
            SceneBuilderCore.SetImage(cardGo, "Assets/Art/Shop/itemBG.png", raycastTarget: true);
            var cardButton = SceneBuilderCore.SetupButton(cardGo);

            var icon = SceneBuilderCore.FindOrCreateChild(cardGo.transform, "Icon");
            var iconRect = SceneBuilderCore.Rect(icon);
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(64f, 64f);
            iconRect.anchoredPosition = new Vector2(0f, 6f);
            var iconImage = SceneBuilderCore.AddOrGet<Image>(icon);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var priceGroup = SceneBuilderCore.FindOrCreateChild(cardGo.transform, "PriceGroup");
            var priceGroupRect = SceneBuilderCore.Rect(priceGroup);
            priceGroupRect.anchorMin = new Vector2(0.5f, 0f);
            priceGroupRect.anchorMax = new Vector2(0.5f, 0f);
            priceGroupRect.pivot = new Vector2(0.5f, 0f);
            priceGroupRect.sizeDelta = new Vector2(60f, 20f);
            priceGroupRect.anchoredPosition = new Vector2(0f, 6f);

            var coinIcon = SceneBuilderCore.FindOrCreateChild(priceGroup.transform, "CoinIcon");
            var coinIconRect = SceneBuilderCore.Rect(coinIcon);
            coinIconRect.anchorMin = new Vector2(0f, 0.5f);
            coinIconRect.anchorMax = new Vector2(0f, 0.5f);
            coinIconRect.sizeDelta = new Vector2(16f, 16f);
            coinIconRect.anchoredPosition = new Vector2(8f, 0f);
            SceneBuilderCore.SetImage(coinIcon, "Assets/Art/UI/coinIcon.png");

            var priceLabelGo = SceneBuilderCore.FindOrCreateChild(priceGroup.transform, "PriceLabel");
            var priceLabelRect = SceneBuilderCore.Rect(priceLabelGo);
            priceLabelRect.anchorMin = new Vector2(0f, 0.5f);
            priceLabelRect.anchorMax = new Vector2(1f, 0.5f);
            priceLabelRect.offsetMin = new Vector2(20f, -10f);
            priceLabelRect.offsetMax = new Vector2(0f, 10f);
            var priceLabel = SceneBuilderCore.SetupText(priceLabelGo, SceneBuilderCore.FontChoice.SpecialElite,
                13f, Color.white, TextAlignmentOptions.Left, null, "0");

            var ownedBadgeGo = SceneBuilderCore.FindOrCreateChild(cardGo.transform, "OwnedBadge");
            var ownedRect = SceneBuilderCore.Rect(ownedBadgeGo);
            ownedRect.anchorMin = new Vector2(0.5f, 0f);
            ownedRect.anchorMax = new Vector2(0.5f, 0f);
            ownedRect.pivot = new Vector2(0.5f, 0f);
            ownedRect.sizeDelta = new Vector2(70f, 20f);
            ownedRect.anchoredPosition = new Vector2(0f, 6f);
            SceneBuilderCore.SetupText(ownedBadgeGo, SceneBuilderCore.FontChoice.SpecialElite, 13f,
                SceneBuilderCore.Hex("f5ba55"), TextAlignmentOptions.Center, "ui.shop.owned", "Owned");
            SceneBuilderCore.SetActive(ownedBadgeGo, false);

            var buttonSfx = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.ButtonSfx>(cardGo);
            var sfxSo = new SerializedObject(buttonSfx);
            SceneBuilderCore.SetFieldEnum(sfxSo, "sfx", RestoriumEmporium.Core.SfxId.ButtonClick,
                "ShopScreen/CardTemplate");
            sfxSo.ApplyModifiedProperties();

            var card = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.ShopItemCard>(cardGo);
            var cardSo = new SerializedObject(card);
            SceneBuilderCore.SetField(cardSo, "button", cardButton, "ShopScreen/CardTemplate");
            SceneBuilderCore.SetField(cardSo, "icon", iconImage, "ShopScreen/CardTemplate");
            SceneBuilderCore.SetField(cardSo, "priceLabel", priceLabel, "ShopScreen/CardTemplate");
            SceneBuilderCore.SetField(cardSo, "priceGroup", priceGroup, "ShopScreen/CardTemplate");
            SceneBuilderCore.SetField(cardSo, "ownedBadge", ownedBadgeGo, "ShopScreen/CardTemplate");
            cardSo.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(cardGo, false);
            return card;
        }
    }
}
