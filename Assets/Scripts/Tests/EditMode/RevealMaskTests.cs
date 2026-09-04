// ============================================================
// RevealMaskTests — behavioural tests for the core scrubbing mechanic.
// WHAT & WHY: RevealMask is the one class every restoration stage runs through:
//   dust, wash, deacidify, squeegee, adhesive and mend are all "paint here,
//   tell me how much is covered". It is deliberately pure C# with no
//   MonoBehaviour and no scene, so all of that logic can be verified here
//   instead of by dragging a finger across a phone.
// KEY DECISIONS:
//   - Tests assert on observable behaviour (coverage, cell values, footprint
//     shape), never on private fields. The incremental coverage counter is an
//     optimisation; if it were replaced with a full scan tomorrow these tests
//     should all still pass unchanged.
//   - The StampLine test compares against two discrete Stamps rather than
//     checking an absolute number. The exact coverage depends on the falloff
//     curve and the step size, both of which are tuning values — but "a swipe
//     fills the gap between its endpoints" is the actual requirement, and that
//     is a relationship, not a constant.
//   - Nothing here touches RevealMask.Texture. Allocating a Texture2D needs a
//     graphics device, which would make these tests slower and would make them
//     fail on a headless build agent for a reason that has nothing to do with
//     the mask logic. HasTexture is asserted instead, to prove the allocation
//     really is lazy.
//   - The aspect test uses a SQUARE grid stretched onto a TALL surface. When the
//     grid already matches the surface proportions the correction is a no-op and
//     a passing test would prove nothing; deliberately mismatching them is the
//     only way to observe that the correction happens at all.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Open Window -> General -> Test Runner.
// [ ] Select the "EditMode" tab at the top of that window.
// [ ] Click "Run All". Everything should turn green.
// [ ] If the window is empty, the test assembly has not compiled yet: wait for
//     Unity to finish importing, then reopen the Test Runner.
// ---------------------------------------------------------------

using NUnit.Framework;
using RestoriumEmporium.Restoration;
using UnityEngine;

namespace RestoriumEmporium.Tests
{
    public class RevealMaskTests
    {
        private const float Centre = 0.5f;

        private static RevealMask NewMask(int width = 64, int height = 64)
        {
            return new RevealMask(width, height);
        }

        /// <summary>Counts cells with any ink along the horizontal centre line.</summary>
        private static int InkedCellsAcrossRow(RevealMask mask, int row)
        {
            var count = 0;

            for (var x = 0; x < mask.Width; x++)
            {
                if (mask.GetCell(x, row) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Counts cells with any ink along the vertical centre line.</summary>
        private static int InkedCellsDownColumn(RevealMask mask, int column)
        {
            var count = 0;

            for (var y = 0; y < mask.Height; y++)
            {
                if (mask.GetCell(column, y) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        // ---------------------------------------------------------------
        // Coverage basics
        // ---------------------------------------------------------------

        [Test]
        public void Coverage_IsZero_OnFreshMask()
        {
            var mask = NewMask();

            Assert.That(mask.Coverage, Is.EqualTo(0f));
            Assert.That(mask.CoveredCells, Is.EqualTo(0));
        }

        [Test]
        public void Coverage_IsOne_AfterFill()
        {
            var mask = NewMask();

            mask.Fill(byte.MaxValue);

            Assert.That(mask.Coverage, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(mask.CoveredCells, Is.EqualTo(mask.CellCount));
        }

        [Test]
        public void Coverage_IsZero_AfterClear()
        {
            var mask = NewMask();
            mask.Fill(byte.MaxValue);

            mask.Clear();

            Assert.That(mask.Coverage, Is.EqualTo(0f));
        }

        [Test]
        public void Fill_BelowThreshold_DoesNotCountAsCovered()
        {
            var mask = new RevealMask(32, 32, 200);

            mask.Fill(100);

            Assert.That(mask.Coverage, Is.EqualTo(0f),
                "A cell below the threshold is inked but not yet 'revealed'.");
        }

        [Test]
        public void Stamp_AtCentre_RevealsSomeButNotAll()
        {
            var mask = NewMask();

            mask.Stamp(new Vector2(Centre, Centre), 0.15f, 1f);

            Assert.That(mask.Coverage, Is.GreaterThan(0f));
            Assert.That(mask.Coverage, Is.LessThan(1f));
        }

        [Test]
        public void Coverage_NeverDecreases_UnderRepeatedStamping()
        {
            var mask = NewMask();
            var previous = 0f;

            for (var i = 0; i < 12; i++)
            {
                var t = i / 11f;
                mask.Stamp(new Vector2(t, t), 0.1f, 1f);

                Assert.That(mask.Coverage, Is.GreaterThanOrEqualTo(previous),
                    $"Coverage fell at stamp {i}. Painting must never un-reveal.");
                previous = mask.Coverage;
            }
        }

        // ---------------------------------------------------------------
        // The anti-gap behaviour: the single most important feel requirement
        // ---------------------------------------------------------------

        [Test]
        public void StampLine_CoversMoreThanTwoDiscreteStampsAtTheSameEndpoints()
        {
            var from = new Vector2(0.2f, Centre);
            var to = new Vector2(0.8f, Centre);
            const float radius = 0.05f;

            var swiped = NewMask();
            swiped.StampLine(from, to, radius, 1f);

            var tapped = NewMask();
            tapped.Stamp(from, radius, 1f);
            tapped.Stamp(to, radius, 1f);

            Assert.That(swiped.Coverage, Is.GreaterThan(tapped.Coverage * 1.5f),
                "A fast drag must leave a continuous band, not two dots. " +
                "If this fails, interpolation in StampLine has regressed and " +
                "quick swipes will leave visible gaps on the poster.");
        }

        [Test]
        public void StampLine_FillsTheMiddleOfTheStroke()
        {
            var mask = NewMask();

            mask.StampLine(new Vector2(0.1f, Centre), new Vector2(0.9f, Centre), 0.05f, 1f);

            var midpoint = mask.SampleUv(new Vector2(Centre, Centre));

            Assert.That(midpoint, Is.GreaterThan(0),
                "The middle of a stroke must be painted, not skipped.");
        }

        [Test]
        public void StampLine_WithIdenticalEndpoints_BehavesLikeASingleStamp()
        {
            var point = new Vector2(Centre, Centre);

            var line = NewMask();
            line.StampLine(point, point, 0.1f, 1f);

            var single = NewMask();
            single.Stamp(point, 0.1f, 1f);

            Assert.That(line.Coverage, Is.EqualTo(single.Coverage).Within(0.0001f));
        }

        // ---------------------------------------------------------------
        // Brush shape
        // ---------------------------------------------------------------

        [Test]
        public void Stamp_CorrectsForSurfaceAspect_SoTheBrushIsRoundOnScreen()
        {
            // A square grid stretched onto a surface twice as tall as it is wide.
            // A brush that is round on screen must therefore be half as tall as
            // it is wide *in grid space*.
            var mask = NewMask(80, 80);
            mask.SurfaceAspect = 0.5f;

            mask.Stamp(new Vector2(Centre, Centre), 0.2f, 1f);

            var acrossX = InkedCellsAcrossRow(mask, mask.Height / 2);
            var downY = InkedCellsDownColumn(mask, mask.Width / 2);

            Assert.That(acrossX, Is.GreaterThan(0));
            Assert.That(downY, Is.GreaterThan(0));
            Assert.That(acrossX / (float)downY, Is.EqualTo(2f).Within(0.35f),
                "With a 0.5 surface aspect the grid footprint should be about " +
                "twice as wide as it is tall, which is what makes it circular " +
                "on the actual poster.");
        }

        [Test]
        public void Stamp_IsSymmetric_WhenGridMatchesSurface()
        {
            var mask = NewMask(64, 64);
            mask.SurfaceAspect = 1f;

            mask.Stamp(new Vector2(Centre, Centre), 0.2f, 1f);

            var acrossX = InkedCellsAcrossRow(mask, mask.Height / 2);
            var downY = InkedCellsDownColumn(mask, mask.Width / 2);

            Assert.That(acrossX, Is.EqualTo(downY).Within(1));
        }

        [Test]
        public void SurfaceAspect_IgnoresNonPositiveAndNonFiniteValues()
        {
            var mask = NewMask();
            var original = mask.SurfaceAspect;

            mask.SurfaceAspect = 0f;
            mask.SurfaceAspect = -3f;
            mask.SurfaceAspect = float.NaN;
            mask.SurfaceAspect = float.PositiveInfinity;

            Assert.That(mask.SurfaceAspect, Is.EqualTo(original),
                "A degenerate aspect would make the brush radius zero or NaN.");
        }

        // ---------------------------------------------------------------
        // Robustness: this class must never throw, whatever input arrives
        // ---------------------------------------------------------------

        [Test]
        public void Stamp_IgnoresNonFiniteInput_WithoutThrowing()
        {
            var mask = NewMask();

            Assert.DoesNotThrow(() =>
            {
                mask.Stamp(new Vector2(float.NaN, Centre), 0.1f, 1f);
                mask.Stamp(new Vector2(Centre, float.PositiveInfinity), 0.1f, 1f);
                mask.Stamp(new Vector2(Centre, Centre), float.NaN, 1f);
                mask.Stamp(new Vector2(Centre, Centre), 0.1f, float.NaN);
            });

            Assert.That(mask.Coverage, Is.EqualTo(0f),
                "Garbage input must be dropped, not partially applied.");
        }

        [Test]
        public void Stamp_IgnoresNonPositiveRadiusOrStrength()
        {
            var mask = NewMask();

            mask.Stamp(new Vector2(Centre, Centre), 0f, 1f);
            mask.Stamp(new Vector2(Centre, Centre), -0.2f, 1f);
            mask.Stamp(new Vector2(Centre, Centre), 0.2f, 0f);
            mask.Stamp(new Vector2(Centre, Centre), 0.2f, -1f);

            Assert.That(mask.Coverage, Is.EqualTo(0f));
        }

        [Test]
        public void Stamp_FarOutsideTheSurface_LeavesTheMaskUntouched()
        {
            var mask = NewMask();

            mask.Stamp(new Vector2(-5f, -5f), 0.1f, 1f);
            mask.Stamp(new Vector2(9f, 9f), 0.1f, 1f);

            Assert.That(mask.Coverage, Is.EqualTo(0f));
        }

        [Test]
        public void Stamp_StraddlingTheEdge_PaintsOnlyTheOverlappingPart()
        {
            var mask = NewMask();

            // Centre sits exactly on the left edge, so about half the brush lands.
            mask.Stamp(new Vector2(0f, Centre), 0.2f, 1f);

            Assert.That(mask.Coverage, Is.GreaterThan(0f),
                "Dragging past the poster edge should still clean the part " +
                "that is on the poster.");
            Assert.That(mask.GetCell(mask.Width - 1, mask.Height / 2), Is.EqualTo(0),
                "...but must not wrap around to the opposite edge.");
        }

        [Test]
        public void Stamp_SaturatesAtMaximum_WhenAppliedRepeatedly()
        {
            var mask = NewMask();
            var point = new Vector2(Centre, Centre);

            for (var i = 0; i < 50; i++)
            {
                mask.Stamp(point, 0.15f, 1f);
            }

            Assert.That(mask.SampleUv(point), Is.EqualTo(byte.MaxValue),
                "Accumulation must clamp, not wrap around to zero.");
        }

        [Test]
        public void GetCell_OutOfRange_ReturnsZeroWithoutThrowing()
        {
            var mask = NewMask();
            mask.Fill(byte.MaxValue);

            Assert.DoesNotThrow(() =>
            {
                Assert.That(mask.GetCell(-1, 0), Is.EqualTo(0));
                Assert.That(mask.GetCell(0, -1), Is.EqualTo(0));
                Assert.That(mask.GetCell(mask.Width, 0), Is.EqualTo(0));
                Assert.That(mask.GetCell(0, mask.Height), Is.EqualTo(0));
            });
        }

        // ---------------------------------------------------------------
        // Construction and bookkeeping
        // ---------------------------------------------------------------

        [Test]
        public void Constructor_ClampsDegenerateDimensionsAndThreshold()
        {
            var mask = new RevealMask(0, -4, 0);

            Assert.That(mask.Width, Is.GreaterThanOrEqualTo(1));
            Assert.That(mask.Height, Is.GreaterThanOrEqualTo(1));
            Assert.That(mask.CoverageThreshold, Is.GreaterThanOrEqualTo((byte)1),
                "A zero threshold would make an untouched mask read as complete, " +
                "and every stage would finish instantly.");
            Assert.That(mask.Coverage, Is.EqualTo(0f));
        }

        [Test]
        public void CellCount_MatchesWidthTimesHeight()
        {
            var mask = NewMask(37, 91);

            Assert.That(mask.CellCount, Is.EqualTo(37 * 91));
        }

        [Test]
        public void IsDirty_IsFalseInitially_AndSetByPainting()
        {
            var mask = NewMask();

            Assert.That(mask.IsDirty, Is.False);

            mask.Stamp(new Vector2(Centre, Centre), 0.1f, 1f);

            Assert.That(mask.IsDirty, Is.True,
                "A clean flag after painting would leave the GPU copy stale.");
        }

        [Test]
        public void HasTexture_IsFalse_UntilTheTextureIsActuallyNeeded()
        {
            var mask = NewMask();

            mask.Stamp(new Vector2(Centre, Centre), 0.1f, 1f);

            Assert.That(mask.HasTexture, Is.False,
                "The Texture2D must be allocated lazily; a mask used only for " +
                "coverage maths should never touch the graphics device.");
        }
    }
}
