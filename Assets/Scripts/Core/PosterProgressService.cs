// ============================================================
// PosterProgressService — per-poster progress, unlock order and reward flags.
// WHAT & WHY: Implements IPosterProgress on top of SaveData.posters. The
//   journal (lock/continue/restored states), the flow controller (resume,
//   rewards) and the tutorial (is poster 1 done?) all ask it the same questions,
//   so the answers come from one set of rules instead of three copies.
// KEY DECISIONS:
//   - Plain C# with no UnityEngine reference, so unlock order, resume and reward
//     idempotence are unit-tested (PosterProgressTests). GameBootstrap passes
//     Debug.LogWarning as 'warn'.
//   - Reads ISaveService.Data on every call instead of caching the SaveData
//     object. ResetProgress() and Load() replace that object; a cached copy
//     would quietly keep writing into the discarded one.
//   - Milestones (BeginOrResume, MarkCompleted, an immediate SetStage) call
//     Save(); a stage beginning uses SaveSoon() because the previous stage's
//     completion was already written synchronously.
//   - The two reward flags (TryMarkRewardClaimed / TryMarkRewardDoubled) use
//     SaveSoon(), deliberately. The caller pays the coins right after through
//     IWallet.Add, which calls Save() on the SAME SaveData — so the flag and the
//     coins land on disk in ONE atomic write. Saving the flag on its own first
//     would open a window where a kill loses the coins; paying first would open
//     one where a kill pays them twice. If nothing pays afterwards, SaveSoon
//     still flushes on the next frame (or on pause/quit).
//   - A completed poster stays completed on a replay (BeginOrResume restarts it
//     at stage 0 but keeps completed/rewardClaimed), so a replay can never pay
//     the base reward a second time.
//   - Invalid ids are warnings and no-ops, never exceptions: a mistyped posterId
//     in an asset must not crash the flow.
//   - Changed is raised with the posterId after every real change, so the
//     journal can refresh a single page.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. GameBootstrap creates it and registers it as IPosterProgress.
//     Get it with ServiceLocator.Get<IPosterProgress>() in Start().
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Core
{
    public sealed class PosterProgressService : IPosterProgress
    {
        private readonly ISaveService _save;
        private readonly Action<string> _warn;

        // Only used when no save service exists (a scene opened on its own in
        // the Editor): the game still plays, it just remembers nothing.
        private readonly SaveData _fallback = new SaveData();

        /// <inheritdoc />
        public event Action<string> Changed;

        public PosterProgressService(ISaveService save, Action<string> warn)
        {
            _save = save;
            _warn = warn;

            if (_save == null)
            {
                Warn("No ISaveService given. Poster progress will not be saved.");
            }
        }

        private SaveData Data
        {
            get
            {
                var data = _save?.Data ?? _fallback;
                data.EnsureCollections();
                return data;
            }
        }

        /// <inheritdoc />
        public string ActivePosterId => Data.activePosterId ?? string.Empty;

        /// <inheritdoc />
        public bool AnyCompleted
        {
            get
            {
                var posters = Data.posters;

                for (var i = 0; i < posters.Count; i++)
                {
                    if (posters[i] != null && posters[i].completed)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <inheritdoc />
        public bool IsCompleted(string posterId)
        {
            var entry = Data.FindPoster(posterId);
            return entry != null && entry.completed;
        }

        /// <inheritdoc />
        public bool IsInProgress(string posterId)
        {
            var entry = Data.FindPoster(posterId);
            return entry != null && !entry.completed && entry.stageIndex >= 0;
        }

        /// <inheritdoc />
        public int GetResumeStage(string posterId)
        {
            return IsInProgress(posterId) ? Data.FindPoster(posterId).stageIndex : -1;
        }

        /// <inheritdoc />
        public bool IsUnlocked(string posterId, IReadOnlyList<string> orderedIds)
        {
            if (string.IsNullOrEmpty(posterId) || orderedIds == null)
            {
                return false;
            }

            var index = -1;

            for (var i = 0; i < orderedIds.Count; i++)
            {
                if (string.Equals(orderedIds[i], posterId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Warn($"IsUnlocked: '{posterId}' is not in the poster catalogue. Treating it as locked.");
                return false;
            }

            return index == 0 || IsCompleted(orderedIds[index - 1]);
        }

        /// <inheritdoc />
        public int BeginOrResume(string posterId)
        {
            if (string.IsNullOrEmpty(posterId))
            {
                Warn("BeginOrResume called with an empty posterId. Nothing started.");
                return -1;
            }

            var data = Data;
            var entry = GetOrCreate(data, posterId);

            // Fresh, replay (completed) or a started-but-no-stage entry all begin
            // at stage 0. An in-progress entry keeps its stage.
            if (entry.completed || entry.stageIndex < 0)
            {
                entry.stageIndex = 0;
            }

            data.activePosterId = posterId;
            _save?.Save();
            Changed?.Invoke(posterId);
            return entry.stageIndex;
        }

        /// <inheritdoc />
        public void SetStage(string posterId, int stageIndex, bool immediate)
        {
            if (string.IsNullOrEmpty(posterId))
            {
                Warn("SetStage called with an empty posterId. Ignored.");
                return;
            }

            if (stageIndex < 0)
            {
                Warn($"SetStage('{posterId}', {stageIndex}): negative stage. Ignored.");
                return;
            }

            var data = Data;
            var entry = GetOrCreate(data, posterId);
            var changed = entry.stageIndex != stageIndex || data.activePosterId != posterId;

            entry.stageIndex = stageIndex;
            data.activePosterId = posterId;

            if (immediate)
            {
                _save?.Save();
            }
            else
            {
                _save?.SaveSoon();
            }

            if (changed)
            {
                Changed?.Invoke(posterId);
            }
        }

        /// <inheritdoc />
        public bool MarkCompleted(string posterId)
        {
            if (string.IsNullOrEmpty(posterId))
            {
                Warn("MarkCompleted called with an empty posterId. Ignored.");
                return false;
            }

            var data = Data;
            var entry = GetOrCreate(data, posterId);
            var firstTime = !entry.completed;

            entry.completed = true;
            entry.stageIndex = -1;

            if (string.Equals(data.activePosterId, posterId, StringComparison.Ordinal))
            {
                data.activePosterId = string.Empty;
            }

            _save?.Save();
            Changed?.Invoke(posterId);
            return firstTime;
        }

        /// <inheritdoc />
        public bool IsRewardPending(string posterId)
        {
            var entry = Data.FindPoster(posterId);
            return entry != null && entry.completed && !entry.rewardClaimed;
        }

        /// <inheritdoc />
        public bool TryMarkRewardClaimed(string posterId)
        {
            var entry = Data.FindPoster(posterId);

            if (entry == null || !entry.completed)
            {
                Warn($"TryMarkRewardClaimed('{posterId}'): poster is not completed. Nothing claimed.");
                return false;
            }

            if (entry.rewardClaimed)
            {
                return false;
            }

            entry.rewardClaimed = true;

            // SaveSoon on purpose: the caller's IWallet.Add does the synchronous
            // write, committing flag and coins together (see KEY DECISIONS).
            _save?.SaveSoon();
            Changed?.Invoke(posterId);
            return true;
        }

        /// <inheritdoc />
        public bool TryMarkRewardDoubled(string posterId)
        {
            var entry = Data.FindPoster(posterId);

            if (entry == null || !entry.completed || entry.rewardDoubled)
            {
                return false;
            }

            entry.rewardDoubled = true;

            // Same single-write reasoning as TryMarkRewardClaimed.
            _save?.SaveSoon();
            Changed?.Invoke(posterId);
            return true;
        }

        /// <inheritdoc />
        public bool IsRewardDoubled(string posterId)
        {
            var entry = Data.FindPoster(posterId);
            return entry != null && entry.rewardDoubled;
        }

        // ---- Helpers ------------------------------------------------------------

        private static PosterProgressEntry GetOrCreate(SaveData data, string posterId)
        {
            var entry = data.FindPoster(posterId);

            if (entry != null)
            {
                return entry;
            }

            entry = new PosterProgressEntry { posterId = posterId, stageIndex = -1 };
            data.posters.Add(entry);
            return entry;
        }

        private void Warn(string message)
        {
            _warn?.Invoke("[PosterProgressService] " + message);
        }
    }
}
