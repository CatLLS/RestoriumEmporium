// ============================================================
// ISaveService — the single entry point for durable state.
// WHAT & WHY: §4 of the build spec makes persistence mandatory and constant.
//   Systems mutate Data and then call Save(); nothing else touches the disk.
// KEY DECISIONS:
//   - Exposes the SaveData object directly rather than a get/set API per field.
//     With one flat POCO, an accessor per field would be pure ceremony, and
//     callers would still need Save() afterwards.
//   - SaveSoon() exists alongside Save(): the drag loop can report progress
//     every frame without doing file I/O on the hot path (§1.5, mobile hygiene).
//     Save() stays available for the moments that must not be deferred.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by SaveManager on the Systems prefab.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Core
{
    public interface ISaveService
    {
        /// <summary>The live save state. Mutate, then call Save() or SaveSoon().</summary>
        SaveData Data { get; }

        /// <summary>Writes to disk immediately. Use on meaningful state changes.</summary>
        void Save();

        /// <summary>
        /// Marks the state dirty; the actual write is coalesced to the end of the
        /// frame. Safe to call every frame from an input loop.
        /// </summary>
        void SaveSoon();

        /// <summary>Re-reads from disk, replacing Data.</summary>
        void Load();

        /// <summary>Wipes progress back to a fresh game and saves.</summary>
        void ResetProgress();

        /// <summary>Raised after Data has been replaced by a Load or a reset.</summary>
        event Action Reloaded;
    }
}
