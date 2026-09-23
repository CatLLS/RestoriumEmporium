// ============================================================
// TutorialTriggerRules — which tutorial sequence (if any) should start now.
// WHAT & WHY: Batch 2 has several tutorial sequences that start by themselves
//   when something happens (the journal opens, the desk hub opens, a sticker
//   stage begins) — but only once, and only after their prerequisites. Getting
//   that wrong either replays a tutorial forever or never shows it, so the
//   decision is extracted into plain C# (no UnityEngine) and unit-tested.
// KEY DECISIONS:
//   - TutorialSequenceData is a ScriptableObject (UnityEngine), so the controller
//     copies the few fields that matter into a TriggerCandidate struct. The rules
//     then never touch a Unity type and run under dotnet test.
//   - A candidate listens to at most one screen and/or one stage kind; "no screen"
//     is GameScreen.None and "no stage kind" is a false HasStageKind, so a Manual
//     sequence is simply a candidate that listens to nothing.
//   - First match in list order wins. Sequence order in the controller's array is
//     therefore also priority order, which is visible in the Inspector.
//   - Item matching treats a blank required id as "any item", mirroring the
//     TutorialStepData tooltip.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# rules used by TutorialController.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Tutorial
{
    using RestoriumEmporium.Core;

    /// <summary>The trigger-relevant facts of one sequence, copied out of its asset.</summary>
    public struct TriggerCandidate
    {
        public string SequenceId;

        /// <summary>Start when this screen is entered. None = not screen-triggered.</summary>
        public GameScreen TriggerScreen;

        /// <summary>True when the sequence starts on a stage of kind TriggerStageKind.</summary>
        public bool HasStageKind;
        public StageKind TriggerStageKind;

        /// <summary>Blank = no requirement.</summary>
        public string RequiresSequence;

        /// <summary>Blank = no requirement.</summary>
        public string RequiresPosterCompleted;
    }

    public static class TutorialTriggerRules
    {
        /// <summary>
        /// True when the sequence has not completed yet and its prerequisites are met.
        /// Ignores the trigger itself; use the Find* methods for that.
        /// </summary>
        public static bool CanStart(
            TriggerCandidate candidate, ICollection<string> completedSequences,
            Func<string, bool> isPosterCompleted)
        {
            if (string.IsNullOrEmpty(candidate.SequenceId))
            {
                return false;
            }

            if (Contains(completedSequences, candidate.SequenceId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(candidate.RequiresSequence) &&
                !Contains(completedSequences, candidate.RequiresSequence))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(candidate.RequiresPosterCompleted) &&
                (isPosterCompleted == null || !isPosterCompleted(candidate.RequiresPosterCompleted)))
            {
                return false;
            }

            return true;
        }

        /// <summary>Index of the first startable sequence triggered by entering <paramref name="screen"/>, or -1.</summary>
        public static int FindForScreen(
            IReadOnlyList<TriggerCandidate> candidates, GameScreen screen,
            ICollection<string> completedSequences, Func<string, bool> isPosterCompleted)
        {
            if (candidates == null || screen == GameScreen.None)
            {
                return -1;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];

                if (c.TriggerScreen == screen && CanStart(c, completedSequences, isPosterCompleted))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Index of the first startable sequence triggered by a stage of <paramref name="kind"/>, or -1.</summary>
        public static int FindForStageKind(
            IReadOnlyList<TriggerCandidate> candidates, StageKind kind,
            ICollection<string> completedSequences, Func<string, bool> isPosterCompleted)
        {
            if (candidates == null)
            {
                return -1;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];

                if (c.HasStageKind && c.TriggerStageKind == kind &&
                    CanStart(c, completedSequences, isPosterCompleted))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Where to resume a saved sequence: the saved index clamped into range.
        /// A saved index at or past the end means "already finished".
        /// </summary>
        public static int ClampResumeIndex(int savedIndex, int stepCount)
        {
            if (savedIndex < 0)
            {
                return 0;
            }

            return savedIndex > stepCount ? stepCount : savedIndex;
        }

        /// <summary>Blank required id matches any item.</summary>
        public static bool ItemMatches(string requiredItemId, string actualItemId)
        {
            return string.IsNullOrEmpty(requiredItemId) ||
                   string.Equals(requiredItemId, actualItemId, StringComparison.Ordinal);
        }

        private static bool Contains(ICollection<string> set, string id)
        {
            if (set == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            // ICollection.Contains on a List<string> uses the default (ordinal) comparer.
            return set.Contains(id);
        }
    }
}
