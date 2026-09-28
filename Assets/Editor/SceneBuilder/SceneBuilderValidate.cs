// ============================================================
// SceneBuilderValidate — "Restorium/Scene/Validate Game Scene".
// WHAT & WHY: Catches the two failure modes a scene builder is most likely to
//   leave behind: a serialized reference that never got assigned, and a
//   tutorial step that points at an anchor id nothing in the scene provides.
//   Runs against whatever scene is currently open (normally the Game scene,
//   after Build Batch 2 Scene Objects) — read-only, changes nothing.
// KEY DECISIONS:
//   - Walks every MonoBehaviour whose type is in a RestoriumEmporium.* namespace,
//     found via FindObjectsByType(..., FindObjectsInactive.Include, ...) so
//     inactive objects (the Shop card template, FinishedRepair's BackFace) are
//     still checked.
//   - "Null object-reference field" is reported unless the exact
//     Type.FieldName pair is in KnownOptionalFields — every entry there quotes
//     the exact script comment ("optional", "leave EMPTY on purpose", falls
//     back at runtime) that makes it a documented exception, not a guess.
//   - Anchor coverage: TutorialAnchor COMPONENTS in the scene (not the runtime
//     Find() registry, which is only populated in Play mode) vs. every
//     targetAnchorId referenced by a TutorialStepData asset on disk, minus the
//     handful of ids that are legitimately created at runtime
//     (RuntimeAnchorIdExemptPrefixes) instead of living in the saved scene.
//   - Screen coverage: every GameScreen a RestorationStageData asset routes to,
//     plus the four screens that are never a stage's own screen but must always
//     be registered (Journal, FinishedRepair, DeskHub, Shop), checked against
//     ScreenRouter.screens (read through each ScreenView's own Screen property,
//     not the field's array order).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — with the Game scene open, run
//     Restorium/Scene/Validate Game Scene and read the Console.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    public static class SceneBuilderValidate
    {
        // Type.FieldName pairs whose null value is a documented, intentional choice
        // (quoted from the owning script's own comment), not a wiring gap.
        private static readonly HashSet<string> KnownOptionalFields = new HashSet<string>
        {
            // CutscenePlayer.cs: "Optional 'Tap to skip' label."
            "CutscenePlayer.skipHint",
            // GameFlowController.cs: "Optional: it is then found through ServiceLocator."
            "GameFlowController.cutsceneSource",
            // JournalScreen.cs: "Shown only while the current page is Locked." (optional)
            "JournalScreen.lockedHint",
            // ShopScreen.cs: "Optional. Scrolled back to the top when the tab changes."
            "ShopScreen.scroll",
            // ShopScreen.cs: "Optional. Shown when the active tab has no items."
            "ShopScreen.emptyLabel",
            // StickerRemovalScreen: SHOP/RESTORATION.md — leave EMPTY on purpose so the
            // shared Poster object is hidden while this screen is up.
            "StickerRemovalScreen.posterStackRoot",
            "StickerRemovalScreen.toolBarRoot",
            // StickerRemovalScreen.cs: "Empty = stickers are built in code."
            "StickerRemovalScreen.stickerTemplate",
            // StickerRemovalScreen.cs: Editor-only gizmo preview; harmless if unset.
            "StickerRemovalScreen.previewStage",
            // CardFlipAnimator.cs: only the FinishedRepair instance needs it; the
            // Poster's flip is driven entirely from script (RestorationPresenter).
            "CardFlipAnimator.backFaceSource",
            "CardFlipAnimator.target",
            "CardFlipAnimator.faceRect",
            // TracyOverlayView.cs: EITHER Mood Sprites OR Mood Objects is filled, never
            // both — whichever set is unused for a given instance stays empty by design.
            "TracyOverlayView.stillSprite", "TracyOverlayView.happySprite",
            "TracyOverlayView.embarrassedSprite", "TracyOverlayView.stillObject",
            "TracyOverlayView.happyObject", "TracyOverlayView.embarrassedObject",
            "TracyOverlayView.tapHintLabel",
            // HandPointer.cs: "Leave empty to use this object's own Rect Transform" / parents.
            "HandPointer.pointer", "HandPointer.canvas",
            // DecorationRoom.cs: "Found automatically when left empty."
            "DecorationRoom.dragCatcher",
            // TutorialAnchor.cs: "Optional. Leave empty to use this object's own RectTransform."
            "TutorialAnchor.pointAt",
            // LinenBackingScreen.cs: "optional and only used by the Final variant."
            "LinenBackingScreen.linenFrame",
        };

        // targetAnchorId values created entirely at RUNTIME (no TutorialAnchor
        // component exists for them in the saved scene file).
        private static readonly string[] RuntimeAnchorIdExemptPrefixes = { "shop.item.", "preview.item", "sticker.next" };

        [MenuItem("Restorium/Scene/Validate Game Scene")]
        public static void ValidateGameScene()
        {
            var nullFieldProblems = new List<string>();
            var anchorProblems = new List<string>();
            var screenProblems = new List<string>();

            CheckNullFields(nullFieldProblems);
            CheckAnchors(anchorProblems);
            CheckScreenCoverage(screenProblems);

            var total = nullFieldProblems.Count + anchorProblems.Count + screenProblems.Count;

            Debug.Log($"[SceneBuilder] Validate Game Scene: {total} problem(s) — " +
                      $"{nullFieldProblems.Count} null field(s), {anchorProblems.Count} orphan tutorial " +
                      $"anchor id(s), {screenProblems.Count} missing screen route(s).");

            foreach (var p in nullFieldProblems) Debug.LogWarning("[SceneBuilder][NullField] " + p);
            foreach (var p in anchorProblems) Debug.LogWarning("[SceneBuilder][Anchor] " + p);
            foreach (var p in screenProblems) Debug.LogWarning("[SceneBuilder][Screen] " + p);

            var message = total == 0
                ? "No problems found."
                : $"{nullFieldProblems.Count} null field(s)\n{anchorProblems.Count} orphan tutorial anchor id(s)\n" +
                  $"{screenProblems.Count} missing screen route(s)\n\nSee the Console for the full list " +
                  "([SceneBuilder][...] lines).";

            EditorUtility.DisplayDialog("Validate Game Scene", message, "OK");
        }

        // ---- Null serialized object-reference fields -------------------------------------

        private static void CheckNullFields(List<string> problems)
        {
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

            foreach (var mb in all)
            {
                if (mb == null)
                {
                    continue;
                }

                var type = mb.GetType();

                if (type.Namespace == null || !type.Namespace.StartsWith("RestoriumEmporium"))
                {
                    continue;
                }

                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                var enterChildren = true;

                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (it.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (it.name == "m_Script")
                    {
                        continue;
                    }

                    if (it.objectReferenceValue != null)
                    {
                        continue;
                    }

                    var key = type.Name + "." + it.name;

                    if (KnownOptionalFields.Contains(key))
                    {
                        continue;
                    }

                    problems.Add($"{PathOf(mb.transform)} ({type.Name}.{it.name}) is empty.");
                }
            }
        }

        private static string PathOf(Transform t)
        {
            var path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        // ---- Tutorial anchor coverage ----------------------------------------------------

        private static void CheckAnchors(List<string> problems)
        {
            var sceneAnchorIds = new HashSet<string>(StringComparer.Ordinal);
            var anchors = UnityEngine.Object.FindObjectsByType<RestoriumEmporium.Tutorial.TutorialAnchor>(
                FindObjectsInactive.Include);

            foreach (var a in anchors)
            {
                if (a != null && !string.IsNullOrEmpty(a.AnchorId))
                {
                    sceneAnchorIds.Add(a.AnchorId);
                }
            }

            var stepGuids = AssetDatabase.FindAssets("t:TutorialStepData");
            var usedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var guid in stepGuids)
            {
                var step = AssetDatabase.LoadAssetAtPath<RestoriumEmporium.Data.TutorialStepData>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (step != null && !string.IsNullOrEmpty(step.targetAnchorId))
                {
                    usedIds.Add(step.targetAnchorId);
                }
            }

            foreach (var id in usedIds)
            {
                if (sceneAnchorIds.Contains(id))
                {
                    continue;
                }

                if (RuntimeAnchorIdExemptPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                problems.Add($"TutorialStepData targetAnchorId '{id}' has no TutorialAnchor in the scene " +
                             "and is not one of the known runtime-spawned ids.");
            }
        }

        // ---- Screen coverage --------------------------------------------------------------

        private static void CheckScreenCoverage(List<string> problems)
        {
            var router = UnityEngine.Object.FindAnyObjectByType<RestoriumEmporium.Core.ScreenRouter>();

            if (router == null)
            {
                problems.Add("No ScreenRouter found in the open scene.");
                return;
            }

            var required = new HashSet<RestoriumEmporium.Core.GameScreen>
            {
                RestoriumEmporium.Core.GameScreen.Journal,
                RestoriumEmporium.Core.GameScreen.FinishedRepair,
                RestoriumEmporium.Core.GameScreen.DeskHub,
                RestoriumEmporium.Core.GameScreen.Shop
            };

            var stageGuids = AssetDatabase.FindAssets("t:RestorationStageData");

            foreach (var guid in stageGuids)
            {
                var stage = AssetDatabase.LoadAssetAtPath<RestoriumEmporium.Data.RestorationStageData>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (stage != null)
                {
                    required.Add(stage.screen);
                }
            }

            foreach (var screen in required)
            {
                if (!router.Has(screen))
                {
                    problems.Add($"GameScreen.{screen} is used by a stage/screen asset but ScreenRouter has " +
                                 "no view registered for it.");
                }
            }
        }
    }
}
