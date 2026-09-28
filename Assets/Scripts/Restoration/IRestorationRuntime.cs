// ============================================================
// IRestorationRuntime — the running restoration, as seen by UI and the tutorial.
// WHAT & WHY: The tool bar, the stage header, the particle FX and every tutorial
//   step all need to know which stage is active and how far along it is. This is
//   the read-and-subscribe face of RestorationController, so none of them has to
//   depend on the controller's concrete type or reach into its internals.
// KEY DECISIONS:
//   - Events, not polling, for state changes; a single property for progress.
//     Progress changes every frame during a drag, so a listener that only wants
//     the final result subscribes to StageCompleted and never sees the churn.
//   - TrySelectTool returns bool rather than throwing or silently ignoring.
//     Tapping the wrong tool is a normal thing a player does — the tool bar uses
//     the false to play a soft rejection instead of treating it as an error.
//   - StageProgress01 is normalised against the stage's own requiredCoverage,
//     so a progress bar reads 100% exactly when the stage ends, whether the
//     threshold is 0.85 or 0.6.
//   - Exposes no setters for stage index. Advancing is the controller's job;
//     letting UI drive it is how a state machine ends up with two owners.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by RestorationController on the GameFlow object.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    public interface IRestorationRuntime
    {
        /// <summary>The poster currently being restored, or null before one starts.</summary>
        PosterData Poster { get; }

        /// <summary>Index into Poster.stages. -1 before the first stage begins.</summary>
        int CurrentStageIndex { get; }

        /// <summary>The active stage, or null when none is running.</summary>
        RestorationStageData CurrentStage { get; }

        /// <summary>The tool the player has picked up. None when nothing is selected.</summary>
        ToolId ActiveTool { get; }

        /// <summary>Progress through the current stage, 0..1, normalised to its threshold.</summary>
        float StageProgress01 { get; }

        event Action<RestorationStageData, int> StageStarted;
        event Action<RestorationStageData, int> StageCompleted;

        /// <summary>Raised while painting. Argument is StageProgress01.</summary>
        event Action<float> StageProgressChanged;

        /// <summary>Raised when the active tool changes, including to None.</summary>
        event Action<ToolId> ToolSelected;

        /// <summary>Raised once the final stage of the poster completes.</summary>
        event Action PosterCompleted;

        /// <summary>
        /// Picks up a tool. Returns false when the tool is not the one the current
        /// stage requires, leaving the previous selection untouched.
        /// </summary>
        bool TrySelectTool(ToolId tool);

        /// <summary>
        /// Starts (or resumes) a poster at <paramref name="startStageIndex"/>.
        /// Pass 0 for a fresh restoration.
        /// </summary>
        void BeginPoster(PosterData poster, int startStageIndex);

        /// <summary>
        /// Completes the current stage regardless of coverage or tool. Used by
        /// non-scrub stages (StageKind.StickerPeel) when their own interaction is
        /// done. No-op when no stage is running or it is already complete.
        /// </summary>
        void ForceCompleteCurrentStage();
    }
}
