// ============================================================
// SaveData — the entire durable state of the game, as one serialisable POCO.
// WHAT & WHY: §4 of the build spec requires that the app survive being killed
//   at any instant. Keeping every durable field in one flat class means the
//   whole save is one JsonUtility round-trip with no reflection surprises and
//   no partial writes.
// KEY DECISIONS:
//   - Plain public fields, no properties: JsonUtility serialises fields only.
//     Lists of small [Serializable] classes are used instead of dictionaries for
//     the same reason (JsonUtility cannot serialise a Dictionary).
//   - A 'version' field is written from day one. v1 (the MVP) tracked ONE poster
//     with currentPosterId/currentStageIndex/posterCompleted and ONE linear
//     tutorial with tutorialStepIndex. v2 tracks progress per poster, tutorial
//     progress per sequence, coins and decorations. The v1 fields are KEPT (marked
//     Legacy) so SaveMigration can read an old file; nothing else may use them.
//   - Enums are stored as ints by JsonUtility, which is exactly why GameEnums
//     pins explicit numeric values.
//   - Progress is stored as (posterId, stageIndex) rather than a screen alone,
//     because the screen is derived from the stage — one source of truth for
//     "where was the player".
//   - Decoration positions are NORMALISED (0..1) inside the desk-hub room rect,
//     centre-anchored, so they survive any change of screen size or of the room
//     art's pixel size.
//   - This class holds no logic beyond trivial lookups. Rules (unlock order,
//     spending, migration) live in PosterProgressService, PlayerWallet,
//     DecorationInventory and SaveMigration, which are plain C# and unit-tested.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This is a data class; SaveManager owns the only instance.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Core
{
    [Serializable]
    public class SaveData
    {
        /// <summary>Bumped whenever the shape of this class changes.</summary>
        public int version = CurrentVersion;

        public const int CurrentVersion = 2;

        // ---- Settings ----
        /// <summary>
        /// Empty means "not chosen yet": the localization service picks from the
        /// device language on first launch and writes the result back here.
        /// </summary>
        public string localeCode = string.Empty;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;

        // ---- Flow ----
        /// <summary>Where to drop the player back on relaunch.</summary>
        public GameScreen lastScreen = GameScreen.Journal;

        /// <summary>True once the player has started a game at least once.</summary>
        public bool hasPlayedBefore;

        /// <summary>Ids of cutscenes that have played to the end (see CutsceneIds).</summary>
        public List<string> seenCutscenes = new List<string>();

        // ---- Restoration progress ----
        /// <summary>
        /// The poster currently on the workbench, i.e. the one to resume into on
        /// relaunch. Empty when no restoration is in progress.
        /// </summary>
        public string activePosterId = string.Empty;

        /// <summary>One entry per poster the player has ever started.</summary>
        public List<PosterProgressEntry> posters = new List<PosterProgressEntry>();

        // ---- Economy ----
        public int coins;

        /// <summary>Every decoration the player owns, with where it sits in the room.</summary>
        public List<OwnedDecoration> decorations = new List<OwnedDecoration>();

        /// <summary>
        /// Store transaction ids whose coin pack has been credited (CoinPurchaseService).
        /// Kept across ResetProgress so old purchases can never be granted twice. Added
        /// without a version bump: an older file simply has none, which EnsureCollections fixes.
        /// </summary>
        public List<string> grantedIapTransactions = new List<string>();

        // ---- Tutorial ----
        /// <summary>Sequence ids that have finished (or been skipped).</summary>
        public List<string> completedTutorialSequences = new List<string>();

        /// <summary>The sequence that was running when the app last saved, or empty.</summary>
        public string activeTutorialSequence = string.Empty;

        /// <summary>Step index inside activeTutorialSequence to resume at.</summary>
        public int activeTutorialStepIndex;

        // ---- Legacy v1 fields: read ONLY by SaveMigration ----
        [Obsolete("v1 only. Use tutorial sequences.")] public bool tutorialCompleted;
        [Obsolete("v1 only. Use tutorial sequences.")] public int tutorialStepIndex;
        [Obsolete("v1 only. Use activePosterId/posters.")] public string currentPosterId = string.Empty;
        [Obsolete("v1 only. Use activePosterId/posters.")] public int currentStageIndex = -1;
        [Obsolete("v1 only. Use posters[].completed.")] public bool posterCompleted;

        // ---- Trivial lookups (no rules here) ----

        /// <summary>The progress entry for <paramref name="posterId"/>, or null.</summary>
        public PosterProgressEntry FindPoster(string posterId)
        {
            if (string.IsNullOrEmpty(posterId) || posters == null)
            {
                return null;
            }

            for (var i = 0; i < posters.Count; i++)
            {
                if (posters[i] != null && string.Equals(posters[i].posterId, posterId, StringComparison.Ordinal))
                {
                    return posters[i];
                }
            }

            return null;
        }

        /// <summary>The owned-decoration record for <paramref name="itemId"/>, or null.</summary>
        public OwnedDecoration FindDecoration(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || decorations == null)
            {
                return null;
            }

            for (var i = 0; i < decorations.Count; i++)
            {
                if (decorations[i] != null && string.Equals(decorations[i].itemId, itemId, StringComparison.Ordinal))
                {
                    return decorations[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Replaces any null collection with an empty one. JsonUtility leaves a
        /// field null when the JSON omits it, which an older or hand-edited file
        /// can do; SaveManager calls this after every load.
        /// </summary>
        public void EnsureCollections()
        {
            seenCutscenes ??= new List<string>();
            posters ??= new List<PosterProgressEntry>();
            decorations ??= new List<OwnedDecoration>();
            grantedIapTransactions ??= new List<string>();
            completedTutorialSequences ??= new List<string>();
            localeCode ??= string.Empty;
            activePosterId ??= string.Empty;
            activeTutorialSequence ??= string.Empty;
        }
    }

    /// <summary>Durable progress for one poster.</summary>
    [Serializable]
    public class PosterProgressEntry
    {
        public string posterId = string.Empty;

        /// <summary>Stage to resume at. -1 means started-but-no-stage / not started.</summary>
        public int stageIndex = -1;

        /// <summary>The whole sequence has been finished at least once.</summary>
        public bool completed;

        /// <summary>The base coin reward has been paid. Written in the same save as 'completed'.</summary>
        public bool rewardClaimed;

        /// <summary>The "double reward" bonus has been paid. At most once per poster.</summary>
        public bool rewardDoubled;
    }

    /// <summary>One owned decoration and its placement in the desk-hub room.</summary>
    [Serializable]
    public class OwnedDecoration
    {
        public string itemId = string.Empty;

        /// <summary>Centre of the item, normalised to the room rect (0,0 bottom-left, 1,1 top-right).</summary>
        public float x = 0.5f;
        public float y = 0.5f;
    }
}
