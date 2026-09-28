// ============================================================
// BuildOverlays — the "Overlays" object: Game Paused + Settings modals.
// WHAT & WHY: Builds the object graph OverlayController.cs, PauseMenuOverlay.cs
//   and SettingsOverlay.cs each ask for in their own checklists, per
//   Docs/Batch2/FigmaLayout.md §1 (GamePausedOverlay) and §2 (SettingsScene).
// KEY DECISIONS:
//   - "Overlays" sits under Canvas, sorted at order 30 (above every screen and
//     the tutorial's order-10/11 objects, below CutscenePlayer's order 1000).
//   - Both PauseMenuOverlay and SettingsOverlay start ACTIVE in the hierarchy;
//     OverlayController hides them at runtime (HideImmediate in its own Awake).
//     Leaving them inactive here would mean a ModalOverlay whose Awake/OnEnable
//     never runs until the first Open(), which the scripts do not expect.
//   - Decorative pieces called out as "baked-in" in FigmaLayout.md (borders,
//     sparkle corners, the divider line under the GAME PAUSED title) are not
//     separate objects: gamePausedBG.png already contains them, so the Panel
//     Image for the pause overlay IS that background image.
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
    internal static class BuildOverlays
    {
        public static GameObject Build(Transform canvas, RestoriumEmporium.Core.GameFlowController flow)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "Overlays");
            SceneBuilderCore.Stretch(root);

            var overlayCanvas = SceneBuilderCore.AddOrGet<Canvas>(root);
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 30;
            SceneBuilderCore.AddOrGet<GraphicRaycaster>(root);

            var pauseMenu = BuildPauseMenu(root.transform);
            var settings = BuildSettingsMenu(root.transform);

            var controller = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.OverlayController>(root);
            var so = new SerializedObject(controller);
            SceneBuilderCore.SetField(so, "pauseMenu", pauseMenu, "OverlayController");
            SceneBuilderCore.SetField(so, "settings", settings, "OverlayController");
            SceneBuilderCore.SetField(so, "flow", flow, "OverlayController");
            SceneBuilderCore.SetField(so, "handleBackKey", true, "OverlayController");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return root;
        }

        // ---- PauseMenuOverlay (Figma GamePausedOverlay 283:177) ----------------------------

        private static RestoriumEmporium.UI.PauseMenuOverlay BuildPauseMenu(Transform overlays)
        {
            var root = SceneBuilderCore.FindOrCreateChild(overlays, "PauseMenuOverlay");
            SceneBuilderCore.Stretch(root);
            SceneBuilderCore.AddOrGet<CanvasGroup>(root);

            var scrim = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Scrim", 0);
            SceneBuilderCore.Stretch(scrim);
            SceneBuilderCore.SetColorShape(scrim, new Color(0.118f, 0.035f, 0.047f, 0.8f), raycastTarget: true);

            var panel = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Panel", 1);
            SceneBuilderCore.FigmaRect(panel, 13f, 16f, 386f, 885f, 0.5f, 0.5f);
            SceneBuilderCore.SetImage(panel, "Assets/Art/GamePausedOverlay/gamePausedBG.png");

            var title = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Title", 2);
            SceneBuilderCore.FigmaRect(title, 98f, 113f, 216f, 30f);
            SceneBuilderCore.SetupText(title, SceneBuilderCore.FontChoice.Rye, 24f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.pause.title", "GAME PAUSED");

            var flavour = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Flavour", 3);
            SceneBuilderCore.FigmaRect(flavour, 50f, 743f, 311f, 47f);
            SceneBuilderCore.SetupText(flavour, SceneBuilderCore.FontChoice.SpecialElite, 16f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.pause.flavour",
                "Take your time,\nMysteries that lie behind these papers shall wait for your return.");

            var journalButton = BuildPauseRow(root.transform, "JournalButton", 4, 128f, 563f, 156f, 20f,
                "ui.pause.journal", "Journal");
            var settingsButton = BuildPauseRow(root.transform, "SettingsButton", 5, 98f, 601f, 216f, 20f,
                "ui.pause.settings", "Settings");
            var quitButton = BuildPauseRow(root.transform, "QuitButton", 6, 98f, 639f, 216f, 20f,
                "ui.pause.quit", "Quit");

            var overlay = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.PauseMenuOverlay>(root);
            var so = new SerializedObject(overlay);
            SceneBuilderCore.SetField(so, "journalButton", journalButton, "PauseMenuOverlay");
            SceneBuilderCore.SetField(so, "settingsButton", settingsButton, "PauseMenuOverlay");
            SceneBuilderCore.SetField(so, "quitButton", quitButton, "PauseMenuOverlay");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return overlay;
        }

        private static Button BuildPauseRow(Transform parent, string name, int sibling, float x, float y,
            float w, float h, string key, string placeholder)
        {
            var go = SceneBuilderCore.FindOrCreateChildOrdered(parent, name, sibling);
            SceneBuilderCore.FigmaRect(go, x, y, w, h);
            var btn = SceneBuilderCore.SetupButton(go);

            var labelGo = SceneBuilderCore.FindOrCreateChildOrdered(go.transform, "Label", 0);
            SceneBuilderCore.Stretch(labelGo);
            SceneBuilderCore.SetupText(labelGo, SceneBuilderCore.FontChoice.SpecialElite, 20f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, key, placeholder);

            return btn;
        }

        // ---- SettingsOverlay (Figma SettingsScene 300:298) ---------------------------------

        private static RestoriumEmporium.UI.SettingsOverlay BuildSettingsMenu(Transform overlays)
        {
            var root = SceneBuilderCore.FindOrCreateChild(overlays, "SettingsOverlay");
            SceneBuilderCore.Stretch(root);
            SceneBuilderCore.AddOrGet<CanvasGroup>(root);

            var scrim = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Scrim", 0);
            SceneBuilderCore.Stretch(scrim);
            SceneBuilderCore.SetColorShape(scrim, new Color(0f, 0f, 0f, 0.6f), raycastTarget: true);

            var bookshelf = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Bookshelf", 1);
            SceneBuilderCore.FigmaRect(bookshelf, -62f, 259f, 523f, 800f);
            SceneBuilderCore.SetImage(bookshelf, "Assets/Art/Settings/bookshelfBG.png");

            var outerBorder = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "OuterBorder", 2);
            SceneBuilderCore.FigmaRect(outerBorder, 13f, 16f, 386f, 885f);
            var outerImg = SceneBuilderCore.AddOrGet<Image>(outerBorder);
            outerImg.sprite = null;
            outerImg.color = new Color(0f, 0f, 0f, 0f);
            var outerOutline = SceneBuilderCore.AddOrGet<Outline>(outerBorder);
            outerOutline.effectColor = SceneBuilderCore.Hex("c7c8bc");
            outerOutline.effectDistance = new Vector2(1f, -1f);

            var portrait = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "TracyPortrait", 3);
            SceneBuilderCore.FigmaRect(portrait, 141f, 74f, 130f, 164f);
            SceneBuilderCore.SetImage(portrait, "Assets/Art/Settings/tracyPortrait.png");

            var title = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Title", 4);
            SceneBuilderCore.FigmaRect(title, 100f, 254f, 212f, 45f);
            SceneBuilderCore.SetupText(title, SceneBuilderCore.FontChoice.Rye, 36f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.settings.title", "SETTINGS");

            var backButton = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "BackButton", 5);
            SceneBuilderCore.FigmaRect(backButton, 29f, 39f, 58.6f, 55f);
            var backImg = SceneBuilderCore.SetImage(backButton, "Assets/Art/UI/backArrowIcon.png");
            backButton.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
            var back = SceneBuilderCore.SetupButton(backButton);

            var flavour = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Flavour", 6);
            SceneBuilderCore.FigmaRect(flavour, 54f, 365f, 311f, 28f);
            SceneBuilderCore.SetupText(flavour, SceneBuilderCore.FontChoice.SpecialElite, 16f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.settings.tracyLine",
                "Oh, the restorium needs adjusting?\nMy bad, let's change it right away!");

            var languageRow = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "LanguageRow", 7);
            SceneBuilderCore.Stretch(languageRow);
            var languageLabel = SceneBuilderCore.FindOrCreateChildOrdered(languageRow.transform, "Label", 0);
            SceneBuilderCore.FigmaRect(languageLabel, 107f, 440f, 71f, 15f);
            SceneBuilderCore.SetupText(languageLabel, SceneBuilderCore.FontChoice.SpecialElite, 15f, Color.white,
                TextAlignmentOptions.Left, "ui.settings.language", "Language");

            var languageButtonGo = SceneBuilderCore.FindOrCreateChildOrdered(languageRow.transform,
                "LanguageButton", 1);
            SceneBuilderCore.FigmaRect(languageButtonGo, 200f, 434f, 112f, 26f);
            SceneBuilderCore.SetColorShape(languageButtonGo, Color.black);
            var langOutline = SceneBuilderCore.AddOrGet<Outline>(languageButtonGo);
            langOutline.effectColor = SceneBuilderCore.Hex("c7c8bc");
            var languageButton = SceneBuilderCore.SetupButton(languageButtonGo);
            var languageValueGo = SceneBuilderCore.FindOrCreateChildOrdered(languageButtonGo.transform, "Value", 0);
            SceneBuilderCore.Stretch(languageValueGo);
            var languageValueLabel = SceneBuilderCore.SetupText(languageValueGo,
                SceneBuilderCore.FontChoice.SpecialElite, 15f, Color.white, TextAlignmentOptions.Center, null,
                "English");

            var sfxRow = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "SfxRow", 8);
            SceneBuilderCore.Stretch(sfxRow);
            var sfxLabel = SceneBuilderCore.FindOrCreateChildOrdered(sfxRow.transform, "Label", 0);
            SceneBuilderCore.FigmaRect(sfxLabel, 81f, 503f, 76f, 15f);
            SceneBuilderCore.SetupText(sfxLabel, SceneBuilderCore.FontChoice.SpecialElite, 15f, Color.white,
                TextAlignmentOptions.Left, "ui.settings.sfx", "SFX Audio");
            var sfxSlider = BuildSlider(sfxRow.transform, "SfxSlider", 178f, 501f, 156f, 20f);

            var musicRow = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "MusicRow", 9);
            SceneBuilderCore.Stretch(musicRow);
            var musicLabel = SceneBuilderCore.FindOrCreateChildOrdered(musicRow.transform, "Label", 0);
            SceneBuilderCore.FigmaRect(musicLabel, 72f, 564f, 94f, 15f);
            SceneBuilderCore.SetupText(musicLabel, SceneBuilderCore.FontChoice.SpecialElite, 15f, Color.white,
                TextAlignmentOptions.Left, "ui.settings.music", "Music Audio");
            var musicSlider = BuildSlider(musicRow.transform, "MusicSlider", 178f, 562f, 156f, 20f);

            var overlay = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.SettingsOverlay>(root);
            var so = new SerializedObject(overlay);
            SceneBuilderCore.SetField(so, "backButton", back, "SettingsOverlay");
            SceneBuilderCore.SetField(so, "languageButton", languageButton, "SettingsOverlay");
            SceneBuilderCore.SetField(so, "languageValueLabel", languageValueLabel, "SettingsOverlay");
            SceneBuilderCore.SetField(so, "sfxSlider", sfxSlider, "SettingsOverlay");
            SceneBuilderCore.SetField(so, "musicSlider", musicSlider, "SettingsOverlay");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return overlay;
        }

        private static Slider BuildSlider(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = SceneBuilderCore.FindOrCreateChild(parent, name);
            SceneBuilderCore.FigmaRect(go, x, y, w, h);
            var slider = SceneBuilderCore.AddOrGet<Slider>(go);
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var track = SceneBuilderCore.FindOrCreateChildOrdered(go.transform, "Track", 0);
            SceneBuilderCore.Stretch(track);
            SceneBuilderCore.SetColorShape(track, Color.black);

            var fillArea = SceneBuilderCore.FindOrCreateChildOrdered(go.transform, "Fill Area", 1);
            SceneBuilderCore.Stretch(fillArea);
            var fill = SceneBuilderCore.FindOrCreateChildOrdered(fillArea.transform, "Fill", 0);
            SceneBuilderCore.Stretch(fill);
            var fillImage = SceneBuilderCore.SetColorShape(fill, new Color(0.78f, 0.784f, 0.737f, 0.76f));
            slider.fillRect = SceneBuilderCore.Rect(fill);

            var handleArea = SceneBuilderCore.FindOrCreateChildOrdered(go.transform, "Handle Slide Area", 2);
            SceneBuilderCore.Stretch(handleArea);
            var handle = SceneBuilderCore.FindOrCreateChildOrdered(handleArea.transform, "Handle", 0);
            var handleRect = SceneBuilderCore.Rect(handle);
            handleRect.sizeDelta = new Vector2(20f, 20f);
            SceneBuilderCore.SetColorShape(handle, SceneBuilderCore.Hex("d9d9d9"));
            slider.handleRect = handleRect;
            slider.targetGraphic = handle.GetComponent<Image>();

            return slider;
        }
    }
}
