// ============================================================
// RestorationStageRunner — the per-stage bookkeeping, as pure C#.
// WHAT & WHY: "Which stage are we on, how far through it are we, and did this
//   coverage reading just finish it?" is arithmetic and index handling, not
//   Unity work. Pulling it out of RestorationController keeps that component a
//   thin piece of scene glue (events, components, audio) and makes the rules
//   themselves testable without a GameObject.
// KEY DECISIONS:
//   - ReportCoverage returns true exactly ONCE, on the reading that crosses the
//     stage's requiredCoverage. Coverage keeps rising after that (FillCompletely
//     pushes it to 1), and a completion check that fired on every subsequent
//     reading would advance the poster several stages in one frame.
//   - Progress01 is normalised against the stage's own requiredCoverage and
//     clamped, so a progress bar reads exactly 100% when the stage ends whether
//     the threshold is 0.85 or the pencil's 0.6. That is the contract
//     IRestorationRuntime.StageProgress01 promises.
//   - Advance() moves the index but does not decide what to show. The choreo-
//     graphy (flip, screen change) is driven by the transition event on the
//     controller; this class knows nothing about it.
//   - Every accessor tolerates a null poster and an out-of-range index, because
//     a save file can name a stage index that a re-authored poster no longer
//     has. That resolves to "no stage" rather than to an exception.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# class; RestorationController owns one instance.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Data;

    /// <summary>Tracks which stage is running and how far through it the player is.</summary>
    public sealed class RestorationStageRunner
    {
        private PosterData _poster;
        private int _index = -1;
        private float _progress;
        private bool _completed;

        /// <summary>The poster being restored, or null.</summary>
        public PosterData Poster => _poster;

        /// <summary>Index into Poster.stages, or -1 when nothing is running.</summary>
        public int Index => _index;

        /// <summary>The active stage asset, or null.</summary>
        public RestorationStageData Stage => _poster != null ? _poster.GetStage(_index) : null;

        /// <summary>Progress through the current stage, 0..1, normalised to its threshold.</summary>
        public float Progress01 => _progress;

        /// <summary>True once this stage has reached its required coverage.</summary>
        public bool IsStageComplete => _completed;

        /// <summary>True when a stage is loaded and has not completed yet.</summary>
        public bool IsRunning => Stage != null && !_completed;

        /// <summary>True when the current stage is the last one on the poster.</summary>
        public bool IsFinalStage => _poster != null && _index >= 0 && _index == _poster.StageCount - 1;

        /// <summary>
        /// Starts a poster at <paramref name="startStageIndex"/>. An index outside
        /// the poster's range is clamped to a valid one; a poster with no stages
        /// leaves the runner idle.
        /// </summary>
        public void Begin(PosterData poster, int startStageIndex)
        {
            _poster = poster;

            if (poster == null || poster.StageCount == 0)
            {
                _index = -1;
                _progress = 0f;
                _completed = false;
                return;
            }

            _index = Mathf.Clamp(startStageIndex, 0, poster.StageCount - 1);
            _progress = 0f;
            _completed = false;
        }

        /// <summary>Clears the runner back to "nothing running".</summary>
        public void Clear()
        {
            _poster = null;
            _index = -1;
            _progress = 0f;
            _completed = false;
        }

        /// <summary>
        /// Feeds a coverage reading in. Returns true on the single reading that
        /// takes the stage over its requiredCoverage; false every other time,
        /// including every reading after completion.
        /// </summary>
        public bool ReportCoverage(float coverage)
        {
            RestorationStageData stage = Stage;
            if (stage == null || _completed)
            {
                return false;
            }

            if (float.IsNaN(coverage))
            {
                return false;
            }

            float required = stage.requiredCoverage > 0f ? stage.requiredCoverage : 1f;
            _progress = Mathf.Clamp01(coverage / required);

            if (coverage + Mathf.Epsilon < required)
            {
                return false;
            }

            _progress = 1f;
            _completed = true;
            return true;
        }

        /// <summary>
        /// Moves to the next stage. Returns false when the completed stage was the
        /// last one, in which case the index is left pointing at it.
        /// </summary>
        public bool Advance()
        {
            if (_poster == null || _index < 0)
            {
                return false;
            }

            if (_index >= _poster.StageCount - 1)
            {
                return false;
            }

            _index++;
            _progress = 0f;
            _completed = false;
            return true;
        }

        /// <summary>Re-arms the current stage without moving the index.</summary>
        public void RestartStage()
        {
            _progress = 0f;
            _completed = false;
        }
    }
}
