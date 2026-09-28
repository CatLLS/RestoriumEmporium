// ============================================================
// StickerPeelTracker — which stickers are still on the poster, as pure C#.
// WHAT & WHY: The sticker screen needs three answers on every tap: "is this one
//   still peelable?", "how many are left?" (for GameSignals.StickerPeeled and the
//   tutorial) and "which one should the tutorial hand point at next?". That is
//   bookkeeping, not Unity work, so it lives here and is unit tested
//   (Tests/EditMode/Logic/StickerPeelTrackerTests.cs).
// KEY DECISIONS:
//   - TryPeel returns true exactly once per sticker. A double tap, or a tap on a
//     sticker that is already falling, is a normal thing a finger does and must
//     not count twice or the stage would complete with a sticker still on.
//   - "Just completed" is reported by TryPeel's out parameter on the single tap
//     that removes the last sticker, mirroring RestorationStageRunner's
//     ReportCoverage, so completion is requested exactly once.
//   - Reset(count) is the whole resume story: re-entering the stage (after a
//     relaunch or coming back from the journal) simply starts with every sticker
//     back on. Individual peels are not saved; the stage is short enough that
//     persisting them would be complexity with no player benefit.
//   - FirstRemaining is by authored order, so the tutorial walks the player
//     through the stickers in the order the designer listed them.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# class; StickerRemovalScreen owns one instance.
// ---------------------------------------------------------------

namespace RestoriumEmporium.Restoration
{
    public sealed class StickerPeelTracker
    {
        private bool[] _peeled = new bool[0];
        private int _remaining;

        /// <summary>How many stickers the stage started with.</summary>
        public int Count => _peeled.Length;

        /// <summary>Stickers still on the poster.</summary>
        public int Remaining => _remaining;

        /// <summary>True when no sticker is left (also true for a stage authored with none).</summary>
        public bool IsComplete => _remaining == 0;

        /// <summary>Puts every sticker back on. Negative counts are treated as zero.</summary>
        public void Reset(int count)
        {
            int n = count > 0 ? count : 0;

            if (_peeled.Length != n)
            {
                _peeled = new bool[n];
            }
            else
            {
                for (int i = 0; i < n; i++)
                {
                    _peeled[i] = false;
                }
            }

            _remaining = n;
        }

        /// <summary>True when index is in range and that sticker has not been peeled yet.</summary>
        public bool IsRemaining(int index)
        {
            return index >= 0 && index < _peeled.Length && !_peeled[index];
        }

        /// <summary>
        /// Marks a sticker as peeled. Returns false (and changes nothing) for an
        /// out-of-range index or a sticker that is already off.
        /// <paramref name="completedNow"/> is true only on the tap that removed the last one.
        /// </summary>
        public bool TryPeel(int index, out bool completedNow)
        {
            completedNow = false;

            if (!IsRemaining(index))
            {
                return false;
            }

            _peeled[index] = true;
            _remaining--;
            completedNow = _remaining == 0;
            return true;
        }

        /// <summary>The lowest-index sticker still on the poster, or -1 when none remain.</summary>
        public int FirstRemaining()
        {
            for (int i = 0; i < _peeled.Length; i++)
            {
                if (!_peeled[i])
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
