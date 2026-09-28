// ============================================================
// BuildJournalAndFinishedRepair — rewires the existing "JournalScreen" and
// "FinishedRepairScreen" objects (built for the MVP) to the Batch 2 design.
// WHAT & WHY: Both screens already exist in Game.unity from the MVP. Per the
//   task's own rule for redesigned MVP screens, this file UPDATES named
//   children in place (renaming an MVP object onto its Batch 2 name/role
//   rather than creating a duplicate next to an orphan) and only disables
//   (never deletes) an old child that has no place in the new design.
// KEY DECISIONS:
//   - Renames (same GameObject, same GUID, carried forward):
//       Journal:  PosterThumbnail -> PosterImage, PosterTitle -> TitleLabel,
//                 RestoreButton -> ActionButton (+ its "Text (TMP)" -> "Label"),
//                 PreviousPageButton -> PrevPageButton.
//       FinishedRepair: ContinueButton's "Text (TMP)" -> "Label" (the button
//                 itself keeps its MVP name; the field is still "continueButton").
//   - Retired (disabled, not deleted, reported): Journal's "PageHint" ("Click to
//     change pages" caption) — the Batch 2 design has no such caption; the
//     arrows themselves are simply enabled/disabled per JournalPageRules.
//   - New objects: Journal's BackButton + optional LockedHint; FinishedRepair's
//     ChapterLabel, CoinsLabel, DoubleButton, OrLabel.
//   - JournalScreen's "Fade Group" is a CanvasGroup on the SCREEN ROOT itself
//     (not a child) — added directly to the existing JournalScreen object.
//   - FinishedRepairScreen's "On Shown" UnityEvent -> CardFlipAnimator.Play() is
//     the one cross-file wiring the whole checker flagged as easy to miss
//     (CONSISTENCY.md dispute c): this file adds a Card Flip Animator to
//     FlipCard (if missing) and wires exactly one persistent listener to it,
//     without disturbing any listener a human already added by hand.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — run Restorium/Scene/Build Batch 2 Scene Objects with the
//     Game scene open. See Docs/Batch2/handoff/SCENE_BUILDER.md.
// ---------------------------------------------------------------

using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RestoriumEmporium.EditorTools
{
    internal static class BuildJournalAndFinishedRepair
    {
        // ---- Journal ------------------------------------------------------------------

        public static void BuildJournal(Transform canvas, RestoriumEmporium.Core.GameFlowController flow)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "JournalScreen");
            SceneBuilderCore.Stretch(root);
            var fadeGroup = SceneBuilderCore.AddOrGet<CanvasGroup>(root);

            var background = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Background", 0);
            SceneBuilderCore.Stretch(background);
            SceneBuilderCore.SetImage(background, "Assets/Art/journalAssets/bg.png");

            var paper = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Paper", 1);
            SceneBuilderCore.FigmaRect(paper, 0f, 175f, 412f, 541f);
            SceneBuilderCore.SetImage(paper, "Assets/Art/journalAssets/paper 1.png");

            var posterImageGo = SceneBuilderCore.FindRenameOrCreateChild(root.transform, "PosterImage",
                "PosterThumbnail");
            posterImageGo.transform.SetSiblingIndex(2);
            SceneBuilderCore.FigmaRect(posterImageGo, 120f, 270f, 173f, 309f);
            var posterImage = SceneBuilderCore.AddOrGet<Image>(posterImageGo);
            posterImage.preserveAspect = false;
            posterImage.raycastTarget = false;

            var titleGo = SceneBuilderCore.FindRenameOrCreateChild(root.transform, "TitleLabel", "PosterTitle");
            titleGo.transform.SetSiblingIndex(3);
            SceneBuilderCore.FigmaRect(titleGo, 82f, 241f, 248f, 20f);
            var titleLabel = SceneBuilderCore.SetupText(titleGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                new Color(0.267f, 0.082f, 0.11f, 0.69f), TextAlignmentOptions.Center, null, "Poster Title");

            var actionGo = SceneBuilderCore.FindRenameOrCreateChild(root.transform, "ActionButton", "RestoreButton");
            actionGo.transform.SetSiblingIndex(4);
            SceneBuilderCore.FigmaRect(actionGo, 104f, 592f, 202f, 69f);
            SceneBuilderCore.SetImage(actionGo, "Assets/Art/journalAssets/buttonBase.png");
            var actionButton = SceneBuilderCore.SetupButton(actionGo);
            SceneBuilderCore.SetupAnchor(actionGo, "journal.restoreButton");
            var actionLabelGo = SceneBuilderCore.FindRenameOrCreateChild(actionGo.transform, "Label", "Text (TMP)");
            SceneBuilderCore.Stretch(actionLabelGo);
            var actionLabel = SceneBuilderCore.SetupText(actionLabelGo, SceneBuilderCore.FontChoice.Rye, 32f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, null, "Restore");

            var prevGo = SceneBuilderCore.FindRenameOrCreateChild(root.transform, "PrevPageButton",
                "PreviousPageButton");
            prevGo.transform.SetSiblingIndex(5);
            SceneBuilderCore.FigmaRect(prevGo, 82f, 846f, 31f, 59f);
            SceneBuilderCore.SetImage(prevGo, "Assets/Art/journalAssets/arrow.png");
            var prevButton = SceneBuilderCore.SetupButton(prevGo, addSfx: false);
            SceneBuilderCore.SetupAnchor(prevGo, "journal.prevPage");

            var nextGo = SceneBuilderCore.FindRenameOrCreateChild(root.transform, "NextPageButton",
                "NextPageButton");
            nextGo.transform.SetSiblingIndex(6);
            SceneBuilderCore.FigmaRect(nextGo, 330f, 787f, 31f, 59f);
            SceneBuilderCore.SetImage(nextGo, "Assets/Art/journalAssets/arrow.png");
            nextGo.transform.localEulerAngles = new Vector3(0f, 180f, 0f); // mirrored, per FigmaLayout.md §10
            var nextButton = SceneBuilderCore.SetupButton(nextGo, addSfx: false);
            SceneBuilderCore.SetupAnchor(nextGo, "journal.nextPage");

            var backGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "BackButton", 7);
            SceneBuilderCore.FigmaRect(backGo, 14f, 60f, 58.6f, 55f);
            SceneBuilderCore.SetImage(backGo, "Assets/Art/UI/backArrowIcon.png");
            backGo.transform.localEulerAngles = new Vector3(0f, 0f, 180f);
            var backButton = SceneBuilderCore.SetupButton(backGo);
            SceneBuilderCore.SetupAnchor(backGo, "journal.back");
            var backLabelGo = SceneBuilderCore.FindOrCreateChild(backGo.transform, "Label");
            var backLabelRect = SceneBuilderCore.Rect(backLabelGo);
            backLabelRect.anchorMin = new Vector2(1f, 0.5f);
            backLabelRect.anchorMax = new Vector2(1f, 0.5f);
            backLabelRect.pivot = new Vector2(0f, 0.5f);
            backLabelRect.sizeDelta = new Vector2(152f, 20f);
            backLabelRect.anchoredPosition = new Vector2(4f, 0f);
            backLabelGo.transform.localEulerAngles = new Vector3(0f, 0f, -180f);
            SceneBuilderCore.SetupText(backLabelGo, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("a1c9c1"), TextAlignmentOptions.Left, "ui.journal.back",
                "Go back to workbench");

            var lockedHint = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "LockedHint", 8);
            SceneBuilderCore.FigmaRect(lockedHint, 82f, 260f, 248f, 30f);
            SceneBuilderCore.SetupText(lockedHint, SceneBuilderCore.FontChoice.SpecialElite, 14f,
                SceneBuilderCore.Hex("441524"), TextAlignmentOptions.Center, "ui.journal.lockedHint",
                "Finish the previous poster first.");
            SceneBuilderCore.SetActive(lockedHint, false);

            SceneBuilderCore.RetireLegacyChild(root.transform, "PageHint");

            var screen = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.JournalScreen>(root);
            var so = new SerializedObject(screen);
            SceneBuilderCore.SetField(so, "flow", flow, "JournalScreen");
            SceneBuilderCore.SetField(so, "fadeGroup", fadeGroup, "JournalScreen");
            SceneBuilderCore.SetField(so, "posterImage", posterImage, "JournalScreen");
            SceneBuilderCore.SetField(so, "titleLabel", titleLabel, "JournalScreen");
            SceneBuilderCore.SetField(so, "actionButton", actionButton, "JournalScreen");
            SceneBuilderCore.SetField(so, "actionButtonLabel", actionLabel, "JournalScreen");
            SceneBuilderCore.SetField(so, "prevPageButton", prevButton, "JournalScreen");
            SceneBuilderCore.SetField(so, "nextPageButton", nextButton, "JournalScreen");
            SceneBuilderCore.SetField(so, "backButton", backButton, "JournalScreen");
            SceneBuilderCore.SetField(so, "lockedHint", lockedHint, "JournalScreen");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
        }

        // ---- FinishedRepair -------------------------------------------------------------

        public static void BuildFinishedRepair(Transform canvas, RestoriumEmporium.Core.GameFlowController flow)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "FinishedRepairScreen");
            SceneBuilderCore.Stretch(root);

            var background = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Background", 0);
            SceneBuilderCore.Stretch(background);
            SceneBuilderCore.SetImage(background, "Assets/Art/FinishedRepairBG.png");

            var titleGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Title", 1);
            SceneBuilderCore.FigmaRect(titleGo, 55f, 100f, 309f, 80f);
            SceneBuilderCore.SetupText(titleGo, SceneBuilderCore.FontChoice.Rye, 32f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.finishedRepair.title",
                "Restoration Complete!");

            var chapterGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "ChapterLabel", 2);
            SceneBuilderCore.FigmaRect(chapterGo, 81f, 196f, 245f, 15f);
            var chapterLabel = SceneBuilderCore.SetupText(chapterGo, SceneBuilderCore.FontChoice.SpecialElite,
                15f, SceneBuilderCore.Hex("a1c9c1"), TextAlignmentOptions.Center, null, "Ch1 - ...");

            var flipCard = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "FlipCard", 3);
            SceneBuilderCore.FigmaRect(flipCard, 108f, 230f, 202f, 344f, 0.5f, 0.5f);

            var frontFaceGo = SceneBuilderCore.FindOrCreateChild(flipCard.transform, "FrontFace");
            SceneBuilderCore.Stretch(frontFaceGo);
            var frontFace = SceneBuilderCore.AddOrGet<Image>(frontFaceGo);
            frontFace.raycastTarget = false;
            frontFace.preserveAspect = true;

            var backFaceGo = SceneBuilderCore.FindOrCreateChild(flipCard.transform, "BackFace");
            SceneBuilderCore.Stretch(backFaceGo);
            var backFace = SceneBuilderCore.AddOrGet<Image>(backFaceGo);
            backFace.raycastTarget = false;
            backFace.preserveAspect = true;
            backFaceGo.transform.localEulerAngles = new Vector3(0f, 180f, 0f);
            SceneBuilderCore.SetActive(backFaceGo, false);

            var flipAnimator = SceneBuilderCore.AddOrGet<RestoriumEmporium.Restoration.CardFlipAnimator>(flipCard);
            var flipSo = new SerializedObject(flipAnimator);
            SceneBuilderCore.SetField(flipSo, "faceImage", frontFace, "FinishedRepairScreen/FlipCard");
            SceneBuilderCore.SetField(flipSo, "backFaceSource", backFace, "FinishedRepairScreen/FlipCard");
            SceneBuilderCore.SetField(flipSo, "duration", 0.5f, "FinishedRepairScreen/FlipCard");
            flipSo.ApplyModifiedProperties();

            var coinsGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "CoinsLabel", 4);
            SceneBuilderCore.FigmaRect(coinsGo, 154f, 607f, 84f, 30f);
            var coinsLabel = SceneBuilderCore.SetupText(coinsGo, SceneBuilderCore.FontChoice.Rye, 24f, Color.white,
                TextAlignmentOptions.Center, null, "+100");

            var doubleGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "DoubleButton", 5);
            SceneBuilderCore.FigmaRect(doubleGo, 76f, 659f, 257f, 63f);
            SceneBuilderCore.SetImage(doubleGo, "Assets/Art/newGameButton.png");
            var doubleButton = SceneBuilderCore.SetupButton(doubleGo);
            SceneBuilderCore.SetupAnchor(doubleGo, "finishedRepair.doubleButton");
            var doubleLabelGo = SceneBuilderCore.FindOrCreateChild(doubleGo.transform, "Label");
            SceneBuilderCore.Stretch(doubleLabelGo);
            SceneBuilderCore.SetupText(doubleLabelGo, SceneBuilderCore.FontChoice.SpecialElite, 24f,
                SceneBuilderCore.Hex("f5ba55"), TextAlignmentOptions.Center, "ui.finishedRepair.double",
                "Double Reward");

            var orGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "OrLabel", 6);
            SceneBuilderCore.FigmaRect(orGo, 187f, 738f, 28f, 42f);
            SceneBuilderCore.SetupText(orGo, SceneBuilderCore.FontChoice.SpecialElite, 24f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.finishedRepair.or", "or");

            var continueGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "ContinueButton", 7);
            SceneBuilderCore.FigmaRect(continueGo, 105f, 734f, 202f, 69f);
            SceneBuilderCore.SetImage(continueGo, "Assets/Art/newGameButton.png");
            var continueButton = SceneBuilderCore.SetupButton(continueGo);
            SceneBuilderCore.SetupAnchor(continueGo, "finishedRepair.continueButton");
            var continueLabelGo = SceneBuilderCore.FindRenameOrCreateChild(continueGo.transform, "Label",
                "Text (TMP)");
            SceneBuilderCore.Stretch(continueLabelGo);
            SceneBuilderCore.SetupText(continueLabelGo, SceneBuilderCore.FontChoice.SpecialElite, 24f,
                SceneBuilderCore.Hex("c7c8bc"), TextAlignmentOptions.Center, "ui.finishedRepair.continue",
                "Continue");

            var screen = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.FinishedRepairScreen>(root);
            var so = new SerializedObject(screen);
            SceneBuilderCore.SetField(so, "flow", flow, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "chapterLabel", chapterLabel, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "coinsLabel", coinsLabel, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "flipCardRoot", SceneBuilderCore.Rect(flipCard), "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "frontFace", frontFace, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "backFace", backFace, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "continueButton", continueButton, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "doubleButton", doubleButton, "FinishedRepairScreen");
            SceneBuilderCore.SetField(so, "orLabelObject", orGo, "FinishedRepairScreen");
            so.ApplyModifiedProperties();

            WireOnShown(screen, flipAnimator);

            SceneBuilderCore.SetActive(root, true);
        }

        /// <summary>
        /// FinishedRepairScreen.onShown is a private UnityEvent (dispute c in
        /// CONSISTENCY.md: connected only by a UnityEvent, never a typed reference).
        /// Adds exactly one persistent listener to CardFlipAnimator.Play() if the
        /// event has none yet; leaves any existing listener(s) alone otherwise.
        /// </summary>
        private static void WireOnShown(RestoriumEmporium.UI.FinishedRepairScreen screen,
            RestoriumEmporium.Restoration.CardFlipAnimator flipAnimator)
        {
            var field = typeof(RestoriumEmporium.UI.FinishedRepairScreen).GetField("onShown",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                SceneBuilderCore.Problem("FinishedRepairScreen no longer has a private 'onShown' field — " +
                                         "the scene builder needs updating to match the current script.");
                return;
            }

            if (!(field.GetValue(screen) is UnityEvent onShown))
            {
                SceneBuilderCore.Problem("FinishedRepairScreen.onShown could not be read via reflection.");
                return;
            }

            for (var i = 0; i < onShown.GetPersistentEventCount(); i++)
            {
                if (onShown.GetPersistentTarget(i) == flipAnimator &&
                    onShown.GetPersistentMethodName(i) == "Play")
                {
                    return; // already wired, leave it alone
                }
            }

            if (onShown.GetPersistentEventCount() > 0)
            {
                SceneBuilderCore.NoteUpdated("FinishedRepairScreen.onShown already has " +
                                              $"{onShown.GetPersistentEventCount()} listener(s) that are not " +
                                              "CardFlipAnimator.Play() — left them alone; verify by hand that " +
                                              "the reveal card still flips.");
                return;
            }

            Undo.RecordObject(screen, "Restorium Scene Builder");
            UnityEventTools.AddVoidPersistentListener(onShown, flipAnimator.Play);
            EditorUtility.SetDirty(screen);
            SceneBuilderCore.NoteUpdated("FinishedRepairScreen.onShown (+) -> CardFlipAnimator.Play() wired.");
        }
    }
}
