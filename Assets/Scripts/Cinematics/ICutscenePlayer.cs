// ============================================================
// ICutscenePlayer — plays a full-screen video over the Game scene.
// WHAT & WHY: Batch 2 adds three videos: Tracy's intro (tracysc1, first launch),
//   Tracy's reaction after poster 1 (tracysc2, before Finished Repair) and the
//   book opening (openBookTransition, desk hub -> journal). Flow code only needs
//   "play this, tell me when it is over", so that is the whole contract.
// KEY DECISIONS:
//   - Callback-based, not a coroutine the caller must host, so a plain C# flow
//     method can chain "video -> screen" without being a MonoBehaviour itself.
//   - onFinished ALWAYS fires exactly once: on the clip's end, on a skip, and on
//     a playback error (a broken video must never soft-lock the game).
//   - Registered in ServiceLocator by the scene's CutscenePlayer while it is
//     alive, because the tutorial must not start talking over a video
//     (it checks IsPlaying and waits).
//   - Seen-state is NOT tracked here. Callers record CutsceneIds in
//     SaveData.seenCutscenes after onFinished, because only the flow knows which
//     videos are one-shot (the book transition plays every time).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing here. See CutscenePlayer.cs for the scene object.
// ---------------------------------------------------------------

using System;
using UnityEngine.Video;

namespace RestoriumEmporium.Cinematics
{
    public interface ICutscenePlayer
    {
        bool IsPlaying { get; }

        /// <summary>Raised when any cutscene starts / ends (tutorial and music listen).</summary>
        event Action<bool> PlayingChanged;

        /// <summary>
        /// Plays <paramref name="clip"/> full screen. <paramref name="skippable"/> lets
        /// a tap end it early. <paramref name="onFinished"/> fires exactly once.
        /// A null clip calls onFinished immediately.
        /// </summary>
        void Play(VideoClip clip, bool skippable, Action onFinished);
    }

    /// <summary>Stable ids written into SaveData.seenCutscenes. Never rename once shipped.</summary>
    public static class CutsceneIds
    {
        public const string Intro = "intro";

        /// <summary>Per-poster completion cutscene id, e.g. "complete:poster01".</summary>
        public static string PosterComplete(string posterId) => "complete:" + posterId;
    }
}
