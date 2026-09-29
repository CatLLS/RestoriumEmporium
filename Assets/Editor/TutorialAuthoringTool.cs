// ============================================================
// TutorialAuthoringTool — builds the four Batch 2 tutorial sequence assets.
// WHAT & WHY: Contract §6 lists four TutorialSequenceData the game needs:
//   first_restoration (the MVP's existing steps 01-14, reused as-is), deskhub_lamp,
//   journal_page2 and stickers. Authoring twelve-plus ScriptableObjects by hand
//   in the Inspector is slow and easy to get subtly wrong (a mistyped anchor id
//   is invisible until playtesting); this tool is the single, re-runnable
//   source of truth for their content, matching the LocaleSource/
//   LocaleTableBuilder pattern already used for strings.
// KEY DECISIONS:
//   - first_restoration's step assets are FOUND, not created: they already
//     exist at Assets/Data/Tutorial/01_Welcome.asset .. 14_UsePencil.asset
//     from the MVP, and their GUIDs must not change (nothing else references
//     them by path, but re-creating them would be needless churn on assets that
//     are already correct — their TEXT already picked up the Batch 2 rewording
//     through LocaleSource.Legacy.cs, no asset field needed to change).
//   - Every other step asset is UPSERTED by (sequence, fileName): loaded and
//     reconfigured if it exists, created if it does not. Re-running this menu
//     item after a design tweak (a different line key, a retimed safety valve)
//     is exactly as safe as the first run — this mirrors CatalogBuilder's own
//     "re-runnable, updates in place" rule.
//   - Some steps carry NO line (blank lineKey, gateInputToTarget = false): a
//     silent "wait for the screen / wait for preview to open" beat between a tap
//     and the line that reacts to it, or a hand-only beat in the edit-mode tour.
//     A silent step is never gated, so it can never be the thing that strands
//     a playtester.
//   - Steps whose whole point is a drag or a purchase (deskhub_lamp's "drag"
//     and "buy") are NOT gated (gateInputToTarget = false): gating would only
//     allow taps on the pointed-at anchor, but placing the lamp needs the whole
//     room to stay touchable, and DecorationRoom's own drag logic already keeps
//     the player from doing anything destructive.
//   - Every step gets a non-zero autoAdvanceSeconds safety valve, per contract
//     §6 ("Every gated step keeps a safety autoAdvanceSeconds or a non-gated
//     fallback so nothing can soft-lock"): TutorialController only reads this
//     valve when "Use Safety Timeout" is ticked (off by default in normal
//     play), so it costs nothing day to day and is there when the human wants
//     to burn through the script while testing.
//   - The lamp's price (100) exactly matches poster 1's reward (100, contract
//     §6), so deskhub_lamp's economy actually works when a fresh player reaches
//     it — this tool does not enforce that (it is a content fact, not a code
//     fact), but ShopItemWizard authoring the lamp at any OTHER price would
//     silently break this tutorial; flagged again in the handoff.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Run Restorium -> Tutorial -> Rebuild Tutorial Sequences. It requires the
//     MVP step assets (Assets/Data/Tutorial/01_Welcome.asset ..
//     14_UsePencil.asset) to already exist; if any are missing the Console
//     lists exactly which ones.
// [ ] It creates/updates:
//       Assets/Data/Tutorial/Sequences/first_restoration.asset
//       Assets/Data/Tutorial/Sequences/deskhub_lamp.asset (+ its Steps/ folder)
//       Assets/Data/Tutorial/Sequences/journal_page2.asset (+ its Steps/ folder)
//       Assets/Data/Tutorial/Sequences/stickers.asset (+ its Steps/ folder)
// [ ] Drag all four, in that order, into TutorialController -> "Sequences" on
//     the GameFlow object.
// [ ] Run this AFTER Restorium -> Localization -> Rebuild Locale Tables (or the
//     combined Restorium -> Rebuild Catalogs & Locale Tables), so the line keys
//     it references already resolve to real text.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    public static class TutorialAuthoringTool
    {
        private const string StepFolder = "Assets/Data/Tutorial";
        private const string SequenceFolder = "Assets/Data/Tutorial/Sequences";

        [MenuItem("Restorium/Tutorial/Rebuild Tutorial Sequences", false, 140)]
        public static void RebuildAll()
        {
            EnsureFolder(SequenceFolder);

            var firstRestoration = BuildFirstRestoration();
            var deskhubLamp = BuildDeskhubLamp();
            var journalPage2 = BuildJournalPage2();
            var stickers = BuildStickers();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TutorialAuthoringTool] Rebuilt sequences: " +
                      $"first_restoration ({firstRestoration.StepCount} steps), " +
                      $"deskhub_lamp ({deskhubLamp.StepCount}), " +
                      $"journal_page2 ({journalPage2.StepCount}), " +
                      $"stickers ({stickers.StepCount}). Drag all four into " +
                      "TutorialController -> Sequences on GameFlow.");
        }

        // ---- first_restoration: reuse the 16 existing MVP step assets -------------------

        private static TutorialSequenceData BuildFirstRestoration()
        {
            var stepNames = new[]
            {
                "01_Welcome", "02_TapRestore", "03_PickDustRemover", "04_UseDustRemover",
                "05_PickWaterSpray", "06_UseWaterSpray", "07_PickDeacidifier", "08_UseDeacidifier",
                "09_PickSqueegee", "10_UseSqueegee", "11_PickRoller", "12_UseRoller",
                "13_PickPencil", "14_UsePencil"

                // 15_Congrats and 16_TapContinue are deliberately left out: the
                // poster-1 completion video leads straight into a quiet Finished
                // Repair screen, with no Tracy line and no hand on Continue.
            };

            var steps = new TutorialStepData[stepNames.Length];
            var missing = new List<string>();

            for (var i = 0; i < stepNames.Length; i++)
            {
                var path = $"{StepFolder}/{stepNames[i]}.asset";
                var step = AssetDatabase.LoadAssetAtPath<TutorialStepData>(path);

                if (step == null)
                {
                    missing.Add(path);
                }

                steps[i] = step;
            }

            if (missing.Count > 0)
            {
                Debug.LogError("[TutorialAuthoringTool] first_restoration is missing these MVP step " +
                               "assets (it will reference null slots for them):\n  " +
                               string.Join("\n  ", missing));
            }

            return UpsertSequence("first_restoration", seq =>
            {
                seq.trigger = TutorialTrigger.ScreenEntered;
                seq.triggerScreen = GameScreen.Journal;
                seq.triggerStageKind = StageKind.Scrub;
                seq.requiresSequence = string.Empty;
                seq.requiresPosterCompleted = string.Empty;
                seq.steps = steps;
            });
        }

        // ---- deskhub_lamp -----------------------------------------------------------------

        private static TutorialSequenceData BuildDeskhubLamp()
        {
            var folder = $"{SequenceFolder}/deskhub_lamp";

            var steps = new[]
            {
                UpsertStep(folder, "01_Welcome", s => Configure(s, "welcome",
                    "tutorial.deskhub_lamp.welcome", TracyMood.Still, TracyPresentation.HubFullBody,
                    "", false, TutorialAdvance.TapAnywhere, 20f)),

                UpsertStep(folder, "02_Dark", s => Configure(s, "dark",
                    "tutorial.deskhub_lamp.dark", TracyMood.Still, TracyPresentation.HubFullBody,
                    "", false, TutorialAdvance.TapAnywhere, 20f)),

                UpsertStep(folder, "03_Fake", s => Configure(s, "fake",
                    "tutorial.deskhub_lamp.fake", TracyMood.Embarrassed, TracyPresentation.HubFullBody,
                    "", false, TutorialAdvance.TapAnywhere, 20f)),

                UpsertStep(folder, "04_TapShop", s => Configure(s, "tapShop",
                    "tutorial.deskhub_lamp.tapShop", TracyMood.Still, TracyPresentation.HubFullBody,
                    "desk.shop", true, TutorialAdvance.TapTarget, 25f)),

                // Silent: waits for the shop screen itself, no line, not gated.
                UpsertStep(folder, "05_WaitForShop", s => Configure(s, "waitForShop",
                    string.Empty, TracyMood.Still, TracyPresentation.HubFullBody,
                    string.Empty, false, TutorialAdvance.ScreenEntered, 20f, requiredScreen: GameScreen.Shop)),

                UpsertStep(folder, "06_TapLamp", s => Configure(s, "tapLamp",
                    "tutorial.deskhub_lamp.tapLamp", TracyMood.Happy, TracyPresentation.Portrait,
                    "shop.item.lamp", true, TutorialAdvance.TapTarget, 25f)),

                // Silent: waits for preview mode to actually open for the lamp.
                UpsertStep(folder, "07_WaitForPreview", s => Configure(s, "waitForPreview",
                    string.Empty, TracyMood.Still, TracyPresentation.Portrait,
                    string.Empty, false, TutorialAdvance.PreviewOpened, 20f, requiredItemId: "lamp")),

                // Not gated: the player must be able to drag anywhere in the room.
                // Ends on the drag itself, so the hand stays on the lamp until the
                // player has actually moved it (TapAnywhere ended with the line).
                UpsertStep(folder, "08_Drag", s => Configure(s, "drag",
                    "tutorial.deskhub_lamp.drag", TracyMood.Happy, TracyPresentation.HubFullBody,
                    "preview.item", false, TutorialAdvance.ItemDragged, 20f, requiredItemId: "lamp")),

                // Not gated for the same reason; hand points at Buy Item while waiting
                // for the purchase itself (any drag in between is fine).
                UpsertStep(folder, "09_Buy", s => Configure(s, "buy",
                    "tutorial.deskhub_lamp.buy", TracyMood.Happy, TracyPresentation.HubFullBody,
                    "preview.buy", false, TutorialAdvance.ItemPurchased, 35f, requiredItemId: "lamp")),

                UpsertStep(folder, "10_Happy", s => Configure(s, "happy",
                    "tutorial.deskhub_lamp.happy", TracyMood.Happy, TracyPresentation.HubFullBody,
                    "", false, TutorialAdvance.TapAnywhere, 20f)),

                // Guided tour of edit mode: tap Edit, move the lamp, Place Item,
                // Back to workshop. Only the first beat has a line; the others are
                // hand-only and not gated, so a wrong tap can never strand anyone.
                UpsertStep(folder, "11_EditMode", s => Configure(s, "editMode",
                    "tutorial.deskhub_lamp.editMode", TracyMood.Still, TracyPresentation.HubFullBody,
                    "desk.edit", true, TutorialAdvance.TapTarget, 25f)),

                UpsertStep(folder, "12_MoveItem", s => Configure(s, "moveItem",
                    string.Empty, TracyMood.Still, TracyPresentation.HubFullBody,
                    "room.item.lamp", false, TutorialAdvance.ItemDragged, 30f, requiredItemId: "lamp")),

                UpsertStep(folder, "13_PlaceItem", s => Configure(s, "placeItem",
                    string.Empty, TracyMood.Still, TracyPresentation.HubFullBody,
                    "edit.place", false, TutorialAdvance.TapTarget, 25f)),

                UpsertStep(folder, "14_ExitEdit", s => Configure(s, "exitEdit",
                    string.Empty, TracyMood.Still, TracyPresentation.HubFullBody,
                    "edit.done", false, TutorialAdvance.TapTarget, 25f)),

                UpsertStep(folder, "15_TapBook", s => Configure(s, "tapBook",
                    "tutorial.deskhub_lamp.tapBook", TracyMood.Happy, TracyPresentation.HubFullBody,
                    "desk.book", true, TutorialAdvance.TapTarget, 30f)),
            };

            return UpsertSequence("deskhub_lamp", seq =>
            {
                seq.trigger = TutorialTrigger.ScreenEntered;
                seq.triggerScreen = GameScreen.DeskHub;
                seq.triggerStageKind = StageKind.Scrub;
                seq.requiresSequence = "first_restoration";
                seq.requiresPosterCompleted = string.Empty;
                seq.steps = steps;
            });
        }

        // ---- journal_page2 ----------------------------------------------------------------

        private static TutorialSequenceData BuildJournalPage2()
        {
            var folder = $"{SequenceFolder}/journal_page2";

            var steps = new[]
            {
                UpsertStep(folder, "01_NewPoster", s => Configure(s, "newPoster",
                    "tutorial.journal_page2.newPoster", TracyMood.Happy, TracyPresentation.Portrait,
                    "", false, TutorialAdvance.TapAnywhere, 20f)),

                UpsertStep(folder, "02_NextPage", s => Configure(s, "nextPage",
                    "tutorial.journal_page2.nextPage", TracyMood.Still, TracyPresentation.Portrait,
                    "journal.nextPage", true, TutorialAdvance.TapTarget, 25f)),

                UpsertStep(folder, "03_Restore", s => Configure(s, "restore",
                    "tutorial.journal_page2.restore", TracyMood.Happy, TracyPresentation.Portrait,
                    "journal.restoreButton", true, TutorialAdvance.TapTarget, 25f)),
            };

            return UpsertSequence("journal_page2", seq =>
            {
                seq.trigger = TutorialTrigger.ScreenEntered;
                seq.triggerScreen = GameScreen.Journal;
                seq.triggerStageKind = StageKind.Scrub;
                seq.requiresSequence = "deskhub_lamp";
                seq.requiresPosterCompleted = "poster01";
                seq.steps = steps;
            });
        }

        // ---- stickers -----------------------------------------------------------------------

        private static TutorialSequenceData BuildStickers()
        {
            var folder = $"{SequenceFolder}/stickers";

            var steps = new[]
            {
                UpsertStep(folder, "01_Explain", s => Configure(s, "explain",
                    "tutorial.stickers.explain", TracyMood.Embarrassed, TracyPresentation.Portrait,
                    "", false, TutorialAdvance.TapAnywhere, 20f)),

                // Not gated: any un-peeled sticker should stay tappable, not just the
                // one the hand currently points at ("sticker.next" hops on its own).
                UpsertStep(folder, "02_Peel", s => Configure(s, "peel",
                    "tutorial.stickers.peel", TracyMood.Still, TracyPresentation.Portrait,
                    "sticker.next", false, TutorialAdvance.StickerPeeled, 45f)),

                UpsertStep(folder, "03_Rest", s => Configure(s, "rest",
                    "tutorial.stickers.rest", TracyMood.Happy, TracyPresentation.Portrait,
                    "", false, TutorialAdvance.TapAnywhere, 15f)),
            };

            return UpsertSequence("stickers", seq =>
            {
                seq.trigger = TutorialTrigger.StageKindStarted;
                seq.triggerScreen = GameScreen.None;
                seq.triggerStageKind = StageKind.StickerPeel;
                seq.requiresSequence = string.Empty;
                seq.requiresPosterCompleted = string.Empty;
                seq.steps = steps;
            });
        }

        // ---- Small authoring helpers --------------------------------------------------------

        private static void Configure(
            TutorialStepData step, string stepId, string lineKey, TracyMood mood,
            TracyPresentation presentation, string targetAnchorId, bool gateInputToTarget,
            TutorialAdvance advance, float autoAdvanceSeconds,
            GameScreen requiredScreen = GameScreen.None, ToolId requiredTool = ToolId.None,
            string requiredItemId = "")
        {
            step.stepId = stepId;
            step.lineKey = lineKey;
            step.mood = mood;
            step.presentation = presentation;
            step.targetAnchorId = targetAnchorId;
            step.gateInputToTarget = gateInputToTarget;
            step.pointerDelay = 0.4f;
            step.advance = advance;
            step.requiredScreen = requiredScreen;
            step.requiredTool = requiredTool;
            step.requiredItemId = requiredItemId ?? string.Empty;
            step.autoAdvanceSeconds = autoAdvanceSeconds;
        }

        private static TutorialStepData UpsertStep(string folder, string fileName, Action<TutorialStepData> configure)
        {
            EnsureFolder(folder);
            var path = $"{folder}/{fileName}.asset";
            var step = AssetDatabase.LoadAssetAtPath<TutorialStepData>(path);
            var isNew = step == null;

            if (isNew)
            {
                step = ScriptableObject.CreateInstance<TutorialStepData>();
            }

            configure(step);

            if (isNew)
            {
                AssetDatabase.CreateAsset(step, path);
            }
            else
            {
                EditorUtility.SetDirty(step);
            }

            return step;
        }

        private static TutorialSequenceData UpsertSequence(string sequenceId, Action<TutorialSequenceData> configure)
        {
            var path = $"{SequenceFolder}/{sequenceId}.asset";
            var seq = AssetDatabase.LoadAssetAtPath<TutorialSequenceData>(path);
            var isNew = seq == null;

            if (isNew)
            {
                seq = ScriptableObject.CreateInstance<TutorialSequenceData>();
                seq.sequenceId = sequenceId;
            }

            configure(seq);
            seq.sequenceId = sequenceId; // stable identity regardless of what configure() touches

            if (isNew)
            {
                AssetDatabase.CreateAsset(seq, path);
            }
            else
            {
                EditorUtility.SetDirty(seq);
            }

            return seq;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || folderPath == "Assets" || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');

            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
