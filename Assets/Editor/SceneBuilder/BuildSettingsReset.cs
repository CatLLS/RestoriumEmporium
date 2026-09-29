// ============================================================
// BuildSettingsReset — "Restorium/Scene/Build Settings Reset": the red Reset
//   Progress button and its confirm panel inside the Settings overlay ONLY.
// WHAT & WHY: Lets a tester (or player) wipe the save from the device, where
//   deleting save.json by hand is not practical. Game.unity has hand edits made
//   after the Batch 2 builder ran, so this is deliberately NOT part of "Build
//   Batch 2 Scene Objects" and touches nothing else.
// KEY DECISIONS:
//   - Scope: creates/refreshes only two children of Canvas/Overlays/
//     SettingsOverlay (ResetButton, ResetConfirm). Outside them, exactly four
//     serialized fields on SettingsOverlay are set, and only when they are
//     empty or already point here. No other object is moved, restyled or
//     re-wired.
//   - ResetConfirm is kept as the LAST child so it draws over every row, and
//     its full-screen scrim swallows taps meant for the controls underneath.
//   - Not in Figma. Placed under the Music row (y 562) in the same 412x917
//     design space; button art is Art/UI/redButton.png, the coin-pack button.
//   - Copy is the ui.settings.reset* group already in LocaleSource.UI.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Assets/Scenes/Game.unity (not in Play mode).
// [ ] Run Restorium -> Scene -> Build Settings Reset. Save when asked.
// ---------------------------------------------------------------

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RestoriumEmporium.EditorTools
{
    public static class BuildSettingsReset
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string RedButtonPath = "Assets/Art/UI/redButton.png";

        private static readonly Color Parchment = SceneBuilderCore.Hex("c7c8bc");

        [MenuItem("Restorium/Scene/Build Settings Reset")]
        public static void BuildMenu()
        {
            var gameScene = SceneManager.GetSceneByPath(GameScenePath);

            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Build Settings Reset",
                    $"Open '{GameScenePath}' first, then run this menu again.", "OK");
                return;
            }

            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Build Settings Reset", "Exit Play mode first.", "OK");
                return;
            }

            var canvas = GameObject.Find("Canvas");
            var overlaysT = canvas != null ? canvas.transform.Find("Overlays") : null;
            var settingsT = overlaysT != null ? overlaysT.Find("SettingsOverlay") : null;
            var settings = settingsT != null ? settingsT.GetComponent<RestoriumEmporium.UI.SettingsOverlay>() : null;

            if (settings == null)
            {
                EditorUtility.DisplayDialog("Build Settings Reset",
                    "Could not find Canvas/Overlays/SettingsOverlay (with SettingsOverlay) in the open scene. " +
                    "This menu only ADDS the reset button to it; it will not create or change it.", "OK");
                return;
            }

            SceneBuilderCore.ResetReport();
            Undo.SetCurrentGroupName("Build Settings Reset");
            var undoGroup = Undo.GetCurrentGroup();

            try
            {
                Build(settings);
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

            body += "\nOnly SettingsOverlay/ResetButton and SettingsOverlay/ResetConfirm were built, and " +
                    "SettingsOverlay's four Reset Progress fields were set. Nothing else in the scene was " +
                    "changed.\n\nSave the Game scene now?";

            if (EditorUtility.DisplayDialog("Build Settings Reset — done", body, "Save scene", "Don't save yet"))
            {
                EditorSceneManager.SaveScene(gameScene);
            }

            Debug.Log($"[SceneBuilder] Build Settings Reset finished: created {SceneBuilderCore.Created.Count}, " +
                      $"{SceneBuilderCore.Problems.Count} problem(s).");
        }

        private static void Build(RestoriumEmporium.UI.SettingsOverlay settings)
        {
            var t = settings.transform;

            // ---- The red button, under the Music row ----
            var resetGo = SceneBuilderCore.FindOrCreateChild(t, "ResetButton");
            SceneBuilderCore.FigmaRect(resetGo, 96f, 630f, 220f, 55f);
            SceneBuilderCore.SetImage(resetGo, RedButtonPath, raycastTarget: true);
            var resetButton = SceneBuilderCore.SetupButton(resetGo);
            BuildLabel(resetGo.transform, "ui.settings.resetProgress", "Reset progress", 20f);

            // ---- Confirm panel, last child so it covers everything ----
            var confirm = SceneBuilderCore.FindOrCreateChild(t, "ResetConfirm");
            SceneBuilderCore.Stretch(confirm);
            confirm.transform.SetAsLastSibling();
            var ct = confirm.transform;

            var scrim = SceneBuilderCore.FindOrCreateChildOrdered(ct, "Scrim", 0);
            SceneBuilderCore.Stretch(scrim);
            SceneBuilderCore.SetColorShape(scrim, new Color(0f, 0f, 0f, 0.6f), raycastTarget: true);

            var box = SceneBuilderCore.FindOrCreateChildOrdered(ct, "Box", 1);
            SceneBuilderCore.FigmaRect(box, 36f, 330f, 340f, 250f);
            SceneBuilderCore.SetColorShape(box, new Color(0f, 0f, 0f, 0.9f), raycastTarget: true);
            var boxOutline = SceneBuilderCore.AddOrGet<Outline>(box);
            boxOutline.effectColor = Parchment;
            boxOutline.effectDistance = new Vector2(1f, -1f);

            var message = SceneBuilderCore.FindOrCreateChildOrdered(ct, "Message", 2);
            SceneBuilderCore.FigmaRect(message, 56f, 350f, 300f, 130f);
            var messageText = SceneBuilderCore.SetupText(message, SceneBuilderCore.FontChoice.SpecialElite, 18f,
                Parchment, TextAlignmentOptions.Center, "ui.settings.resetConfirm",
                "Erase all progress? Your coins, decorations and restored posters will be lost.");
            AutoSize(messageText, 12f, 18f);

            var yesGo = SceneBuilderCore.FindOrCreateChildOrdered(ct, "YesButton", 3);
            SceneBuilderCore.FigmaRect(yesGo, 56f, 500f, 140f, 55f);
            SceneBuilderCore.SetImage(yesGo, RedButtonPath, raycastTarget: true);
            var yesButton = SceneBuilderCore.SetupButton(yesGo);
            BuildLabel(yesGo.transform, "ui.settings.resetYes", "Erase", 20f);

            var noGo = SceneBuilderCore.FindOrCreateChildOrdered(ct, "NoButton", 4);
            SceneBuilderCore.FigmaRect(noGo, 216f, 500f, 140f, 55f);
            SceneBuilderCore.SetColorShape(noGo, Color.black, raycastTarget: true);
            var noOutline = SceneBuilderCore.AddOrGet<Outline>(noGo);
            noOutline.effectColor = Parchment;
            var noButton = SceneBuilderCore.SetupButton(noGo);
            BuildLabel(noGo.transform, "ui.settings.resetNo", "Cancel", 20f);

            // Hidden in the scene; SettingsOverlay also hides it on every open/close.
            SceneBuilderCore.SetActive(confirm, false);

            WireIfFree(settings, "resetButton", resetButton);
            WireIfFree(settings, "resetConfirmPanel", confirm);
            WireIfFree(settings, "resetYesButton", yesButton);
            WireIfFree(settings, "resetNoButton", noButton);
        }

        private static void BuildLabel(Transform button, string key, string placeholder, float size)
        {
            var labelGo = SceneBuilderCore.FindOrCreateChild(button, "Label");
            SceneBuilderCore.Stretch(labelGo);
            var label = SceneBuilderCore.SetupText(labelGo, SceneBuilderCore.FontChoice.SpecialElite, size, Parchment,
                TextAlignmentOptions.Center, key, placeholder);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            AutoSize(label, 12f, size);
        }

        /// <summary>Sets one object reference only if it is empty or already this value.</summary>
        private static void WireIfFree(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);

            if (p == null)
            {
                SceneBuilderCore.Problem($"[SettingsOverlay] has no field '{field}'. Update BuildSettingsReset.cs.");
                return;
            }

            if (p.objectReferenceValue != null && p.objectReferenceValue != value)
            {
                SceneBuilderCore.Problem($"[SettingsOverlay] '{field}' already points at " +
                                         $"'{p.objectReferenceValue.name}'; left unchanged.");
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void AutoSize(TMP_Text text, float min, float max)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
        }
    }
}
