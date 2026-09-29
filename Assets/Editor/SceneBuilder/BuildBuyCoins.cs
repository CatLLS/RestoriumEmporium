// ============================================================
// BuildBuyCoins — "Restorium/Scene/Build Coin Shop": The Golden Vault overlay ONLY.
// WHAT & WHY: Builds Canvas/Overlays/BuyCoinsOverlay (Figma BuyCoins 623:26,
//   Docs/Batch2/FigmaLayout.md section 13) and wires it to the Shop's "Buy More
//   Coins" button. The Game scene has hand edits made after the Batch 2 builder
//   ran, so this is deliberately NOT part of "Build Batch 2 Scene Objects" and
//   touches nothing else.
// KEY DECISIONS:
//   - Scope: creates/refreshes only the BuyCoinsOverlay subtree. Outside it,
//     exactly two serialized references are set: OverlayController.buyCoins and
//     ShopScreen.overlays, and only when they are empty or already point here.
//     No other object is moved, re-sorted, restyled or re-wired.
//   - Canvas/Overlays and Canvas/ShopScreen must already exist; if either is
//     missing the menu stops with a dialog instead of creating one.
//   - Idempotent by name (SceneBuilderCore.FindOrCreateChild), one Undo group,
//     asks before saving, same as the Batch 2 menu.
//   - Figma deviations, on purpose:
//       * Pack buttons use Art/UI/redButton.png, which matches the Figma render.
//         (FigmaLayout.md names newGameButton.png, but that has "New Game" baked in.)
//       * Close "X" uses LiberationSans (TMP default): no Quicksand TMP asset exists
//         and LiberationSans is the closest thin sans in the project.
//       * Both sparkles use starGold.png; the Figma render shows both gold.
//       * "Terms | Privacy | Remove Ads" is split into three tappable labels.
//       * A Status line (not in Figma) sits under the packs for failed purchases.
//       * The plant uses the Figma design-context position (left 295), which
//         matches the render; FigmaLayout.md's 328.5 is the unrotated box.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Assets/Scenes/Game.unity (not in Play mode).
// [ ] Run Restorium -> Scene -> Build Coin Shop. Save when asked.
// ---------------------------------------------------------------

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RestoriumEmporium.EditorTools
{
    public static class BuildBuyCoins
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string ConfigPath = "Assets/Data/Config/RevenueCatConfig.asset";
        private const string LiberationSansPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static readonly Color Parchment = SceneBuilderCore.Hex("c7c8bc");
        private static readonly Color Gold = SceneBuilderCore.Hex("f5ba55");

        [MenuItem("Restorium/Scene/Build Coin Shop")]
        public static void BuildMenu()
        {
            var gameScene = SceneManager.GetSceneByPath(GameScenePath);

            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Build Coin Shop",
                    $"Open '{GameScenePath}' first, then run this menu again.", "OK");
                return;
            }

            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Build Coin Shop", "Exit Play mode first.", "OK");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            var overlaysT = canvas != null ? canvas.transform.Find("Overlays") : null;
            var controller = overlaysT != null ? overlaysT.GetComponent<RestoriumEmporium.UI.OverlayController>() : null;
            var shopT = canvas != null ? canvas.transform.Find("ShopScreen") : null;
            var shop = shopT != null ? shopT.GetComponent<RestoriumEmporium.UI.ShopScreen>() : null;

            if (controller == null || shop == null)
            {
                EditorUtility.DisplayDialog("Build Coin Shop",
                    "Could not find Canvas/Overlays (with OverlayController) and Canvas/ShopScreen (with " +
                    "ShopScreen) in the open scene. This menu only ADDS the coin overlay to them; it will not " +
                    "create or change them.", "OK");
                return;
            }

            SceneBuilderCore.ResetReport();
            Undo.SetCurrentGroupName("Build Coin Shop");
            var undoGroup = Undo.GetCurrentGroup();

            try
            {
                var overlay = Build(overlaysT);
                WireIfFree(controller, "buyCoins", overlay, "OverlayController");
                WireIfFree(shop, "overlays", controller, "ShopScreen");
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            EditorSceneManager.MarkSceneDirty(gameScene);

            var body = $"Created: {SceneBuilderCore.Created.Count}\nProblems: {SceneBuilderCore.Problems.Count}\n\n";

            for (var i = 0; i < SceneBuilderCore.Problems.Count && i < 15; i++)
            {
                body += "- " + SceneBuilderCore.Problems[i] + "\n";
            }

            body += "\nOnly Overlays/BuyCoinsOverlay was built; OverlayController.buyCoins and " +
                    "ShopScreen.overlays were set. Nothing else in the scene was changed.\n\nSave the Game scene now?";

            if (EditorUtility.DisplayDialog("Build Coin Shop — done", body, "Save scene", "Don't save yet"))
            {
                EditorSceneManager.SaveScene(gameScene);
            }

            Debug.Log($"[SceneBuilder] Build Coin Shop finished: created {SceneBuilderCore.Created.Count}, " +
                      $"{SceneBuilderCore.Problems.Count} problem(s).");
        }

        /// <summary>Sets one object reference only if it is empty or already this value.</summary>
        private static void WireIfFree(Object target, string field, Object value, string context)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);

            if (p == null)
            {
                SceneBuilderCore.Problem($"[{context}] has no field '{field}'. Update BuildBuyCoins.cs.");
                return;
            }

            if (p.objectReferenceValue != null && p.objectReferenceValue != value)
            {
                SceneBuilderCore.Problem($"[{context}] '{field}' already points at " +
                                         $"'{p.objectReferenceValue.name}'; left unchanged. Clear it and re-run " +
                                         "to wire the coin shop.");
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static RestoriumEmporium.UI.BuyCoinsOverlay Build(Transform overlays)
        {
            var root = SceneBuilderCore.FindOrCreateChild(overlays, "BuyCoinsOverlay");
            SceneBuilderCore.Stretch(root);
            SceneBuilderCore.AddOrGet<CanvasGroup>(root);
            var t = root.transform;
            var i = 0;

            // Behind everything: covers screens taller than the 412x917 design.
            var backdrop = SceneBuilderCore.FindOrCreateChildOrdered(t, "Backdrop", i++);
            SceneBuilderCore.Stretch(backdrop);
            SceneBuilderCore.SetColorShape(backdrop, SceneBuilderCore.Hex("1e090c"), raycastTarget: true);

            var bg = SceneBuilderCore.FindOrCreateChildOrdered(t, "Background", i++);
            SceneBuilderCore.FigmaRect(bg, -76f, 0f, 523f, 930f);
            SceneBuilderCore.SetImage(bg, "Assets/Art/Monetization/bookshelfBG.png");

            var tracy = SceneBuilderCore.FindOrCreateChildOrdered(t, "Tracy", i++);
            SceneBuilderCore.FigmaRect(tracy, 46f, 72f, 200f, 223f);
            SceneBuilderCore.SetImage(tracy, "Assets/Art/Monetization/tracyCoins.png").preserveAspect = true;

            var starLarge = SceneBuilderCore.FindOrCreateChildOrdered(t, "StarLarge", i++);
            SceneBuilderCore.FigmaRect(starLarge, 211f, 85f, 45f, 59f);
            SceneBuilderCore.SetImage(starLarge, "Assets/Art/UI/starGold.png");

            var starSmall = SceneBuilderCore.FindOrCreateChildOrdered(t, "StarSmall", i++);
            SceneBuilderCore.FigmaRect(starSmall, 256f, 222f, 22f, 32f);
            SceneBuilderCore.SetImage(starSmall, "Assets/Art/UI/starGold.png");

            var panel = SceneBuilderCore.FindOrCreateChildOrdered(t, "Panel", i++);
            SceneBuilderCore.FigmaRect(panel, 18f, 295f, 356f, 540f);
            SceneBuilderCore.SetColorShape(panel, new Color(0f, 0f, 0f, 0.59f));

            var title = SceneBuilderCore.FindOrCreateChildOrdered(t, "Title", i++);
            SceneBuilderCore.FigmaRect(title, 26f, 318f, 340f, 44f);
            var titleText = SceneBuilderCore.SetupText(title, SceneBuilderCore.FontChoice.Rye, 32f, Gold,
                TextAlignmentOptions.Center, "ui.coins.title", "The Golden Vault");
            AutoSize(titleText, 20f, 32f);

            var rows = new[]
            {
                BuildRow(t, "PackRow1", i++, 409f),
                BuildRow(t, "PackRow2", i++, 519f),
                BuildRow(t, "PackRow3", i++, 629f),
            };

            var status = SceneBuilderCore.FindOrCreateChildOrdered(t, "Status", i++);
            SceneBuilderCore.FigmaRect(status, 32f, 693f, 329f, 20f);
            var statusText = SceneBuilderCore.SetupText(status, SceneBuilderCore.FontChoice.SpecialElite, 13f, Gold,
                TextAlignmentOptions.Center, null, string.Empty);

            var finePrint = SceneBuilderCore.FindOrCreateChildOrdered(t, "FinePrint", i++);
            SceneBuilderCore.FigmaRect(finePrint, 32f, 716f, 329f, 28f);
            SceneBuilderCore.SetupText(finePrint, SceneBuilderCore.FontChoice.SpecialElite, 14f, Parchment,
                TextAlignmentOptions.Center, "ui.coins.finePrint",
                "charged to your account. Restore purchases any time from Settings");

            var terms = BuildLink(t, "TermsButton", i++, 58f, 62f, "ui.coins.terms", "Terms");
            BuildSeparator(t, "Separator1", i++, 124f);
            var privacy = BuildLink(t, "PrivacyButton", i++, 142f, 84f, "ui.coins.privacy", "Privacy");
            BuildSeparator(t, "Separator2", i++, 230f);
            var removeAds = BuildLink(t, "RemoveAdsButton", i++, 248f, 104f, "ui.coins.removeAds", "Remove Ads");

            // Figma 633:119: 89x118 image rotated 16.5 degrees clockwise, centred at (354.5, 842.2).
            var plant = SceneBuilderCore.FindOrCreateChildOrdered(t, "Plant", i++);
            SceneBuilderCore.FigmaRect(plant, 354.5f - 44.544f, 842.18f - 58.952f, 89.088f, 117.904f, 0.5f, 0.5f);
            SceneBuilderCore.SetImage(plant, "Assets/Art/ShopItems/plant/placed.png").preserveAspect = true;
            plant.transform.localEulerAngles = new Vector3(0f, 0f, -16.51f);

            var pill = SceneBuilderCore.FindOrCreateChildOrdered(t, "BalancePill", i++);
            SceneBuilderCore.FigmaRect(pill, 282f, 32f, 106f, 53f);
            SceneBuilderCore.SetImage(pill, "Assets/Art/UI/coinShowerBG.png");
            var balance = SceneBuilderCore.FindOrCreateChild(pill.transform, "BalanceLabel");
            SceneBuilderCore.FigmaRect(balance, 42f, 9f, 56f, 34f);
            var balanceText = SceneBuilderCore.SetupText(balance, SceneBuilderCore.FontChoice.Rye, 14f, Color.black,
                TextAlignmentOptions.Center, null, "120");
            AutoSize(balanceText, 9f, 14f);
            balanceText.textWrappingMode = TextWrappingModes.NoWrap;
            var balanceBinding = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.CoinBalanceLabel>(balance);
            var balanceSo = new SerializedObject(balanceBinding);
            SceneBuilderCore.SetField(balanceSo, "label", balanceText, "BuyCoinsOverlay/BalanceLabel");
            balanceSo.ApplyModifiedProperties();

            // Figma 623:33 is a 15x30 glyph at (24,35); the hit area is a thumb-sized box around it.
            var close = SceneBuilderCore.FindOrCreateChildOrdered(t, "CloseButton", i++);
            SceneBuilderCore.FigmaRect(close, 8f, 26f, 48f, 48f);
            SceneBuilderCore.SetColorShape(close, new Color(0f, 0f, 0f, 0f), raycastTarget: true);
            var closeButton = SceneBuilderCore.SetupButton(close);
            var closeLabel = SceneBuilderCore.FindOrCreateChild(close.transform, "Label");
            SceneBuilderCore.Stretch(closeLabel);
            var closeText = SceneBuilderCore.SetupText(closeLabel, SceneBuilderCore.FontChoice.SpecialElite, 24f,
                Color.white, TextAlignmentOptions.Center, null, "X");
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationSansPath);

            if (liberation != null)
            {
                closeText.font = liberation;
            }

            closeButton.targetGraphic = closeText;

            var overlay = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.BuyCoinsOverlay>(root);
            var so = new SerializedObject(overlay);
            SceneBuilderCore.SetField(so, "closeButton", closeButton, "BuyCoinsOverlay");
            SceneBuilderCore.SetFieldArray(so, "rows", rows, "BuyCoinsOverlay");
            SceneBuilderCore.SetField(so, "termsButton", terms, "BuyCoinsOverlay");
            SceneBuilderCore.SetField(so, "privacyButton", privacy, "BuyCoinsOverlay");
            SceneBuilderCore.SetField(so, "removeAdsButton", removeAds, "BuyCoinsOverlay");
            SceneBuilderCore.SetField(so, "statusLabel", statusText, "BuyCoinsOverlay");
            SceneBuilderCore.SetField(so, "config",
                SceneBuilderCore.LoadAsset<RestoriumEmporium.Economy.RevenueCatConfig>(ConfigPath, "RevenueCatConfig"),
                "BuyCoinsOverlay");
            so.ApplyModifiedProperties();

            // Active in the scene; OverlayController hides it at runtime (HideImmediate).
            SceneBuilderCore.SetActive(root, true);
            return overlay;
        }

        /// <summary>One pack button (Figma newGameButton group): 283x61 at x 55.</summary>
        private static RestoriumEmporium.UI.CoinPackRow BuildRow(Transform parent, string name, int sibling, float y)
        {
            var go = SceneBuilderCore.FindOrCreateChildOrdered(parent, name, sibling);
            SceneBuilderCore.FigmaRect(go, 55f, y, 283f, 61f);
            SceneBuilderCore.SetImage(go, "Assets/Art/UI/redButton.png", raycastTarget: true);
            var button = SceneBuilderCore.SetupButton(go);

            // Figma Theme=3D coin: 26x23 at (70, y+19) -> local (15, 19).
            var icon = SceneBuilderCore.FindOrCreateChildOrdered(go.transform, "CoinIcon", 0);
            SceneBuilderCore.FigmaRect(icon, 15f, 19f, 26f, 23f);
            SceneBuilderCore.SetImage(icon, "Assets/Art/UI/coinIcon.png").preserveAspect = true;

            var labelGo = SceneBuilderCore.FindOrCreateChildOrdered(go.transform, "Label", 1);
            SceneBuilderCore.FigmaRect(labelGo, 42f, 4f, 232f, 53f);
            var label = SceneBuilderCore.SetupText(labelGo, SceneBuilderCore.FontChoice.SpecialElite, 24f, Parchment,
                TextAlignmentOptions.Center, null, "<color=#f5ba55>300</color> coins | $ 0.99");
            label.richText = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(label, 14f, 24f);

            var row = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.CoinPackRow>(go);
            var so = new SerializedObject(row);
            SceneBuilderCore.SetField(so, "button", button, name);
            SceneBuilderCore.SetField(so, "label", label, name);
            so.ApplyModifiedProperties();
            return row;
        }

        /// <summary>A text-only link in the Figma "Terms | Privacy | Remove Ads" row (y 770).</summary>
        private static Button BuildLink(Transform parent, string name, int sibling, float x, float w, string key,
            string placeholder)
        {
            var go = SceneBuilderCore.FindOrCreateChildOrdered(parent, name, sibling);
            SceneBuilderCore.FigmaRect(go, x, 770f, w, 28f);
            SceneBuilderCore.SetColorShape(go, new Color(0f, 0f, 0f, 0f), raycastTarget: true);
            var button = SceneBuilderCore.SetupButton(go);

            var labelGo = SceneBuilderCore.FindOrCreateChild(go.transform, "Label");
            SceneBuilderCore.Stretch(labelGo);
            var label = SceneBuilderCore.SetupText(labelGo, SceneBuilderCore.FontChoice.SpecialElite, 16f, Parchment,
                TextAlignmentOptions.Center, key, placeholder);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            AutoSize(label, 10f, 16f);

            // Tint the words, not the invisible hit box, so a disabled link visibly dims.
            button.targetGraphic = label;
            return button;
        }

        private static void BuildSeparator(Transform parent, string name, int sibling, float x)
        {
            var go = SceneBuilderCore.FindOrCreateChildOrdered(parent, name, sibling);
            SceneBuilderCore.FigmaRect(go, x, 770f, 14f, 28f);
            SceneBuilderCore.SetupText(go, SceneBuilderCore.FontChoice.SpecialElite, 16f, Parchment,
                TextAlignmentOptions.Center, null, "|");
        }

        private static void AutoSize(TMP_Text text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }
    }
}
