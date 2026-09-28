// ============================================================
// SceneBuilderMenu — "Restorium/Scene/Build Batch 2 Scene Objects".
// WHAT & WHY: The single entry point a human runs, once, in the open Game
//   scene, after the human menu run order in Docs/Batch2/CONSISTENCY.md
//   (posters, shop items, locale/tutorial tables) has created the .asset files
//   every Build*.cs file below loads by path. Wraps the whole run in one Undo
//   group, is fully idempotent (every Build*.cs file finds-or-creates by name),
//   and ends with a summary dialog of what was created/updated/left as a
//   problem for the human to fix.
// KEY DECISIONS:
//   - Refuses to run with no scene open / the wrong scene open, with a dialog
//     explaining exactly what to do (Assets/Scenes/Game.unity, opened, not in
//     Play mode) rather than silently building into whatever happens to be open.
//   - Order matters for exactly one reason: CutscenePlayer must be built LAST
//     so FindOrCreateChild's "already exists" path never has to re-sort it back
//     to the last sibling of a Canvas that grew new children after it (it also
//     explicitly re-asserts last-sibling every run, so this is a belt-and-braces
//     ordering, not a hard requirement).
//   - Saves the scene only after asking (EditorUtility.DisplayDialog), per the
//     task brief — a human mid-review of the diff should get to say no.
//   - The whole run is one Undo group ("Build Batch 2 Scene Objects"): Ctrl+Z
//     right after running undoes everything in one step.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// See Docs/Batch2/handoff/SCENE_BUILDER.md for the full human run order. In
// short: open Assets/Scenes/Game.unity, run the posters/shop/locale/tutorial
// menus from CONSISTENCY.md first, THEN:
// [ ] Restorium > Scene > Build Batch 2 Scene Objects
// [ ] Restorium > Scene > Validate Game Scene
// [ ] Read the Console for anything logged as [SceneBuilder] — each line names
//     the exact object/field/asset that still needs attention.
// ---------------------------------------------------------------

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RestoriumEmporium.EditorTools
{
    public static class SceneBuilderMenu
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Restorium/Scene/Build Batch 2 Scene Objects")]
        public static void BuildBatch2SceneObjects()
        {
            var gameScene = SceneManager.GetSceneByPath(GameScenePath);

            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                EditorUtility.DisplayDialog("Restorium Scene Builder",
                    "The Game scene is not open.\n\n" +
                    $"Open '{GameScenePath}' (double-click it in the Project window) and run this menu " +
                    "again. This tool edits the currently open scene in place; it will not open one for " +
                    "you (to avoid discarding unsaved changes in whatever scene IS open).",
                    "OK");
                return;
            }

            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Restorium Scene Builder",
                    "Exit Play mode first. Changes made while playing are not saved.", "OK");
                return;
            }

            var canvasGo = GameObject.Find("Canvas");
            var gameFlowGo = GameObject.Find("GameFlow");

            if (canvasGo == null || gameFlowGo == null)
            {
                EditorUtility.DisplayDialog("Restorium Scene Builder",
                    "Could not find 'Canvas' and/or 'GameFlow' at the root of the open scene. This tool " +
                    "extends the existing MVP scene; it does not build those two from scratch. If this is a " +
                    "brand new scene, follow SETUP.md Part 6 first.",
                    "OK");
                return;
            }

            SceneBuilderCore.ResetReport();
            Undo.SetCurrentGroupName("Build Batch 2 Scene Objects");
            var undoGroup = Undo.GetCurrentGroup();

            try
            {
                Run(canvasGo.transform, gameFlowGo);
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            EditorSceneManager.MarkSceneDirty(gameScene);
            ShowSummary(gameScene);
        }

        private static void Run(Transform canvas, GameObject gameFlowGo)
        {
            var flow = SceneBuilderCore.AddOrGet<RestoriumEmporium.Core.GameFlowController>(gameFlowGo);
            var router = SceneBuilderCore.AddOrGet<RestoriumEmporium.Core.ScreenRouter>(gameFlowGo);
            var restoration = SceneBuilderCore.AddOrGet<RestoriumEmporium.Restoration.RestorationController>(
                gameFlowGo);
            var tutorial = SceneBuilderCore.AddOrGet<RestoriumEmporium.Tutorial.TutorialController>(gameFlowGo);
            var inputGate = SceneBuilderCore.AddOrGet<RestoriumEmporium.Tutorial.TutorialInputGate>(gameFlowGo);

            var shopCatalog = SceneBuilderCore.LoadAsset<RestoriumEmporium.Data.ShopCatalog>(
                "Assets/Data/Catalogs/ShopCatalog.asset", "ShopCatalog");

            // 1) Overlays — everything else can reference its OverlayController.
            var overlaysRoot = BuildOverlays.Build(canvas, flow);
            var overlayController = overlaysRoot.GetComponent<RestoriumEmporium.UI.OverlayController>();

            // 2) HubTracyOverlay — TutorialController needs it, DeskHubScreen does not.
            var hubOverlay = BuildHubTracyOverlay.Build(canvas);

            // 3) DeskHubScreen, then ShopScreen (needs the DeskHubScreen reference).
            var deskHub = BuildDeskHub.Build(canvas, flow, overlayController, shopCatalog);
            var shop = BuildShop.Build(canvas, flow, deskHub, shopCatalog);

            // 4) StickerRemovalScreen.
            var stickerRemoval = BuildStickerRemoval.Build(canvas, restoration, overlayController);

            // 5) Rewire the existing MVP Journal / FinishedRepair screens.
            BuildJournalAndFinishedRepair.BuildJournal(canvas, flow);
            BuildJournalAndFinishedRepair.BuildFinishedRepair(canvas, flow);

            // 6) Hamburgers on the four pre-existing restoration screens that lack one.
            BuildHamburgers.Build(canvas, overlayController);

            // 7) CutscenePlayer. Built before GameFlow's own wiring (step 8) so
            // GameFlowController.cutsceneSource can be assigned on the very first
            // run, not just the second. It re-asserts "last child of Canvas" itself
            // every time it runs, so building it here (not literally last) still
            // leaves it topmost when this whole method returns.
            var cutscenePlayer = BuildCutscenePlayer.Build(canvas);

            // 8) GameFlow's own components (needs every screen + CutscenePlayer above).
            BuildGameFlow.Build(canvas, gameFlowGo, flow, router, restoration, tutorial, inputGate,
                cutscenePlayer, overlayController, hubOverlay, deskHub, shop, stickerRemoval);
        }

        private static void ShowSummary(Scene gameScene)
        {
            var body = $"Created: {SceneBuilderCore.Created.Count}\n" +
                       $"Updated: {SceneBuilderCore.Updated.Count}\n" +
                       $"Problems: {SceneBuilderCore.Problems.Count}\n\n";

            if (SceneBuilderCore.Problems.Count > 0)
            {
                body += "Problems (also logged to the Console as [SceneBuilder] warnings):\n";

                for (var i = 0; i < SceneBuilderCore.Problems.Count && i < 20; i++)
                {
                    body += "- " + SceneBuilderCore.Problems[i] + "\n";
                }

                if (SceneBuilderCore.Problems.Count > 20)
                {
                    body += $"...and {SceneBuilderCore.Problems.Count - 20} more (see the Console).\n";
                }

                body += "\n";
            }

            body += "Save the Game scene now?";

            var save = EditorUtility.DisplayDialog("Restorium Scene Builder — done", body, "Save scene",
                "Don't save yet");

            if (save)
            {
                EditorSceneManager.SaveScene(gameScene);
            }

            Debug.Log($"[SceneBuilder] Build Batch 2 Scene Objects finished. Created " +
                      $"{SceneBuilderCore.Created.Count}, updated {SceneBuilderCore.Updated.Count}, " +
                      $"{SceneBuilderCore.Problems.Count} problem(s). Scene {(save ? "saved." : "NOT saved.")}");
        }
    }
}
