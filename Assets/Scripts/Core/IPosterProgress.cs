// ============================================================
// IPosterProgress — per-poster progress and unlock rules.
// WHAT & WHY: Batch 2 turns the single hardcoded poster into a journal of many.
//   The journal, the flow controller and the tutorial all need to ask the same
//   questions ("is poster 2 unlocked? is poster 1 half done?") and they must get
//   the same answer. This is the one place those answers come from.
// KEY DECISIONS:
//   - Works on posterId strings and an ORDERED id list, never on PosterData.
//     That keeps the implementation plain C# with no UnityEngine dependency, so
//     the unlock rules are unit-tested outside Unity.
//   - Unlock rule: the first poster in the catalogue is always unlocked; every
//     other one unlocks when the poster before it is completed.
//   - The reward is claimed through its own idempotent call, separate from
//     MarkCompleted. If the app dies between the two writes, the next launch sees
//     completed && !rewardClaimed and pays it then — coins are never lost and
//     never paid twice.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by PosterProgressService, created and registered in
//     ServiceLocator by GameBootstrap.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Core
{
    public interface IPosterProgress
    {
        /// <summary>Raised with the posterId whenever that poster's progress changes.</summary>
        event Action<string> Changed;

        /// <summary>The poster on the workbench, or empty when none is in progress.</summary>
        string ActivePosterId { get; }

        /// <summary>True once at least one poster has been completed (unlocks the desk hub).</summary>
        bool AnyCompleted { get; }

        bool IsCompleted(string posterId);

        /// <summary>Started, not completed, with a valid stage to resume at.</summary>
        bool IsInProgress(string posterId);

        /// <summary>The stage to resume at, or -1 when there is none.</summary>
        int GetResumeStage(string posterId);

        /// <summary>First poster always; others once the previous one in <paramref name="orderedIds"/> is completed.</summary>
        bool IsUnlocked(string posterId, IReadOnlyList<string> orderedIds);

        /// <summary>
        /// Puts <paramref name="posterId"/> on the workbench. Creates its entry at
        /// stage 0 when it has none (or when it was completed — a replay), keeps
        /// the saved stage when it is in progress. Returns the stage to begin at.
        /// Saves immediately.
        /// </summary>
        int BeginOrResume(string posterId);

        /// <summary>Records the stage to resume at. <paramref name="immediate"/> = Save() instead of SaveSoon().</summary>
        void SetStage(string posterId, int stageIndex, bool immediate);

        /// <summary>
        /// Marks the poster completed and clears the workbench. Returns true only the
        /// FIRST time a poster is completed. Saves immediately.
        /// </summary>
        bool MarkCompleted(string posterId);

        /// <summary>Completed but base reward not yet paid.</summary>
        bool IsRewardPending(string posterId);

        /// <summary>Sets rewardClaimed. Returns false (and changes nothing) if already claimed.</summary>
        bool TryMarkRewardClaimed(string posterId);

        /// <summary>Sets rewardDoubled. Returns false (and changes nothing) if already doubled or not completed.</summary>
        bool TryMarkRewardDoubled(string posterId);

        bool IsRewardDoubled(string posterId);
    }
}
