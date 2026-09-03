// ============================================================
// IRevealSurface — the paintable poster surface, as seen by input and by stages.
// WHAT & WHY: All six tools do the same thing: drag across the poster, reveal
//   the next layer underneath the stroke, and report how much has been covered.
//   This interface is that operation, so RevealMaskPainter (input) and
//   RestorationStageRunner (rules) both work against one abstraction and can be
//   tested against a fake.
// KEY DECISIONS:
//   - Stroke-shaped API (Begin/Continue/End) rather than a single Paint(point).
//     The implementation interpolates between Begin and Continue, so a fast
//     flick across the screen leaves a continuous band instead of two dots —
//     the single most noticeable difference between a cheap and a good scrub
//     mechanic.
//   - Coordinates are normalised UV (0..1 across the poster rect), not pixels
//     or screen space. The mask resolution, the poster's on-screen size and the
//     device resolution can then all change independently.
//   - Coverage is a property AND an event. Polling suits the stage runner;
//     the event suits UI that wants a progress bar without an Update loop.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Implemented by PosterLayerStack on the poster object.
// ---------------------------------------------------------------

using System;
using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    public interface IRevealSurface
    {
        /// <summary>Fraction of the surface revealed so far, 0..1.</summary>
        float Coverage { get; }

        /// <summary>Raised when Coverage changes, with the new value.</summary>
        event Action<float> CoverageChanged;

        /// <summary>Brush radius in UV units (0..1 relative to poster width).</summary>
        float BrushRadiusUv { get; set; }

        /// <summary>Begins a stroke at a normalised point inside the poster rect.</summary>
        void BeginStroke(Vector2 uv);

        /// <summary>Extends the current stroke, filling the gap since the last point.</summary>
        void ContinueStroke(Vector2 uv);

        /// <summary>Ends the current stroke.</summary>
        void EndStroke();

        /// <summary>Snaps to fully revealed. Used when a stage auto-completes.</summary>
        void FillCompletely();

        /// <summary>Clears the mask back to nothing revealed.</summary>
        void ResetMask();
    }
}
