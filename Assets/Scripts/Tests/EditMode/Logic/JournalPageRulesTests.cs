// ============================================================
// JournalPageRulesTests — pins down what each journal page shows.
// WHAT & WHY: The journal's button caption / enabled state / opening page are
//   what stop a player from starting a locked poster or re-buying a finished
//   one. These run with plain NUnit (no UnityEngine) under dotnet test and in
//   Unity's EditMode Test Runner.
// KEY DECISIONS:
//   - A tiny in-test FakeProgress implements IPosterProgress with the contract's
//     unlock rule, so the tests exercise JournalPageRules only, not the real
//     service (which has its own tests).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Window -> General -> Test Runner -> EditMode -> Run All.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RestoriumEmporium.Core;
using RestoriumEmporium.UI;

namespace RestoriumEmporium.Tests.Logic
{
    public class JournalPageRulesTests
    {
        private static readonly IReadOnlyList<string> Ids = new[] { "poster01", "poster02", "poster03" };

        private sealed class FakeProgress : IPosterProgress
        {
            public readonly HashSet<string> Completed = new HashSet<string>();
            public string Active = string.Empty;

            public event Action<string> Changed { add { } remove { } }
            public string ActivePosterId => Active;
            public bool AnyCompleted => Completed.Count > 0;
            public bool IsCompleted(string posterId) => Completed.Contains(posterId);
            public bool IsInProgress(string posterId) => posterId == Active && !Completed.Contains(posterId);
            public int GetResumeStage(string posterId) => IsInProgress(posterId) ? 1 : -1;

            public bool IsUnlocked(string posterId, IReadOnlyList<string> orderedIds)
            {
                for (var i = 0; i < orderedIds.Count; i++)
                {
                    if (orderedIds[i] == posterId)
                    {
                        return i == 0 || Completed.Contains(orderedIds[i - 1]);
                    }
                }

                return false;
            }

            public int BeginOrResume(string posterId) => 0;
            public void SetStage(string posterId, int stageIndex, bool immediate) { }
            public bool MarkCompleted(string posterId) => Completed.Add(posterId);
            public bool IsRewardPending(string posterId) => false;
            public bool TryMarkRewardClaimed(string posterId) => false;
            public bool TryMarkRewardDoubled(string posterId) => false;
            public bool IsRewardDoubled(string posterId) => false;
        }

        [Test]
        public void FreshGame_FirstPageAvailable_OthersLocked()
        {
            var p = new FakeProgress();

            Assert.That(JournalPageRules.Evaluate(p, "poster01", Ids), Is.EqualTo(JournalPageState.Available));
            Assert.That(JournalPageRules.Evaluate(p, "poster02", Ids), Is.EqualTo(JournalPageState.Locked));
        }

        [Test]
        public void CompletingPoster1_UnlocksPoster2()
        {
            var p = new FakeProgress();
            p.Completed.Add("poster01");

            Assert.That(JournalPageRules.Evaluate(p, "poster01", Ids), Is.EqualTo(JournalPageState.Completed));
            Assert.That(JournalPageRules.Evaluate(p, "poster02", Ids), Is.EqualTo(JournalPageState.Available));
            Assert.That(JournalPageRules.Evaluate(p, "poster03", Ids), Is.EqualTo(JournalPageState.Locked));
        }

        [Test]
        public void ActivePoster_IsInProgress()
        {
            var p = new FakeProgress { Active = "poster01" };

            Assert.That(JournalPageRules.Evaluate(p, "poster01", Ids), Is.EqualTo(JournalPageState.InProgress));
        }

        [Test]
        public void NoProgressService_FirstPageStillPlayable()
        {
            Assert.That(JournalPageRules.Evaluate(null, "poster01", Ids), Is.EqualTo(JournalPageState.Available));
            Assert.That(JournalPageRules.Evaluate(null, "poster02", Ids), Is.EqualTo(JournalPageState.Locked));
        }

        [TestCase(JournalPageState.Locked, "ui.journal.restore", false)]
        [TestCase(JournalPageState.Available, "ui.journal.restore", true)]
        [TestCase(JournalPageState.InProgress, "ui.journal.continue", true)]
        [TestCase(JournalPageState.Completed, "ui.journal.restored", false)]
        public void ButtonLabelAndInteractable(JournalPageState state, string key, bool interactable)
        {
            Assert.That(JournalPageRules.ButtonLabelKey(state), Is.EqualTo(key));
            Assert.That(JournalPageRules.IsButtonInteractable(state), Is.EqualTo(interactable));
        }

        [Test]
        public void Arrows_OnlyWhenAPageExistsThatWay()
        {
            Assert.That(JournalPageRules.HasPreviousPage(0), Is.False);
            Assert.That(JournalPageRules.HasPreviousPage(1), Is.True);
            Assert.That(JournalPageRules.HasNextPage(0, 2), Is.True);
            Assert.That(JournalPageRules.HasNextPage(1, 2), Is.False);
            Assert.That(JournalPageRules.HasNextPage(0, 1), Is.False);
        }

        [Test]
        public void ClampPage_StaysInRange()
        {
            Assert.That(JournalPageRules.ClampPage(-3, 2), Is.EqualTo(0));
            Assert.That(JournalPageRules.ClampPage(9, 2), Is.EqualTo(1));
            Assert.That(JournalPageRules.ClampPage(5, 0), Is.EqualTo(0));
        }

        [Test]
        public void InitialPage_PrefersActiveThenLastCompletedThenFirstOpen()
        {
            var p = new FakeProgress();
            p.Completed.Add("poster01");

            Assert.That(JournalPageRules.InitialPageIndex(Ids, p, "poster02", "poster01"), Is.EqualTo(1),
                "The poster on the bench wins.");
            Assert.That(JournalPageRules.InitialPageIndex(Ids, p, "", "poster01"), Is.EqualTo(0),
                "Just finished: open on it so the next-page arrow leads onward.");
            Assert.That(JournalPageRules.InitialPageIndex(Ids, p, "", ""), Is.EqualTo(1),
                "Otherwise the first page with work left.");
        }

        [Test]
        public void InitialPage_EmptyCatalogue_IsZero()
        {
            Assert.That(JournalPageRules.InitialPageIndex(new string[0], new FakeProgress(), "x", "y"), Is.EqualTo(0));
        }
    }
}
