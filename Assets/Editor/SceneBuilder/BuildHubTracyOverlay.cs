// ============================================================
// BuildHubTracyOverlay — the full-body Tracy dialogue view used in the desk hub.
// WHAT & WHY: TracyOverlayView.cs's own checklist has a SECOND block ("SECOND
//   INSTANCE — HubTracyOverlay") for exactly this: the deskhub_lamp tutorial
//   sequence's TracyPresentation.HubFullBody steps show a full-body Tracy over
//   the desk hub instead of the portrait+dialogue-box overlay every restoration
//   screen uses. Per CONSISTENCY.md's dispute (a) resolution, catBoard.png
//   belongs HERE, not on DeskHubScreen: only the Figma "Desk_Hub" frame (the
//   dialogue frame) shows the corkboard, the plain DeskHubScreen states
//   (enteredEditMode / PreviewMode) do not.
// KEY DECISIONS:
//   - Uses Mood OBJECTS (three positioned Images: TracyStill / TracyHappy /
//     TracyEmbarrassed), not Mood Sprites — TracyOverlayView.cs's own comment
//     explains why: the full-body poses differ in size/position, so each is its
//     own positioned Image rather than one Image with a swapped sprite.
//   - Sorting order 10, same as the portrait TracyHelpOverlay: only one of the
//     two overlay instances is ever visible at a time (TutorialController
//     switches which one it Shows()), so there is no stacking conflict to sort
//     against between them, but both must beat the tool bar's order 2 and sit
//     below HelpingHand (order 11) and Overlays (order 30).
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
    internal static class BuildHubTracyOverlay
    {
        public static RestoriumEmporium.Tutorial.TracyOverlayView Build(Transform canvas)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "HubTracyOverlay");
            SceneBuilderCore.Stretch(root);

            var scrim = SceneBuilderCore.SetImage(root, null, raycastTarget: true,
                color: new Color(0f, 0f, 0f, 0.25f));

            var overlayCanvas = SceneBuilderCore.AddOrGet<Canvas>(root);
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 10;
            SceneBuilderCore.AddOrGet<GraphicRaycaster>(root);
            SceneBuilderCore.AddOrGet<CanvasGroup>(root);

            var catBoard = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "CatBoard", 0);
            SceneBuilderCore.FigmaRect(catBoard, 261f, 67f, 146f, 249f);
            SceneBuilderCore.SetImage(catBoard, "Assets/Art/DeskHub/catBoard.png");

            var still = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "TracyStill", 1);
            SceneBuilderCore.FigmaRect(still, -36f, 155f, 307f, 762f);
            var stillImage = SceneBuilderCore.SetImage(still, "Assets/Art/DeskHub/tracyStill.png");

            var happy = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "TracyHappy", 2);
            SceneBuilderCore.FigmaRect(happy, -36f, 155f, 307f, 762f);
            SceneBuilderCore.SetImage(happy, "Assets/Art/DeskHub/tracyTalkingHappyNormal.png");

            var embarrassed = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "TracyEmbarrassed", 3);
            SceneBuilderCore.FigmaRect(embarrassed, -36f, 150f, 312f, 775f);
            SceneBuilderCore.SetImage(embarrassed, "Assets/Art/DeskHub/tracyTalkingEmbarassed.png");

            var dialogueRect = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "DialogueRect", 4);
            SceneBuilderCore.FigmaRect(dialogueRect, 36f, 627f, 340f, 163f);
            SceneBuilderCore.SetImage(dialogueRect, "Assets/Art/UI/dialogueRect.png");

            var lineText = SceneBuilderCore.FindOrCreateChildOrdered(dialogueRect.transform, "LineText", 0);
            SceneBuilderCore.FigmaRect(lineText, 24f, 30f, 292f, 90f);
            var lineLabel = SceneBuilderCore.SetupText(lineText, SceneBuilderCore.FontChoice.SpecialElite, 22f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Left, null, "...");

            var tapHint = SceneBuilderCore.FindOrCreateChildOrdered(dialogueRect.transform, "TapHint", 1);
            SceneBuilderCore.FigmaRect(tapHint, 220f, 130f, 100f, 20f);
            var tapHintLabel = SceneBuilderCore.SetupText(tapHint, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                new Color(0.7f, 0.7f, 0.7f, 1f), TextAlignmentOptions.Right, null, "tap to continue");

            // Every pose starts inactive; ApplyMood() (TracyOverlayView) turns one on.
            SceneBuilderCore.SetActive(still, true);
            SceneBuilderCore.SetActive(happy, false);
            SceneBuilderCore.SetActive(embarrassed, false);

            var view = SceneBuilderCore.AddOrGet<RestoriumEmporium.Tutorial.TracyOverlayView>(root);
            var so = new SerializedObject(view);
            SceneBuilderCore.SetField(so, "scrimImage", scrim, "HubTracyOverlay");
            SceneBuilderCore.SetField(so, "portraitImage", stillImage, "HubTracyOverlay");
            SceneBuilderCore.SetField(so, "lineLabel", lineLabel, "HubTracyOverlay");
            SceneBuilderCore.SetField(so, "tapHintLabel", tapHintLabel, "HubTracyOverlay");
            // Mood Objects instance: leave Mood Sprites empty (per TracyOverlayView's
            // own ApplyMood(): if any Mood Object is set it takes over from the sprites).
            SceneBuilderCore.SetField(so, "stillObject", still, "HubTracyOverlay");
            SceneBuilderCore.SetField(so, "happyObject", happy, "HubTracyOverlay");
            SceneBuilderCore.SetField(so, "embarrassedObject", embarrassed, "HubTracyOverlay");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return view;
        }
    }
}
