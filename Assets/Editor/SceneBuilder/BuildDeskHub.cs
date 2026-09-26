// ============================================================
// BuildDeskHub — creates/updates "DeskHubScreen" (Normal / Edit / Preview modes).
// WHAT & WHY: Full hierarchy per Docs/Batch2/handoff/SHOP.md §3 and
//   Docs/Batch2/FigmaLayout.md §5/§7/§8/§9 (Desk_Hub, PreviewMode,
//   SelectedObjectToMove, enteredEditMode all share this one screen's
//   underlying room; only the top bar/panel changes per mode).
// KEY DECISIONS:
//   - COORDINATE CONVENTION: every Figma number in the handoffs is absolute to
//     the 412x917 frame UNLESS the handoff says "rel." (TopBar's children) or
//     the element is drawn as a child of an already-positioned visual container
//     (a header bar, an info panel) rather than a full-screen group. This file
//     therefore makes every MODE GROUP (NormalUI/EditUI/PreviewUI) and every
//     "*Group" container stretch to fill the screen (so its direct children's
//     Figma coordinates resolve as-given), and only subtracts a parent's own
//     origin for the handful of elements that are genuinely nested inside a
//     smaller positioned panel (TopBar's buttons, EditHeader/PreviewHeader's
//     title+coins badge, and the three buttons-and-labels panels). See the
//     inline comments at each such spot for the exact subtraction.
//   - catBoard.png and the full-body Tracy art are NOT built here (dispute (a)
//     in CONSISTENCY.md) — they belong to HubTracyOverlay (BuildHubTracyOverlay.cs).
//   - The desk hub's own hamburger (MenuButton) is wired directly to
//     OverlayController.OpenSettings() by DeskHubScreen's own code — it does
//     NOT get a PauseButton component (unlike every restoration screen).
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
    internal static class BuildDeskHub
    {
        public static RestoriumEmporium.UI.DeskHubScreen Build(Transform canvas,
            RestoriumEmporium.Core.GameFlowController flow, RestoriumEmporium.UI.OverlayController overlays,
            RestoriumEmporium.Data.ShopCatalog shopCatalog)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "DeskHubScreen");
            SceneBuilderCore.Stretch(root);

            var background = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Background", 0);
            SceneBuilderCore.Stretch(background);
            SceneBuilderCore.SetImage(background, "Assets/Art/DeskHub/zoomoutBG.png");

            var roomGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Room", 1);
            SceneBuilderCore.Stretch(roomGo);
            var roomImage = SceneBuilderCore.SetImage(roomGo, null, raycastTarget: false,
                color: new Color(1f, 1f, 1f, 0f));
            var room = SceneBuilderCore.AddOrGet<RestoriumEmporium.Decor.DecorationRoom>(roomGo);
            var roomSo = new SerializedObject(room);
            SceneBuilderCore.SetField(roomSo, "catalog", shopCatalog, "DeskHubScreen/Room");
            roomSo.ApplyModifiedProperties();

            var bookButton = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "BookButton", 2);
            SceneBuilderCore.FigmaRect(bookButton, 151f, 457f, 125f, 115f, 0.5f, 0.5f);
            SceneBuilderCore.SetImage(bookButton, "Assets/Art/DeskHub/book.png");
            var bookBtn = SceneBuilderCore.SetupButton(bookButton);
            SceneBuilderCore.SetupAnchor(bookButton, "desk.book");

            var normalUi = BuildNormalUi(root.transform);
            var editUi = BuildEditUi(root.transform, out var editIdleGroup, out var editSelectedGroup,
                out var editNameLabel, out var editDescLabel, out var placeButton, out var undoButton,
                out var backToWorkshopButton);
            var previewUi = BuildPreviewUi(root.transform, out var previewNameLabel, out var previewPriceLabel,
                out var previewDescLabel, out var notEnoughCoinsLabel, out var previewCoinsBadge,
                out var buyButton, out var giveUpButton);

            var menuButton = normalUi.menuButton;
            var shopButton = normalUi.shopButton;
            var editButton = normalUi.editButton;

            var screen = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.DeskHubScreen>(root);
            var so = new SerializedObject(screen);
            SceneBuilderCore.SetField(so, "flow", flow, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "overlays", overlays, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "room", room, "DeskHubScreen");
            SceneBuilderCore.SetFieldArray(so, "normalObjects", new UnityEngine.Object[] { normalUi.root, bookButton },
                "DeskHubScreen");
            SceneBuilderCore.SetFieldArray(so, "editObjects", new UnityEngine.Object[] { editUi }, "DeskHubScreen");
            SceneBuilderCore.SetFieldArray(so, "previewObjects", new UnityEngine.Object[] { previewUi },
                "DeskHubScreen");
            SceneBuilderCore.SetField(so, "menuButton", menuButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "shopButton", shopButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "editButton", editButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "bookButton", bookBtn, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "editIdleGroup", editIdleGroup, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "editSelectedGroup", editSelectedGroup, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "editNameLabel", editNameLabel, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "editDescLabel", editDescLabel, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "placeButton", placeButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "undoButton", undoButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "backToWorkshopButton", backToWorkshopButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "previewNameLabel", previewNameLabel, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "previewPriceLabel", previewPriceLabel, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "previewDescLabel", previewDescLabel, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "buyButton", buyButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "giveUpButton", giveUpButton, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "notEnoughCoinsLabel", notEnoughCoinsLabel, "DeskHubScreen");
            SceneBuilderCore.SetField(so, "previewCoinsBadge", previewCoinsBadge, "DeskHubScreen");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return screen;
        }

        // ---- NormalUI (top bar) --------------------------------------------------------

        private readonly struct NormalUiResult
        {
            public readonly GameObject root;
            public readonly Button menuButton;
            public readonly Button shopButton;
            public readonly Button editButton;

            public NormalUiResult(GameObject root, Button menuButton, Button shopButton, Button editButton)
            {
                this.root = root;
                this.menuButton = menuButton;
                this.shopButton = shopButton;
                this.editButton = editButton;
            }
        }

        private static NormalUiResult BuildNormalUi(Transform screen)
        {
            var normalUi = SceneBuilderCore.FindOrCreateChildOrdered(screen, "NormalUI", 3);
            SceneBuilderCore.Stretch(normalUi);

            var topBar = SceneBuilderCore.FindOrCreateChild(normalUi.transform, "TopBar");
            SceneBuilderCore.FigmaRect(topBar, 11f, 25f, 396f, 84.36f);

            var menuButton = SceneBuilderCore.FindOrCreateChild(topBar.transform, "MenuButton");
            SceneBuilderCore.FigmaRect(menuButton, 248f, 34f, 53f, 57f);
            SceneBuilderCore.SetImage(menuButton, "Assets/Art/UI/hamburgerIcon.png");
            var menuBtn = SceneBuilderCore.SetupButton(menuButton);
            SceneBuilderCore.SetupAnchor(menuButton, "desk.menu");

            var shopButton = SceneBuilderCore.FindOrCreateChild(topBar.transform, "ShopButton");
            SceneBuilderCore.FigmaRect(shopButton, 11f, 25f, 79f, 84.36f);
            SceneBuilderCore.SetImage(shopButton, "Assets/Art/UI/bagButton.png");
            var shopBtn = SceneBuilderCore.SetupButton(shopButton);
            SceneBuilderCore.SetupAnchor(shopButton, "desk.shop");

            var editButton = SceneBuilderCore.FindOrCreateChild(topBar.transform, "EditButton");
            SceneBuilderCore.FigmaRect(editButton, 96f, 41f, 44f, 40f);
            SceneBuilderCore.SetImage(editButton, "Assets/Art/UI/editModeButtonBase.png");
            var editBtn = SceneBuilderCore.SetupButton(editButton);
            SceneBuilderCore.SetupAnchor(editButton, "desk.edit");
            var editMoveIcon = SceneBuilderCore.FindOrCreateChild(editButton.transform, "MoveIcon");
            SceneBuilderCore.Stretch(editMoveIcon);
            SceneBuilderCore.SetImage(editMoveIcon, "Assets/Art/UI/moveIcon.png");

            var coinsBadge = SceneBuilderCore.FindOrCreateChild(topBar.transform, "CoinsBadge");
            SceneBuilderCore.FigmaRect(coinsBadge, 301f, 36f, 106f, 53f);
            SceneBuilderCore.SetImage(coinsBadge, "Assets/Art/UI/coinShowerBG.png");
            SceneBuilderCore.SetupAnchor(coinsBadge, "desk.coins");
            var coinsLabel = SceneBuilderCore.FindOrCreateChild(coinsBadge.transform, "CoinsLabel");
            SceneBuilderCore.Stretch(coinsLabel);
            var coinsText = SceneBuilderCore.SetupText(coinsLabel, SceneBuilderCore.FontChoice.Rye, 14f,
                Color.black, TextAlignmentOptions.Center, null, "0");
            var coinsBinding = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.CoinBalanceLabel>(coinsLabel);
            var coinsSo = new SerializedObject(coinsBinding);
            SceneBuilderCore.SetField(coinsSo, "label", coinsText, "DeskHubScreen/CoinsLabel");
            coinsSo.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(normalUi, true);
            return new NormalUiResult(normalUi, menuBtn, shopBtn, editBtn);
        }

        // ---- EditUI ---------------------------------------------------------------------

        private static GameObject BuildEditUi(Transform screen, out GameObject editIdleGroup,
            out GameObject editSelectedGroup, out TMP_Text editNameLabel, out TMP_Text editDescLabel,
            out Button placeButton, out Button undoButton, out Button backToWorkshopButton)
        {
            var editUi = SceneBuilderCore.FindOrCreateChildOrdered(screen, "EditUI", 4);
            SceneBuilderCore.Stretch(editUi);

            // -- EditHeader: a positioned bar; Title + MoveIcon are nested, so their
            // coordinates are the Figma-absolute value MINUS the header's own origin (0,56).
            var header = SceneBuilderCore.FindOrCreateChildOrdered(editUi.transform, "EditHeader", 0);
            SceneBuilderCore.FigmaRect(header, 0f, 56f, 413f, 61f);
            SceneBuilderCore.SetColorShape(header, new Color(0f, 0f, 0f, 0.71f));
            var headerTitle = SceneBuilderCore.FindOrCreateChild(header.transform, "Title");
            SceneBuilderCore.FigmaRect(headerTitle, 97f, 16f, 181f, 36f);
            SceneBuilderCore.SetupText(headerTitle, SceneBuilderCore.FontChoice.SpecialElite, 36f, Color.white,
                TextAlignmentOptions.Center, "ui.edit.title", "Edit Mode");
            var headerMoveIcon = SceneBuilderCore.FindOrCreateChild(header.transform, "MoveIcon");
            SceneBuilderCore.FigmaRect(headerMoveIcon, 291f, 13f, 38f, 36f);
            SceneBuilderCore.SetImage(headerMoveIcon, "Assets/Art/UI/moveIconLight.png");

            // -- EditIdleGroup: stretch container; Panel and BackToWorkshopButton are its
            // direct children so their coordinates are Figma-absolute, no subtraction.
            editIdleGroup = SceneBuilderCore.FindOrCreateChildOrdered(editUi.transform, "EditIdleGroup", 1);
            SceneBuilderCore.Stretch(editIdleGroup);

            var idlePanel = SceneBuilderCore.FindOrCreateChild(editIdleGroup.transform, "Panel");
            SceneBuilderCore.FigmaRect(idlePanel, 18f, 640f, 376f, 100f);
            SceneBuilderCore.SetColorShape(idlePanel, new Color(3f / 255f, 50f / 255f, 29f / 255f, 0.52f));

            // IdleLabel is nested under Panel: subtract Panel's own origin (18,640).
            var idleLabel = SceneBuilderCore.FindOrCreateChild(idlePanel.transform, "IdleLabel");
            SceneBuilderCore.FigmaRect(idleLabel, 41f - 18f, 666f - 640f, 330f, 48f);
            SceneBuilderCore.SetupText(idleLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f, Color.white,
                TextAlignmentOptions.Center, "ui.edit.hint", "Select an item to edit its position.");

            var backToWorkshop = SceneBuilderCore.FindOrCreateChild(editIdleGroup.transform,
                "BackToWorkshopButton");
            SceneBuilderCore.FigmaRect(backToWorkshop, 106f, 793f, 212f, 51f);
            SceneBuilderCore.SetImage(backToWorkshop, "Assets/Art/newGameButton.png");
            backToWorkshopButton = SceneBuilderCore.SetupButton(backToWorkshop);
            SceneBuilderCore.SetupAnchor(backToWorkshop, "edit.done");
            var backLabel = SceneBuilderCore.FindOrCreateChild(backToWorkshop.transform, "Label");
            SceneBuilderCore.Stretch(backLabel);
            SceneBuilderCore.SetupText(backLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.edit.backToWorkshop",
                "Back to workshop");

            // -- EditSelectedGroup: stretch container with ONE positioned child, Panel;
            // every label/button below is nested under Panel, so subtract (18,640).
            editSelectedGroup = SceneBuilderCore.FindOrCreateChildOrdered(editUi.transform, "EditSelectedGroup", 2);
            SceneBuilderCore.Stretch(editSelectedGroup);

            var selectedPanel = SceneBuilderCore.FindOrCreateChild(editSelectedGroup.transform, "Panel");
            SceneBuilderCore.FigmaRect(selectedPanel, 18f, 640f, 376f, 220f);
            SceneBuilderCore.SetColorShape(selectedPanel, new Color(3f / 255f, 50f / 255f, 29f / 255f, 0.52f));

            var dragHint = SceneBuilderCore.FindOrCreateChild(selectedPanel.transform, "DragHint");
            SceneBuilderCore.FigmaRect(dragHint, 41f - 18f, 657f - 640f, 330f, 15.6f);
            SceneBuilderCore.SetupText(dragHint, SceneBuilderCore.FontChoice.SpecialElite, 14f, Color.white,
                TextAlignmentOptions.Left, "ui.edit.dragHint", "Click and drag around the item to position it");

            var editNameGo = SceneBuilderCore.FindOrCreateChild(selectedPanel.transform, "EditNameLabel");
            SceneBuilderCore.FigmaRect(editNameGo, 41f - 18f, 685f - 640f, 347f, 40f);
            editNameLabel = SceneBuilderCore.SetupText(editNameGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Left, null, "Selected: ...");

            var editDescGo = SceneBuilderCore.FindOrCreateChild(selectedPanel.transform, "EditDescLabel");
            SceneBuilderCore.FigmaRect(editDescGo, 41f - 18f, 725f - 640f, 347f, 53f);
            editDescLabel = SceneBuilderCore.SetupText(editDescGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Left, null, "Desc.: ...");

            var placeGo = SceneBuilderCore.FindOrCreateChild(selectedPanel.transform, "PlaceButton");
            SceneBuilderCore.FigmaRect(placeGo, 45f - 18f, 796.67f - 640f, 152f, 36.67f);
            SceneBuilderCore.SetColorShape(placeGo, SceneBuilderCore.Hex("d19562"));
            placeButton = SceneBuilderCore.SetupButton(placeGo);
            SceneBuilderCore.SetupAnchor(placeGo, "edit.place");
            var placeLabel = SceneBuilderCore.FindOrCreateChild(placeGo.transform, "Label");
            SceneBuilderCore.Stretch(placeLabel);
            SceneBuilderCore.SetupText(placeLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f, Color.white,
                TextAlignmentOptions.Center, "ui.edit.place", "Place Item");

            var undoGo = SceneBuilderCore.FindOrCreateChild(selectedPanel.transform, "UndoButton");
            SceneBuilderCore.FigmaRect(undoGo, 245f - 18f, 796.67f - 640f, 121f, 36.67f);
            SceneBuilderCore.SetColorShape(undoGo, SceneBuilderCore.Hex("340203"));
            undoButton = SceneBuilderCore.SetupButton(undoGo);
            SceneBuilderCore.SetupAnchor(undoGo, "edit.undo");
            var undoLabel = SceneBuilderCore.FindOrCreateChild(undoGo.transform, "Label");
            SceneBuilderCore.Stretch(undoLabel);
            SceneBuilderCore.SetupText(undoLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f, Color.white,
                TextAlignmentOptions.Center, "ui.edit.undo", "Undo");

            SceneBuilderCore.SetActive(editUi, true);
            SceneBuilderCore.SetActive(editIdleGroup, true);
            SceneBuilderCore.SetActive(editSelectedGroup, true);
            return editUi;
        }

        // ---- PreviewUI ------------------------------------------------------------------

        private static GameObject BuildPreviewUi(Transform screen, out TMP_Text previewNameLabel,
            out TMP_Text previewPriceLabel, out TMP_Text previewDescLabel, out TMP_Text notEnoughCoinsLabel,
            out RectTransform previewCoinsBadge, out Button buyButton, out Button giveUpButton)
        {
            var previewUi = SceneBuilderCore.FindOrCreateChildOrdered(screen, "PreviewUI", 5);
            SceneBuilderCore.Stretch(previewUi);

            // -- PreviewHeader: positioned bar; Title/MoveIcon/PreviewCoinsBadge are
            // nested, so subtract the header's own origin (0,79).
            var header = SceneBuilderCore.FindOrCreateChild(previewUi.transform, "PreviewHeader");
            SceneBuilderCore.FigmaRect(header, 0f, 79f, 413f, 61f);
            SceneBuilderCore.SetColorShape(header, new Color(0f, 0f, 0f, 0.6f));

            var headerTitle = SceneBuilderCore.FindOrCreateChild(header.transform, "Title");
            SceneBuilderCore.FigmaRect(headerTitle, 52f, 98f - 79f, 164f, 24f);
            SceneBuilderCore.SetupText(headerTitle, SceneBuilderCore.FontChoice.SpecialElite, 24f, Color.white,
                TextAlignmentOptions.Left, "ui.preview.title", "Preview Mode");

            var headerMoveIcon = SceneBuilderCore.FindOrCreateChild(header.transform, "MoveIcon");
            SceneBuilderCore.FigmaRect(headerMoveIcon, 225f, 92f - 79f, 38f, 36f);
            SceneBuilderCore.SetImage(headerMoveIcon, "Assets/Art/UI/moveIconLight.png");

            var coinsBadgeGo = SceneBuilderCore.FindOrCreateChild(header.transform, "PreviewCoinsBadge");
            SceneBuilderCore.FigmaRect(coinsBadgeGo, 272f, 83f - 79f, 106f, 53f);
            SceneBuilderCore.SetImage(coinsBadgeGo, "Assets/Art/UI/coinShowerBG.png");
            previewCoinsBadge = SceneBuilderCore.Rect(coinsBadgeGo);

            var coinsLabelGo = SceneBuilderCore.FindOrCreateChild(coinsBadgeGo.transform, "PreviewCoinsLabel");
            SceneBuilderCore.Stretch(coinsLabelGo);
            var coinsText = SceneBuilderCore.SetupText(coinsLabelGo, SceneBuilderCore.FontChoice.Rye, 14f,
                Color.black, TextAlignmentOptions.Center, null, "0");
            var coinsBinding = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.CoinBalanceLabel>(coinsLabelGo);
            var coinsSo = new SerializedObject(coinsBinding);
            SceneBuilderCore.SetField(coinsSo, "label", coinsText, "DeskHubScreen/PreviewCoinsLabel");
            coinsSo.ApplyModifiedProperties();

            // -- PreviewPanel: stretch's sibling, positioned; every field below is
            // nested under it, so subtract its own origin (18,640).
            var panel = SceneBuilderCore.FindOrCreateChild(previewUi.transform, "PreviewPanel");
            SceneBuilderCore.FigmaRect(panel, 18f, 640f, 376f, 220f);
            SceneBuilderCore.SetColorShape(panel, new Color(3f / 255f, 50f / 255f, 29f / 255f, 0.52f));

            var dragHint = SceneBuilderCore.FindOrCreateChild(panel.transform, "DragHint");
            SceneBuilderCore.FigmaRect(dragHint, 41f - 18f, 657f - 640f, 330f, 15.6f);
            SceneBuilderCore.SetupText(dragHint, SceneBuilderCore.FontChoice.SpecialElite, 14f, Color.white,
                TextAlignmentOptions.Left, "ui.preview.dragHint", "Click and drag around the item to position it");

            var nameGo = SceneBuilderCore.FindOrCreateChild(panel.transform, "PreviewNameLabel");
            SceneBuilderCore.FigmaRect(nameGo, 41f - 18f, 685f - 640f, 347f, 31f);
            previewNameLabel = SceneBuilderCore.SetupText(nameGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Left, null, "Selected: ...");

            var priceGo = SceneBuilderCore.FindOrCreateChild(panel.transform, "PreviewPriceLabel");
            SceneBuilderCore.FigmaRect(priceGo, 41f - 18f, 716f - 640f, 347f, 31f);
            previewPriceLabel = SceneBuilderCore.SetupText(priceGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Left, null, "Price: ...");

            var descGo = SceneBuilderCore.FindOrCreateChild(panel.transform, "PreviewDescLabel");
            SceneBuilderCore.FigmaRect(descGo, 41f - 18f, 747f - 640f, 347f, 31f);
            previewDescLabel = SceneBuilderCore.SetupText(descGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Left, null, "Desc.: ...");

            var notEnoughGo = SceneBuilderCore.FindOrCreateChild(panel.transform, "NotEnoughCoinsLabel");
            SceneBuilderCore.FigmaRect(notEnoughGo, 41f - 18f, 778f - 640f, 330f, 15f);
            notEnoughCoinsLabel = SceneBuilderCore.SetupText(notEnoughGo, SceneBuilderCore.FontChoice.SpecialElite,
                13f, SceneBuilderCore.Hex("db4033"), TextAlignmentOptions.Left, null, "Not enough coins yet...");
            SceneBuilderCore.SetActive(notEnoughGo, false);

            var buyGo = SceneBuilderCore.FindOrCreateChild(panel.transform, "BuyButton");
            SceneBuilderCore.FigmaRect(buyGo, 45f - 18f, 796.67f - 640f, 121f, 36.67f);
            SceneBuilderCore.SetColorShape(buyGo, SceneBuilderCore.Hex("620b1a"));
            buyButton = SceneBuilderCore.SetupButton(buyGo);
            SceneBuilderCore.SetupAnchor(buyGo, "preview.buy");
            var buyLabel = SceneBuilderCore.FindOrCreateChild(buyGo.transform, "Label");
            SceneBuilderCore.Stretch(buyLabel);
            SceneBuilderCore.SetupText(buyLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f,
                new Color(0.96f, 0.73f, 0.33f, 0.92f), TextAlignmentOptions.Center, "ui.preview.buy", "Buy Item");

            var giveUpGo = SceneBuilderCore.FindOrCreateChild(panel.transform, "GiveUpButton");
            SceneBuilderCore.FigmaRect(giveUpGo, 245f - 18f, 796.67f - 640f, 121f, 36.67f);
            SceneBuilderCore.SetColorShape(giveUpGo, SceneBuilderCore.Hex("340203"));
            giveUpButton = SceneBuilderCore.SetupButton(giveUpGo);
            SceneBuilderCore.SetupAnchor(giveUpGo, "preview.cancel");
            var giveUpLabel = SceneBuilderCore.FindOrCreateChild(giveUpGo.transform, "Label");
            SceneBuilderCore.Stretch(giveUpLabel);
            SceneBuilderCore.SetupText(giveUpLabel, SceneBuilderCore.FontChoice.SpecialElite, 20f, Color.white,
                TextAlignmentOptions.Center, "ui.preview.giveUp", "Give up");

            SceneBuilderCore.SetActive(previewUi, true);
            return previewUi;
        }
    }
}
