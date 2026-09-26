// ============================================================
// BuildGameFlow — wires GameFlow's components (ScreenRouter, GameFlowController,
// TutorialController, TutorialInputGate) and RestorationController.beginOnStart.
// WHAT & WHY: CONSISTENCY.md's "Scene builder input" §GameFlow, done last since
//   it references almost everything every other Build*.cs file just made.
// KEY DECISIONS:
//   - RestorationController.beginOnStart is verified/forced to false every run
//     (dispute b in CONSISTENCY.md: "a single checkbox with no compile-time
//     signal if forgotten" — the scene builder is exactly the place to make
//     that check un-forgettable).
//   - TutorialInputGate.gatedRoots: a CanvasGroup per interactive area a gated
//     tutorial step can target (see the full list in the code below and the
//     cross-reference against every gated step's targetAnchorId in
//     Docs/Batch2/CONTRACT.md §6/§7). Every CanvasGroup is added with
//     AddOrGet — reusing one a script already relies on (JournalScreen's own
//     Fade Group) rather than adding a second, redundant group over the same
//     objects.
//   - Missing TutorialSequenceData assets (the human hasn't run
//     Restorium/Tutorial/Rebuild Tutorial Sequences yet) are reported by name,
//     not silently skipped, so "Sequences" ends up with an obvious hole instead
//     of quietly being one element short.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — run Restorium/Scene/Build Batch 2 Scene Objects with the
//     Game scene open, AFTER the human menu run order in CONSISTENCY.md steps
//     1-5 (posters, shop items, locale/tutorial tables) has created the assets
//     this file loads by path. See Docs/Batch2/handoff/SCENE_BUILDER.md.
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    internal static class BuildGameFlow
    {
        private const string SequenceFolder = "Assets/Data/Tutorial/Sequences";

        public static void Build(
            Transform canvas,
            GameObject gameFlow,
            RestoriumEmporium.Core.GameFlowController flow,
            RestoriumEmporium.Core.ScreenRouter router,
            RestoriumEmporium.Restoration.RestorationController restoration,
            RestoriumEmporium.Tutorial.TutorialController tutorial,
            RestoriumEmporium.Tutorial.TutorialInputGate inputGate,
            RestoriumEmporium.Cinematics.CutscenePlayer cutscenePlayer,
            RestoriumEmporium.UI.OverlayController overlays,
            RestoriumEmporium.Tutorial.TracyOverlayView hubOverlay,
            RestoriumEmporium.UI.DeskHubScreen deskHub,
            RestoriumEmporium.UI.ShopScreen shop,
            RestoriumEmporium.UI.StickerRemovalScreen stickerRemoval)
        {
            // ---- RestorationController.beginOnStart -------------------------------------
            var restorationSo = new SerializedObject(restoration);
            var beginOnStart = restorationSo.FindProperty("beginOnStart");

            if (beginOnStart == null)
            {
                SceneBuilderCore.Problem("RestorationController has no 'beginOnStart' field — the scene " +
                                         "builder needs updating to match the current script.");
            }
            else if (beginOnStart.boolValue)
            {
                beginOnStart.boolValue = false;
                restorationSo.ApplyModifiedProperties();
                SceneBuilderCore.NoteUpdated("RestorationController.beginOnStart forced to UNTICKED " +
                                              "(dispute b, CONSISTENCY.md — GameFlowController drives it).");
            }

            // ---- ScreenRouter.screens -----------------------------------------------------
            var journal = FindScreen(canvas, "JournalScreen");
            var cleaning = FindScreen(canvas, "CleaningScreen");
            var linenFront = FindScreen(canvas, "LinenBackingFrontScreen");
            var linenBack = FindScreen(canvas, "LinenBackingBackScreen");
            var linenFinal = FindScreen(canvas, "LinenBackingFinalScreen");
            var finishedRepair = FindScreen(canvas, "FinishedRepairScreen");

            var routerSo = new SerializedObject(router);
            SceneBuilderCore.SetFieldArray(routerSo, "screens", new UnityEngine.Object[]
            {
                journal, cleaning, linenFront, linenBack, linenFinal, finishedRepair, deskHub, shop,
                stickerRemoval
            }, "GameFlow/ScreenRouter");
            routerSo.ApplyModifiedProperties();

            // ---- GameFlowController --------------------------------------------------------
            var posters = SceneBuilderCore.LoadAsset<RestoriumEmporium.Data.PosterCatalog>(
                "Assets/Data/Catalogs/PosterCatalog.asset", "PosterCatalog");
            var introClip = LoadVideo("Assets/videos/tracysc1.mp4");
            var bookClip = LoadVideo("Assets/videos/openBookTransition.mp4");

            var flowSo = new SerializedObject(flow);
            SceneBuilderCore.SetField(flowSo, "router", router, "GameFlow/GameFlowController");
            SceneBuilderCore.SetField(flowSo, "restorationSource", restoration, "GameFlow/GameFlowController");
            SceneBuilderCore.SetField(flowSo, "cutsceneSource", cutscenePlayer, "GameFlow/GameFlowController");
            SceneBuilderCore.SetField(flowSo, "posters", posters, "GameFlow/GameFlowController");
            SceneBuilderCore.SetField(flowSo, "introCutscene", introClip, "GameFlow/GameFlowController");
            SceneBuilderCore.SetField(flowSo, "bookOpenCutscene", bookClip, "GameFlow/GameFlowController");
            SceneBuilderCore.SetField(flowSo, "bookCutsceneSkippable", true, "GameFlow/GameFlowController");
            flowSo.ApplyModifiedProperties();

            // ---- Gated roots (built here so every anchor a gated step can target has one) --
            var toolBarCleaning = EnsureCanvasGroup(cleaning != null ? cleaning.transform.Find("ToolBarRoot") : null);
            var toolBarLinenFront = EnsureCanvasGroup(linenFront != null ? linenFront.transform.Find("ToolBarRoot") : null);
            var toolBarLinenBack = EnsureCanvasGroup(linenBack != null ? linenBack.transform.Find("ToolBarRoot") : null);
            var toolBarLinenFinal = EnsureCanvasGroup(linenFinal != null ? linenFinal.transform.Find("ToolBarRoot") : null);
            var posterGroup = EnsureCanvasGroup(canvas.Find("Poster"));
            var finishedRepairGroup = finishedRepair != null
                ? SceneBuilderCore.AddOrGet<CanvasGroup>(finishedRepair.gameObject)
                : null;
            var journalGroup = journal != null ? SceneBuilderCore.AddOrGet<CanvasGroup>(journal.gameObject) : null;

            var deskHubTransform = deskHub != null ? deskHub.transform : null;
            var deskTopBar = EnsureCanvasGroup(deskHubTransform != null
                ? deskHubTransform.Find("NormalUI/TopBar") : null);
            var deskBook = EnsureCanvasGroup(deskHubTransform != null ? deskHubTransform.Find("BookButton") : null);
            var deskRoom = EnsureCanvasGroup(deskHubTransform != null ? deskHubTransform.Find("Room") : null);
            var deskEditUi = EnsureCanvasGroup(deskHubTransform != null ? deskHubTransform.Find("EditUI") : null);
            var deskPreviewUi = EnsureCanvasGroup(deskHubTransform != null ? deskHubTransform.Find("PreviewUI") : null);

            var shopGrid = EnsureCanvasGroup(shop != null ? shop.transform.Find("Grid") : null);

            var gatedRoots = new List<UnityEngine.Object>();
            AddIfPresent(gatedRoots, journalGroup);
            AddIfPresent(gatedRoots, toolBarCleaning);
            AddIfPresent(gatedRoots, toolBarLinenFront);
            AddIfPresent(gatedRoots, toolBarLinenBack);
            AddIfPresent(gatedRoots, toolBarLinenFinal);
            AddIfPresent(gatedRoots, posterGroup);
            AddIfPresent(gatedRoots, finishedRepairGroup);
            AddIfPresent(gatedRoots, deskTopBar);
            AddIfPresent(gatedRoots, deskBook);
            AddIfPresent(gatedRoots, deskRoom);
            AddIfPresent(gatedRoots, deskEditUi);
            AddIfPresent(gatedRoots, deskPreviewUi);
            AddIfPresent(gatedRoots, shopGrid);

            var gateSo = new SerializedObject(inputGate);
            SceneBuilderCore.SetFieldArray(gateSo, "gatedRoots", gatedRoots.ToArray(), "GameFlow/TutorialInputGate");
            gateSo.ApplyModifiedProperties();

            // ---- TutorialController --------------------------------------------------------
            var sequences = new[]
            {
                LoadSequence("first_restoration"), LoadSequence("deskhub_lamp"), LoadSequence("journal_page2"),
                LoadSequence("stickers")
            };

            var tracyHelpOverlay = canvas.Find("TracyHelpOverlay");
            var portraitOverlay = tracyHelpOverlay != null
                ? tracyHelpOverlay.GetComponent<RestoriumEmporium.Tutorial.TracyOverlayView>()
                : null;

            if (portraitOverlay == null)
            {
                SceneBuilderCore.Problem("'TracyHelpOverlay' (under Canvas) was not found or has no " +
                                         "TracyOverlayView — TutorialController.Portrait Overlay was left empty.");
            }

            var helpingHand = canvas.Find("HelpingHand");
            var handPointer = helpingHand != null
                ? helpingHand.GetComponent<RestoriumEmporium.Tutorial.HandPointer>()
                : null;

            if (handPointer == null)
            {
                SceneBuilderCore.Problem("'HelpingHand' (under Canvas) was not found or has no HandPointer — " +
                                         "TutorialController.Hand Pointer was left empty.");
            }

            var tutorialSo = new SerializedObject(tutorial);
            SceneBuilderCore.SetFieldArray(tutorialSo, "sequences", sequences, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "portraitOverlay", portraitOverlay, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "hubOverlay", hubOverlay, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "handPointer", handPointer, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "inputGate", inputGate, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "overlays", overlays, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "restorationSource", restoration, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "screenRouterSource", router, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "runAutomatically", true, "GameFlow/TutorialController");
            SceneBuilderCore.SetField(tutorialSo, "useSafetyTimeout", false, "GameFlow/TutorialController");
            tutorialSo.ApplyModifiedProperties();
        }

        private static RestoriumEmporium.Core.ScreenView FindScreen(Transform canvas, string name)
        {
            var t = canvas.Find(name);

            if (t == null)
            {
                SceneBuilderCore.Problem($"'{name}' was not found under Canvas — ScreenRouter.screens will be " +
                                         "missing an entry for it.");
                return null;
            }

            var view = t.GetComponent<RestoriumEmporium.Core.ScreenView>();

            if (view == null)
            {
                SceneBuilderCore.Problem($"'{name}' has no ScreenView-derived component.");
            }

            return view;
        }

        private static CanvasGroup EnsureCanvasGroup(Transform target)
        {
            if (target == null)
            {
                return null;
            }

            return SceneBuilderCore.AddOrGet<CanvasGroup>(target.gameObject);
        }

        private static void AddIfPresent(List<UnityEngine.Object> list, UnityEngine.Object value)
        {
            if (value != null)
            {
                list.Add(value);
            }
        }

        private static UnityEngine.Video.VideoClip LoadVideo(string path)
        {
            return SceneBuilderCore.LoadAsset<UnityEngine.Video.VideoClip>(path, "VideoClip");
        }

        private static RestoriumEmporium.Data.TutorialSequenceData LoadSequence(string sequenceId)
        {
            var path = $"{SequenceFolder}/{sequenceId}.asset";
            var seq = AssetDatabase.LoadAssetAtPath<RestoriumEmporium.Data.TutorialSequenceData>(path);

            if (seq == null)
            {
                SceneBuilderCore.Problem($"Tutorial sequence '{sequenceId}' not found at '{path}'. Run " +
                                         "Restorium/Tutorial/Rebuild Tutorial Sequences, then re-run the scene " +
                                         "builder.");
            }

            return seq;
        }
    }
}
