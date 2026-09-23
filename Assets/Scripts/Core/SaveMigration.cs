// ============================================================
// SaveMigration — upgrades an older save file to the current SaveData shape.
// WHAT & WHY: Players who installed the MVP have a version-1 save: ONE poster
//   tracked by currentPosterId/currentStageIndex/posterCompleted and ONE linear
//   tutorial tracked by tutorialCompleted/tutorialStepIndex. Batch 2 stores
//   progress per poster and per tutorial sequence. Without a migration those
//   players would silently lose their restoration and replay the tutorial.
//   SaveManager calls MigrateInPlace right after every load.
// KEY DECISIONS:
//   - Plain C# with no UnityEngine reference, so every rule is unit-tested
//     (SaveMigrationTests) without starting Unity. Warnings go through an
//     injected Action<string>.
//   - Migrates IN PLACE on the object JsonUtility just produced. JsonUtility
//     happily fills a v2 class from v1 JSON (unknown fields are ignored, missing
//     ones keep their defaults), so the v1 values are already sitting in the
//     [Obsolete] fields; this only moves them to their new home.
//   - This is the ONLY file allowed to read the [Obsolete] v1 fields. The
//     warning is suppressed here and nowhere else, so any other use still shows
//     up as a compiler warning.
//   - A poster the MVP marked completed also gets rewardClaimed=true AND its
//     reward added to coins. The MVP had no coins at all, so the player never
//     received those 100; marking it claimed without paying would cost them the
//     lamp the desk-hub tutorial asks them to buy.
//   - The reward is a constant (V1PosterReward = 100) rather than a lookup in
//     PosterData: the MVP shipped exactly one poster with no reward field, and
//     keeping the migration free of assets is what keeps it testable.
//   - The v1 fields are reset to their defaults afterwards. The save is stamped
//     v2 and will never be migrated again, but a stale "posterCompleted=true"
//     left in the file would mislead whoever reads it next while debugging.
//   - Idempotent: a save that is already v2 (or newer) is left untouched and the
//     call returns false.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# class; SaveManager calls it automatically after loading.
// [ ] To test by hand: put an MVP save.json in the save folder (see SaveManager's
//     checklist for the path), press Play from the Title scene, and check that the
//     poster resumes where it was and the Console shows "[SaveManager] Migrated".
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Core
{
    public static class SaveMigration
    {
        /// <summary>The coins the MVP's single poster is worth (it never paid them).</summary>
        public const int V1PosterReward = 100;

        /// <summary>Sequence id of the MVP's linear tutorial.</summary>
        public const string FirstRestorationSequence = "first_restoration";

        /// <summary>
        /// Must equal CutsceneIds.Intro. Duplicated as a literal because CutsceneIds
        /// lives next to a UnityEngine.Video type and this class must stay free of
        /// UnityEngine so it can be tested outside Unity.
        /// </summary>
        public const string IntroCutsceneId = "intro";

        /// <summary>
        /// Upgrades <paramref name="data"/> to <see cref="SaveData.CurrentVersion"/>.
        /// Returns true when anything was migrated (the caller should then write the file).
        /// </summary>
        public static bool MigrateInPlace(SaveData data, Action<string> warn)
        {
            if (data == null)
            {
                warn?.Invoke("[SaveMigration] Asked to migrate a null save. Nothing done.");
                return false;
            }

            if (data.version >= SaveData.CurrentVersion)
            {
                return false;
            }

            data.EnsureCollections();

            if (data.version <= 1)
            {
                MigrateV1ToV2(data, warn);
            }

            data.version = SaveData.CurrentVersion;
            return true;
        }

#pragma warning disable CS0618 // This is the one place allowed to read the v1 fields.
        private static void MigrateV1ToV2(SaveData data, Action<string> warn)
        {
            // ---- Poster progress ----
            var posterId = data.currentPosterId;

            if (!string.IsNullOrEmpty(posterId))
            {
                var entry = data.FindPoster(posterId);

                if (entry == null)
                {
                    entry = new PosterProgressEntry { posterId = posterId };
                    data.posters.Add(entry);
                }

                if (data.posterCompleted)
                {
                    entry.completed = true;
                    entry.stageIndex = -1;

                    if (!entry.rewardClaimed)
                    {
                        entry.rewardClaimed = true;
                        data.coins = SafeAdd(data.coins, V1PosterReward);
                    }
                }
                else if (data.currentStageIndex >= 0)
                {
                    entry.stageIndex = data.currentStageIndex;
                    data.activePosterId = posterId;
                }
            }
            else if (data.posterCompleted)
            {
                warn?.Invoke("[SaveMigration] v1 save says a poster was completed but names no " +
                             "poster. Ignoring the completion.");
            }

            // ---- Tutorial ----
            if (data.tutorialCompleted)
            {
                if (!data.completedTutorialSequences.Contains(FirstRestorationSequence))
                {
                    data.completedTutorialSequences.Add(FirstRestorationSequence);
                }
            }
            else if (!data.completedTutorialSequences.Contains(FirstRestorationSequence))
            {
                data.activeTutorialSequence = FirstRestorationSequence;
                data.activeTutorialStepIndex = Math.Max(0, data.tutorialStepIndex);
            }

            // ---- Cutscenes ----
            // A v1 player already started the game, so the intro would be a
            // surprise replay of a story they are past.
            if (data.hasPlayedBefore && !data.seenCutscenes.Contains(IntroCutsceneId))
            {
                data.seenCutscenes.Add(IntroCutsceneId);
            }

            // ---- Clear the legacy fields so the file stops advertising them ----
            data.currentPosterId = string.Empty;
            data.currentStageIndex = -1;
            data.posterCompleted = false;
            data.tutorialCompleted = false;
            data.tutorialStepIndex = 0;
        }
#pragma warning restore CS0618

        private static int SafeAdd(int a, int b)
        {
            var sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, sum);
        }
    }
}
