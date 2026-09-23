// ============================================================
// IAudioService — one-shot SFX, a looping tool channel, and music.
// WHAT & WHY: The restoration tools need a sound that starts when the finger
//   goes down and stops when it lifts, which a PlayOneShot API cannot express.
//   This adds a dedicated loop channel alongside the usual one-shots.
// KEY DECISIONS:
//   - A single loop channel, not one per tool. Only one tool can be dragging at
//     a time, so a second channel would only ever be a bug (two tool sounds
//     overlapping). StartToolLoop on an already-running channel swaps the clip.
//   - Takes SfxId rather than AudioClip so callers never hold clip references
//     and the SFX can be authored later without touching any prefab.
//   - Volumes are 0..1 linear; the implementation converts to the mixer's dB.
//     Callers should never have to know about logarithms.
//   - Batch 2 adds SetMusicPaused(bool) so a cutscene with its own soundtrack
//     can hold the music and give it back at the same position afterwards.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by AudioManager on the Systems prefab.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Audio
{
    using RestoriumEmporium.Core;

    public interface IAudioService
    {
        /// <summary>Fire-and-forget one-shot. Silent (not an error) if unmapped.</summary>
        void PlaySfx(SfxId id);

        /// <summary>Starts or swaps the looping tool channel.</summary>
        void StartToolLoop(SfxId id);

        /// <summary>Stops the looping tool channel.</summary>
        void StopToolLoop();

        void PlayMusic(AudioClip clip, bool loop = true);
        void StopMusic();

        /// <summary>
        /// Pauses (true) or resumes (false) the music without losing its position.
        /// Used by the CutscenePlayer while a video with its own soundtrack plays.
        /// </summary>
        void SetMusicPaused(bool paused);

        /// <summary>0..1 linear. Persisted by SaveManager.</summary>
        void SetMusicVolume(float linear01);

        /// <summary>0..1 linear. Persisted by SaveManager.</summary>
        void SetSfxVolume(float linear01);
    }
}
