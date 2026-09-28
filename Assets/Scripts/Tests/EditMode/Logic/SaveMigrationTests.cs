// ============================================================
// SaveMigrationTests — v1 (MVP) save -> v2 upgrade rules.
// WHAT & WHY: A v1 player must keep their half-done poster, their tutorial
//   progress and must receive the 100 coins the MVP never paid. These tests pin
//   every rule of SaveMigration.MigrateInPlace so a later edit cannot quietly
//   lose an old player's progress.
// KEY DECISIONS:
//   - Pure NUnit + the classes under test, no UnityEngine: runs under the Unity
//     Test Runner AND under plain `dotnet test`.
//   - The v1 state is written through the [Obsolete] fields exactly as
//     JsonUtility would leave them after reading an MVP save.json; the warning
//     is suppressed in this file only, like in SaveMigration itself.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Window -> General -> Test Runner, pick the "EditMode" tab and click
//     "Run All". Nothing else to set up.
// ---------------------------------------------------------------

using System.Collections.Generic;
using NUnit.Framework;
using RestoriumEmporium.Core;

namespace RestoriumEmporium.Tests
{
#pragma warning disable CS0618 // Tests must write the v1 fields to simulate an old save.
    public class SaveMigrationTests
    {
        private readonly List<string> _warnings = new List<string>();

        private SaveData NewV1()
        {
            _warnings.Clear();
            var data = new SaveData { version = 1 };
            return data;
        }

        private bool Migrate(SaveData data) => SaveMigration.MigrateInPlace(data, _warnings.Add);

        [Test]
        public void CurrentVersion_IsLeftUntouched()
        {
            var data = new SaveData();
            data.coins = 7;

            Assert.IsFalse(Migrate(data));
            Assert.AreEqual(7, data.coins);
            Assert.AreEqual(0, data.posters.Count);
        }

        [Test]
        public void NullData_ReturnsFalseAndWarns()
        {
            _warnings.Clear();
            Assert.IsFalse(SaveMigration.MigrateInPlace(null, _warnings.Add));
            Assert.AreEqual(1, _warnings.Count);
        }

        [Test]
        public void InProgressPoster_BecomesActiveEntryAtSameStage()
        {
            var data = NewV1();
            data.currentPosterId = "poster01";
            data.currentStageIndex = 3;
            data.hasPlayedBefore = true;

            Assert.IsTrue(Migrate(data));

            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual("poster01", data.activePosterId);
            var entry = data.FindPoster("poster01");
            Assert.IsNotNull(entry);
            Assert.AreEqual(3, entry.stageIndex);
            Assert.IsFalse(entry.completed);
            Assert.IsFalse(entry.rewardClaimed);
            Assert.AreEqual(0, data.coins);
        }

        [Test]
        public void CompletedPoster_IsClaimedAndPaysTheUnpaidReward()
        {
            var data = NewV1();
            data.currentPosterId = "poster01";
            data.currentStageIndex = 5;
            data.posterCompleted = true;
            data.coins = 20;

            Migrate(data);

            var entry = data.FindPoster("poster01");
            Assert.IsTrue(entry.completed);
            Assert.IsTrue(entry.rewardClaimed);
            Assert.IsFalse(entry.rewardDoubled);
            Assert.AreEqual(20 + SaveMigration.V1PosterReward, data.coins);
            Assert.AreEqual(string.Empty, data.activePosterId, "A completed poster is not on the bench.");
        }

        [Test]
        public void NoPoster_AddsNoEntry()
        {
            var data = NewV1();

            Migrate(data);

            Assert.AreEqual(0, data.posters.Count);
            Assert.AreEqual(string.Empty, data.activePosterId);
        }

        [Test]
        public void CompletedTutorial_BecomesCompletedSequence()
        {
            var data = NewV1();
            data.tutorialCompleted = true;
            data.tutorialStepIndex = 16;

            Migrate(data);

            CollectionAssert.Contains(data.completedTutorialSequences, "first_restoration");
            Assert.AreEqual(string.Empty, data.activeTutorialSequence);
        }

        [Test]
        public void UnfinishedTutorial_ResumesAtSameStep()
        {
            var data = NewV1();
            data.tutorialStepIndex = 4;

            Migrate(data);

            Assert.AreEqual("first_restoration", data.activeTutorialSequence);
            Assert.AreEqual(4, data.activeTutorialStepIndex);
            CollectionAssert.DoesNotContain(data.completedTutorialSequences, "first_restoration");
        }

        [Test]
        public void ReturningPlayer_HasIntroMarkedSeen()
        {
            var data = NewV1();
            data.hasPlayedBefore = true;

            Migrate(data);

            CollectionAssert.Contains(data.seenCutscenes, SaveMigration.IntroCutsceneId);
            Assert.AreEqual("intro", SaveMigration.IntroCutsceneId, "Must match CutsceneIds.Intro.");
        }

        [Test]
        public void NeverPlayed_IntroNotMarkedSeen()
        {
            var data = NewV1();

            Migrate(data);

            CollectionAssert.DoesNotContain(data.seenCutscenes, SaveMigration.IntroCutsceneId);
        }

        [Test]
        public void NullCollections_AreRepaired()
        {
            var data = NewV1();
            data.posters = null;
            data.seenCutscenes = null;
            data.completedTutorialSequences = null;
            data.currentPosterId = "poster01";
            data.currentStageIndex = 0;
            data.hasPlayedBefore = true;

            Assert.DoesNotThrow(() => Migrate(data));
            Assert.IsNotNull(data.FindPoster("poster01"));
        }

        [Test]
        public void MigratingTwice_DoesNotPayTwice()
        {
            var data = NewV1();
            data.currentPosterId = "poster01";
            data.posterCompleted = true;

            Migrate(data);
            var coins = data.coins;

            // Simulate a (wrongly) re-stamped file: still must not double pay.
            data.version = 1;
            data.currentPosterId = "poster01";
            data.posterCompleted = true;
            Migrate(data);

            Assert.AreEqual(coins, data.coins);
            Assert.AreEqual(1, data.posters.Count);
        }

        [Test]
        public void LegacyFields_AreClearedAfterMigration()
        {
            var data = NewV1();
            data.currentPosterId = "poster01";
            data.currentStageIndex = 2;
            data.tutorialCompleted = true;

            Migrate(data);

            Assert.AreEqual(string.Empty, data.currentPosterId);
            Assert.AreEqual(-1, data.currentStageIndex);
            Assert.IsFalse(data.posterCompleted);
            Assert.IsFalse(data.tutorialCompleted);
        }
    }
#pragma warning restore CS0618
}
