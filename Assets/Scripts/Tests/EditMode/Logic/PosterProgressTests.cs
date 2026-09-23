// ============================================================
// PosterProgressTests — unlock order, resume and reward idempotence.
// WHAT & WHY: PosterProgressService decides which journal pages are locked,
//   where a relaunch resumes, and whether a reward may be paid. A mistake there
//   is either a soft-lock (poster 2 never unlocks) or free coins, so each rule
//   is pinned here.
// KEY DECISIONS:
//   - Pure NUnit, no UnityEngine; an in-memory FakeSave counts Save() and
//     SaveSoon() calls so the "which write happens when" rules are testable.
//   - The single-write reward rule is tested end to end with a real PlayerWallet
//     on the same FakeSave: flag + coins must reach "disk" in one Save().
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Window -> General -> Test Runner, pick the "EditMode" tab and click
//     "Run All". Nothing else to set up.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using NUnit.Framework;
using RestoriumEmporium.Core;
using RestoriumEmporium.Economy;

namespace RestoriumEmporium.Tests
{
    public class PosterProgressTests
    {
        private sealed class FakeSave : ISaveService
        {
            public SaveData Data { get; private set; } = new SaveData();
            public int SaveCount;
            public int SaveSoonCount;

            // Snapshot of what the last Save() would have put on disk.
            public int DiskCoins;
            public bool DiskRewardClaimed;

            public event Action Reloaded;

            public void Save()
            {
                SaveCount++;
                DiskCoins = Data.coins;
                var entry = Data.FindPoster("p1");
                DiskRewardClaimed = entry != null && entry.rewardClaimed;
            }

            public void SaveSoon() => SaveSoonCount++;
            public void Load() => Reloaded?.Invoke();

            public void ResetProgress()
            {
                Data = new SaveData();
                Reloaded?.Invoke();
            }
        }

        private static readonly IReadOnlyList<string> Order = new[] { "p1", "p2", "p3" };

        private FakeSave _save;
        private List<string> _warnings;
        private PosterProgressService _progress;

        [SetUp]
        public void SetUp()
        {
            _save = new FakeSave();
            _warnings = new List<string>();
            _progress = new PosterProgressService(_save, _warnings.Add);
        }

        // ---- Unlock order ----

        [Test]
        public void FirstPoster_IsAlwaysUnlocked()
        {
            Assert.IsTrue(_progress.IsUnlocked("p1", Order));
            Assert.IsFalse(_progress.IsUnlocked("p2", Order));
            Assert.IsFalse(_progress.IsUnlocked("p3", Order));
        }

        [Test]
        public void CompletingAPoster_UnlocksOnlyTheNextOne()
        {
            _progress.BeginOrResume("p1");
            _progress.MarkCompleted("p1");

            Assert.IsTrue(_progress.IsUnlocked("p2", Order));
            Assert.IsFalse(_progress.IsUnlocked("p3", Order));
        }

        [Test]
        public void InProgressPrevious_DoesNotUnlockNext()
        {
            _progress.BeginOrResume("p1");
            _progress.SetStage("p1", 3, immediate: true);

            Assert.IsFalse(_progress.IsUnlocked("p2", Order));
        }

        [Test]
        public void UnknownPoster_IsLockedWithWarning()
        {
            Assert.IsFalse(_progress.IsUnlocked("nope", Order));
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void AnyCompleted_FlipsOnFirstCompletion()
        {
            Assert.IsFalse(_progress.AnyCompleted);
            _progress.MarkCompleted("p1");
            Assert.IsTrue(_progress.AnyCompleted);
        }

        // ---- Begin / resume ----

        [Test]
        public void BeginOrResume_NewPoster_StartsAtZeroAndSaves()
        {
            var stage = _progress.BeginOrResume("p1");

            Assert.AreEqual(0, stage);
            Assert.AreEqual("p1", _progress.ActivePosterId);
            Assert.IsTrue(_progress.IsInProgress("p1"));
            Assert.AreEqual(1, _save.SaveCount);
        }

        [Test]
        public void BeginOrResume_InProgress_KeepsSavedStage()
        {
            _progress.BeginOrResume("p1");
            _progress.SetStage("p1", 4, immediate: true);

            Assert.AreEqual(4, _progress.BeginOrResume("p1"));
            Assert.AreEqual(4, _progress.GetResumeStage("p1"));
        }

        [Test]
        public void BeginOrResume_CompletedPoster_ReplaysFromZeroButStaysCompleted()
        {
            _progress.BeginOrResume("p1");
            _progress.MarkCompleted("p1");
            _progress.TryMarkRewardClaimed("p1");

            Assert.AreEqual(0, _progress.BeginOrResume("p1"));
            Assert.IsTrue(_progress.IsCompleted("p1"));
            Assert.IsFalse(_progress.IsRewardPending("p1"), "A replay must never re-open the reward.");
        }

        [Test]
        public void BeginOrResume_EmptyId_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, _progress.BeginOrResume(""));
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void SetStage_SoonUsesSaveSoon_ImmediateUsesSave()
        {
            _progress.BeginOrResume("p1");
            var saves = _save.SaveCount;

            _progress.SetStage("p1", 1, immediate: false);
            Assert.AreEqual(saves, _save.SaveCount);
            Assert.AreEqual(1, _save.SaveSoonCount);

            _progress.SetStage("p1", 2, immediate: true);
            Assert.AreEqual(saves + 1, _save.SaveCount);
        }

        [Test]
        public void SetStage_Negative_IsIgnored()
        {
            _progress.BeginOrResume("p1");
            _progress.SetStage("p1", -3, immediate: true);

            Assert.AreEqual(0, _progress.GetResumeStage("p1"));
        }

        [Test]
        public void NotStarted_HasNoResumeStage()
        {
            Assert.IsFalse(_progress.IsInProgress("p1"));
            Assert.AreEqual(-1, _progress.GetResumeStage("p1"));
        }

        // ---- Completion ----

        [Test]
        public void MarkCompleted_TrueOnlyTheFirstTime_AndClearsBench()
        {
            _progress.BeginOrResume("p1");

            Assert.IsTrue(_progress.MarkCompleted("p1"));
            Assert.IsFalse(_progress.MarkCompleted("p1"));
            Assert.AreEqual(string.Empty, _progress.ActivePosterId);
            Assert.IsFalse(_progress.IsInProgress("p1"));
        }

        [Test]
        public void Changed_IsRaisedWithPosterId()
        {
            var raised = new List<string>();
            _progress.Changed += raised.Add;

            _progress.BeginOrResume("p1");
            _progress.MarkCompleted("p1");

            CollectionAssert.AreEqual(new[] { "p1", "p1" }, raised);
        }

        // ---- Rewards ----

        [Test]
        public void Reward_PendingAfterCompletion_ClaimedOnce()
        {
            _progress.MarkCompleted("p1");

            Assert.IsTrue(_progress.IsRewardPending("p1"));
            Assert.IsTrue(_progress.TryMarkRewardClaimed("p1"));
            Assert.IsFalse(_progress.IsRewardPending("p1"));
            Assert.IsFalse(_progress.TryMarkRewardClaimed("p1"));
        }

        [Test]
        public void Reward_CannotBeClaimedBeforeCompletion()
        {
            _progress.BeginOrResume("p1");

            Assert.IsFalse(_progress.IsRewardPending("p1"));
            Assert.IsFalse(_progress.TryMarkRewardClaimed("p1"));
        }

        [Test]
        public void Double_OnlyOnceAndOnlyWhenCompleted()
        {
            Assert.IsFalse(_progress.TryMarkRewardDoubled("p1"));

            _progress.MarkCompleted("p1");

            Assert.IsTrue(_progress.TryMarkRewardDoubled("p1"));
            Assert.IsTrue(_progress.IsRewardDoubled("p1"));
            Assert.IsFalse(_progress.TryMarkRewardDoubled("p1"));
        }

        [Test]
        public void RewardFlag_AndCoins_ReachDiskInOneWrite()
        {
            var wallet = new PlayerWallet(_save, _warnings.Add);
            _progress.MarkCompleted("p1");
            var saves = _save.SaveCount;

            Assert.IsTrue(_progress.TryMarkRewardClaimed("p1"));

            // Flag alone must not hit disk: a kill here pays again next launch,
            // which is correct because the coins were not written either.
            Assert.AreEqual(saves, _save.SaveCount);
            Assert.IsFalse(_save.DiskRewardClaimed);

            wallet.Add(100, "test");

            Assert.AreEqual(saves + 1, _save.SaveCount);
            Assert.IsTrue(_save.DiskRewardClaimed);
            Assert.AreEqual(100, _save.DiskCoins);
        }

        [Test]
        public void ResetProgress_IsSeenImmediately()
        {
            _progress.BeginOrResume("p1");
            _progress.MarkCompleted("p1");

            _save.ResetProgress();

            Assert.IsFalse(_progress.AnyCompleted);
            Assert.IsFalse(_progress.IsUnlocked("p2", Order));
        }

        [Test]
        public void NullSave_DoesNotThrow()
        {
            var progress = new PosterProgressService(null, _warnings.Add);

            Assert.DoesNotThrow(() =>
            {
                progress.BeginOrResume("p1");
                progress.SetStage("p1", 2, immediate: false);
                progress.MarkCompleted("p1");
                progress.TryMarkRewardClaimed("p1");
            });
            Assert.IsTrue(progress.IsCompleted("p1"));
        }
    }
}
