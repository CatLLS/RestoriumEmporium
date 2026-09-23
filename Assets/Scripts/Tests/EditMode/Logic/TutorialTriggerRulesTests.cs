// ============================================================
// TutorialTriggerRulesTests — which tutorial starts, and when.
// WHAT & WHY: A tutorial that replays forever or never appears is the classic
//   tutorial bug. These pin the start rules (once only, prerequisites, first
//   match wins) with plain NUnit, no UnityEngine.
// KEY DECISIONS:
//   - Candidates mirror the real Batch 2 sequence table (contract §6) so the
//     tests read like the design.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Window -> General -> Test Runner -> EditMode -> Run All.
// ---------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using RestoriumEmporium.Core;
using RestoriumEmporium.Tutorial;

namespace RestoriumEmporium.Tests.Logic
{
    public class TutorialTriggerRulesTests
    {
        private static TriggerCandidate[] Table() => new[]
        {
            new TriggerCandidate { SequenceId = "first_restoration", TriggerScreen = GameScreen.Journal },
            new TriggerCandidate
            {
                SequenceId = "deskhub_lamp", TriggerScreen = GameScreen.DeskHub,
                RequiresSequence = "first_restoration"
            },
            new TriggerCandidate
            {
                SequenceId = "journal_page2", TriggerScreen = GameScreen.Journal,
                RequiresSequence = "deskhub_lamp", RequiresPosterCompleted = "poster01"
            },
            new TriggerCandidate
            {
                SequenceId = "stickers", HasStageKind = true, TriggerStageKind = StageKind.StickerPeel
            }
        };

        [Test]
        public void FreshGame_JournalStartsFirstRestoration()
        {
            var done = new List<string>();

            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.Journal, done, _ => false),
                Is.EqualTo(0));
        }

        [Test]
        public void CompletedSequence_NeverStartsAgain()
        {
            var done = new List<string> { "first_restoration" };

            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.Journal, done, _ => false),
                Is.EqualTo(-1), "journal_page2 still needs deskhub_lamp; first_restoration is done.");
        }

        [Test]
        public void DeskHub_RequiresFirstRestoration()
        {
            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.DeskHub, new List<string>(), _ => false),
                Is.EqualTo(-1));
            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.DeskHub,
                new List<string> { "first_restoration" }, _ => false), Is.EqualTo(1));
        }

        [Test]
        public void JournalPage2_NeedsBothSequenceAndPoster()
        {
            var done = new List<string> { "first_restoration", "deskhub_lamp" };

            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.Journal, done, _ => false),
                Is.EqualTo(-1), "poster01 not completed yet");
            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.Journal, done, id => id == "poster01"),
                Is.EqualTo(2));
        }

        [Test]
        public void StageKind_MatchesOnlyItsKind()
        {
            var done = new List<string>();

            Assert.That(TutorialTriggerRules.FindForStageKind(Table(), StageKind.StickerPeel, done, _ => false),
                Is.EqualTo(3));
            Assert.That(TutorialTriggerRules.FindForStageKind(Table(), StageKind.Scrub, done, _ => false),
                Is.EqualTo(-1));
        }

        [Test]
        public void ScreenNone_NeverTriggers()
        {
            Assert.That(TutorialTriggerRules.FindForScreen(Table(), GameScreen.None, new List<string>(), _ => true),
                Is.EqualTo(-1));
        }

        [Test]
        public void BlankId_CannotStart()
        {
            Assert.That(TutorialTriggerRules.CanStart(new TriggerCandidate(), new List<string>(), _ => true), Is.False);
        }

        [Test]
        public void ResumeIndex_IsClamped()
        {
            Assert.That(TutorialTriggerRules.ClampResumeIndex(-2, 5), Is.EqualTo(0));
            Assert.That(TutorialTriggerRules.ClampResumeIndex(3, 5), Is.EqualTo(3));
            Assert.That(TutorialTriggerRules.ClampResumeIndex(9, 5), Is.EqualTo(5));
        }

        [Test]
        public void ItemMatches_BlankMeansAny()
        {
            Assert.That(TutorialTriggerRules.ItemMatches("", "plant"), Is.True);
            Assert.That(TutorialTriggerRules.ItemMatches("lamp", "lamp"), Is.True);
            Assert.That(TutorialTriggerRules.ItemMatches("lamp", "plant"), Is.False);
        }
    }
}
