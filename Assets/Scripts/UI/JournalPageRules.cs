// ============================================================
// JournalPageRules — what one journal page shows, decided in plain C#.
// WHAT & WHY: The journal has one page per poster. Each page is in one of four
//   states (Locked / Available / InProgress / Completed) and the state decides
//   the button caption, whether the button can be pressed, and which sprite the
//   page draws. Those rules are small but easy to get subtly wrong ("can I press
//   Restore on a finished poster?"), so they live here, with no UnityEngine
//   reference, where NUnit can pin them down outside Unity.
// KEY DECISIONS:
//   - Works on posterId strings + IPosterProgress, never on PosterData, for the
//     same reason IPosterProgress does: it keeps the rules testable with dotnet.
//   - Completed wins over every other state. A finished poster shows its
//     restored art and a disabled "Restored" button; replays are not a feature
//     of this build.
//   - Locked pages still draw the poster (at low opacity, like Available ones)
//     so the player can see what is coming; only the button is disabled.
//   - The page the journal OPENS on is a rule too (InitialPageIndex): the poster
//     on the bench first, then the poster just finished (so the player sees
//     their work and the arrow invites them onward — this is exactly what the
//     "journal_page2" tutorial points at), then the first page still to do.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# rules used by JournalScreen. No component to attach.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;

    public enum JournalPageState
    {
        /// <summary>The previous poster is not finished yet. Button disabled.</summary>
        Locked = 0,

        /// <summary>Unlocked and never started. Button says "Restore".</summary>
        Available = 1,

        /// <summary>Started and saved mid-way. Button says "Continue".</summary>
        InProgress = 2,

        /// <summary>Finished. Shows the restored art; button says "Restored", disabled.</summary>
        Completed = 3
    }

    public static class JournalPageRules
    {
        public const string RestoreKey = "ui.journal.restore";
        public const string ContinueKey = "ui.journal.continue";
        public const string RestoredKey = "ui.journal.restored";

        /// <summary>
        /// State of the page for <paramref name="posterId"/>. With no progress service
        /// (a scene opened on its own in the Editor) the first page is Available and
        /// every other page Locked, so the journal is still usable.
        /// </summary>
        public static JournalPageState Evaluate(
            IPosterProgress progress, string posterId, IReadOnlyList<string> orderedIds)
        {
            if (string.IsNullOrEmpty(posterId))
            {
                return JournalPageState.Locked;
            }

            if (progress == null)
            {
                return orderedIds != null && orderedIds.Count > 0 &&
                       string.Equals(orderedIds[0], posterId, StringComparison.Ordinal)
                    ? JournalPageState.Available
                    : JournalPageState.Locked;
            }

            if (progress.IsCompleted(posterId))
            {
                return JournalPageState.Completed;
            }

            if (progress.IsInProgress(posterId))
            {
                return JournalPageState.InProgress;
            }

            return progress.IsUnlocked(posterId, orderedIds)
                ? JournalPageState.Available
                : JournalPageState.Locked;
        }

        /// <summary>Localisation key for the page's main button.</summary>
        public static string ButtonLabelKey(JournalPageState state)
        {
            switch (state)
            {
                case JournalPageState.InProgress:
                    return ContinueKey;
                case JournalPageState.Completed:
                    return RestoredKey;
                default:
                    return RestoreKey;
            }
        }

        /// <summary>Only Available and InProgress pages can be started from the journal.</summary>
        public static bool IsButtonInteractable(JournalPageState state) =>
            state == JournalPageState.Available || state == JournalPageState.InProgress;

        /// <summary>Completed pages draw the restored art; every other state draws the preview.</summary>
        public static bool ShowsFinalArt(JournalPageState state) => state == JournalPageState.Completed;

        public static bool HasPreviousPage(int index) => index > 0;

        public static bool HasNextPage(int index, int pageCount) => index >= 0 && index < pageCount - 1;

        /// <summary>Clamps any index into the valid page range (0 when there are no pages).</summary>
        public static int ClampPage(int index, int pageCount)
        {
            if (pageCount <= 0)
            {
                return 0;
            }

            return index < 0 ? 0 : (index >= pageCount ? pageCount - 1 : index);
        }

        /// <summary>
        /// The page the journal opens on: the poster on the bench, else the poster
        /// just finished, else the first page that still has work to do, else page 0.
        /// </summary>
        public static int InitialPageIndex(
            IReadOnlyList<string> orderedIds, IPosterProgress progress,
            string activePosterId, string lastCompletedPosterId)
        {
            if (orderedIds == null || orderedIds.Count == 0)
            {
                return 0;
            }

            var active = IndexOf(orderedIds, activePosterId);

            if (active >= 0)
            {
                return active;
            }

            var last = IndexOf(orderedIds, lastCompletedPosterId);

            if (last >= 0)
            {
                return last;
            }

            for (var i = 0; i < orderedIds.Count; i++)
            {
                var state = Evaluate(progress, orderedIds[i], orderedIds);

                if (state == JournalPageState.Available || state == JournalPageState.InProgress)
                {
                    return i;
                }
            }

            return 0;
        }

        private static int IndexOf(IReadOnlyList<string> ids, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            for (var i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
