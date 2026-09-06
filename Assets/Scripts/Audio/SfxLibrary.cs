// ============================================================
// SfxLibrary — one asset mapping every SfxId to a clip, volume and pitch jitter.
// WHAT & WHY: Callers ask for a logical sound (SfxId.ButtonClick), never for an
//   AudioClip. This asset is the only place the mapping lives, so the human can
//   drop the real .mp3 files in later without touching a prefab or a line of code.
// KEY DECISIONS:
//   - An unmapped id, or a mapped id whose clip is still empty, resolves to
//     "silent" rather than logging an error. The SFX are still being authored;
//     a half-filled library must never spam the console or stop a playtest.
//   - Serialised List plus a Dictionary built once on first use. Unity cannot
//     serialise a Dictionary, and a linear scan on every footstep-frequency
//     sound would be a needless per-call cost on a phone.
//   - Pitch is authored as a min/max pair, not a single "+/- jitter" number.
//     Some sounds want an asymmetric range (a spray that may go higher but never
//     lower), and a pair expresses that without a second field.
//   - Volume is per entry rather than per call. Levelling a library of clips
//     recorded at different loudnesses is authoring work, not caller work.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] In the Project window, right-click Assets/Audio -> Create -> Folder,
//     name it "Data" (final path: Assets/Audio/Data).
// [x] Right-click that folder -> Create -> Restorium -> Sfx Library.
//     Name the asset exactly "SfxLibrary".
// [x] Select the asset. In the Inspector, set Entries -> Size to 13.
// [x] Fill the entries as follows. For each one set Id from the dropdown,
//     drag the clip from Assets/Audio into Clip, leave Volume at 1 and set
//     Pitch Min / Pitch Max to 0.95 / 1.05 (use 1 / 1 for music-like cues):
//       Element 0  Id = ButtonClick          Clip = (leave empty until authored)
//       Element 1  Id = PageFlip             Clip = freesound_community-page-flip-47177
//       Element 2  Id = PosterFlip           Clip = freesound_community-page-flip-47177
//       Element 3  Id = StageComplete        Clip = (leave empty until authored)
//       Element 4  Id = RestorationComplete  Clip = freesound_community-cottagecore-17463
//       Element 5  Id = ToolSelect           Clip = (leave empty until authored)
//       Element 6  Id = ToolDustRemover      Clip = (dustRemover)freesound_community-sweeping-44962
//       Element 7  Id = ToolWaterSpray       Clip = (waterSpray)dragon-studio-water-dripping-364450
//       Element 8  Id = ToolDeacidifier      Clip = (deacidifier)yuliana-yurukova-spray-bottle-333144
//       Element 9  Id = ToolSqueegee         Clip = (squeegeesfx)freesound_community-water-splash-46402
//       Element 10 Id = ToolRoller           Clip = (roller)freesound_community-organic-blurpy-sticky-sound-in-kitchen-01-43763
//       Element 11 Id = ToolPencil           Clip = (pencil)freesound_community-pencil-29272
//       Element 12 Id = None                 Clip = (leave empty; harmless placeholder)
// [x] IMPORTANT: select the six tool clips (Elements 6-11) in the Project
//     window together and, in the Inspector, tick "Loop" is NOT needed here —
//     instead set Load Type = "Decompress On Load" and Preload Audio Data = on.
//     They are looped by the AudioManager, not by the clip.
// [x] Drag this SfxLibrary asset into the "Sfx Library" field of the
//     AudioManager component (see AudioManager.cs setup).
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Audio
{
    using RestoriumEmporium.Core;

    [CreateAssetMenu(menuName = "Restorium/Sfx Library", fileName = "SfxLibrary")]
    public class SfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Logical sound this row defines.")]
            public SfxId id;

            [Tooltip("Clip to play. Leave empty while the sound is unauthored — " +
                     "the id then resolves to silence instead of an error.")]
            public AudioClip clip;

            [Range(0f, 1f)]
            [Tooltip("Per-clip level, so a loud recording can be tamed here " +
                     "rather than in every caller.")]
            public float volume;

            [Range(0.1f, 3f)]
            [Tooltip("Lower bound of the random pitch. Set both to 1 for no jitter.")]
            public float pitchMin;

            [Range(0.1f, 3f)]
            [Tooltip("Upper bound of the random pitch. Set both to 1 for no jitter.")]
            public float pitchMax;
        }

        [Tooltip("One row per logical sound. Rows may be added in any order; " +
                 "the first row for an id wins.")]
        public List<Entry> entries = new List<Entry>();

        private Dictionary<SfxId, Entry> _index;

        /// <summary>
        /// Resolves a logical sound. Returns false — silently — when the id is
        /// unmapped or its clip has not been authored yet.
        /// </summary>
        public bool TryResolve(SfxId id, out AudioClip clip, out float volume, out float pitch)
        {
            clip = null;
            volume = 1f;
            pitch = 1f;

            if (id == SfxId.None)
            {
                return false;
            }

            BuildIndexIfNeeded();

            if (!_index.TryGetValue(id, out var entry) || entry.clip == null)
            {
                return false;
            }

            clip = entry.clip;

            // A freshly added row has volume 0 because structs default to zero;
            // treating 0 as "unset" beats forcing the human to type 1 on every row.
            volume = entry.volume <= 0f ? 1f : Mathf.Clamp01(entry.volume);

            float min = entry.pitchMin <= 0f ? 1f : entry.pitchMin;
            float max = entry.pitchMax <= 0f ? min : entry.pitchMax;
            pitch = max > min ? UnityEngine.Random.Range(min, max) : min;

            return true;
        }

        /// <summary>True when the id has an authored, non-empty clip.</summary>
        public bool Has(SfxId id)
        {
            return TryResolve(id, out _, out _, out _);
        }

        /// <summary>Drops the cached index. Called by the Inspector after an edit.</summary>
        public void Invalidate()
        {
            _index = null;
        }

        private void OnValidate()
        {
            _index = null;
        }

        private void OnDisable()
        {
            // Domain reload / asset unload: never keep a stale index alive.
            _index = null;
        }

        private void BuildIndexIfNeeded()
        {
            if (_index != null)
            {
                return;
            }

            _index = new Dictionary<SfxId, Entry>(entries != null ? entries.Count : 0);

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry.id == SfxId.None || _index.ContainsKey(entry.id))
                {
                    continue;
                }

                _index.Add(entry.id, entry);
            }
        }
    }
}
