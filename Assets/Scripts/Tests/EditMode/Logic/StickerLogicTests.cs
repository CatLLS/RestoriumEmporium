// ============================================================
// StickerLogicTests — NUnit tests for the sticker removal stage's pure logic.
// WHAT & WHY: The layout maths decides whether a sticker lands on the right spot
//   of the close-up on every phone shape, the tracker decides when the stage
//   completes, and the motion decides how long a peel takes. All three are plain
//   C#, so they are tested here without Unity (the checker runs them with
//   `dotnet test` via Tools/logictests; Unity's Test Runner runs them too).
// KEY DECISIONS:
//   - No UnityEngine types at all, per the Batch 2 contract.
//   - The Figma-derived default positions for poster 2 are pinned by a test, so a
//     change to DesignRectToNormalized cannot silently move the stickers.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Window -> General -> Test Runner -> EditMode -> Run All.
// ---------------------------------------------------------------

using NUnit.Framework;
using RestoriumEmporium.Restoration;

namespace RestoriumEmporium.Tests
{
    public class StickerLayoutTests
    {
        private const float Tol = 0.0005f;

        [Test]
        public void Fit_Contain_TallImageInWiderArea_MatchesHeight()
        {
            // 2304x4096 close-up (0.5625) into 412x600 (0.687): height-limited.
            StickerLayoutMath.Fit(412f, 600f, 2304f, 4096f, StickerFitMode.Contain, out float w, out float h);

            Assert.That(h, Is.EqualTo(600f).Within(Tol));
            Assert.That(w, Is.EqualTo(600f * 2304f / 4096f).Within(Tol));
        }

        [Test]
        public void Fit_Cover_TallImageInNarrowerArea_MatchesHeightAndOverflowsWidth()
        {
            // 412x917 phone (0.449) is narrower than the art (0.5625): cover matches height.
            StickerLayoutMath.Fit(412f, 917f, 2304f, 4096f, StickerFitMode.Cover, out float w, out float h);

            Assert.That(h, Is.EqualTo(917f).Within(Tol));
            Assert.That(w, Is.GreaterThan(412f));
            Assert.That(w / h, Is.EqualTo(2304f / 4096f).Within(Tol));
        }

        [Test]
        public void Fit_Contain_NarrowerArea_MatchesWidth()
        {
            StickerLayoutMath.Fit(412f, 917f, 2304f, 4096f, StickerFitMode.Contain, out float w, out float h);

            Assert.That(w, Is.EqualTo(412f).Within(Tol));
            Assert.That(h, Is.LessThanOrEqualTo(917f));
        }

        [Test]
        public void Fit_DegenerateInput_ReturnsZero()
        {
            StickerLayoutMath.Fit(0f, 917f, 2304f, 4096f, StickerFitMode.Contain, out float w, out float h);
            Assert.That(w, Is.EqualTo(0f));
            Assert.That(h, Is.EqualTo(0f));

            StickerLayoutMath.Fit(412f, 917f, 0f, 4096f, StickerFitMode.Cover, out w, out h);
            Assert.That(w, Is.EqualTo(0f));
            Assert.That(h, Is.EqualTo(0f));
        }

        [Test]
        public void NormalizedToLocal_Centre_IsOrigin()
        {
            StickerRect r = StickerLayoutMath.NormalizedToLocal(0.5f, 0.5f, 0.25f, 0.1f, 400f, 800f);

            Assert.That(r.CenterX, Is.EqualTo(0f).Within(Tol));
            Assert.That(r.CenterY, Is.EqualTo(0f).Within(Tol));
            Assert.That(r.Width, Is.EqualTo(100f).Within(Tol));
            Assert.That(r.Height, Is.EqualTo(80f).Within(Tol));
        }

        [Test]
        public void NormalizedToLocal_BottomLeftOrigin()
        {
            StickerRect r = StickerLayoutMath.NormalizedToLocal(0f, 0f, 0f, 0f, 400f, 800f);

            Assert.That(r.CenterX, Is.EqualTo(-200f).Within(Tol));
            Assert.That(r.CenterY, Is.EqualTo(-400f).Within(Tol));
        }

        [Test]
        public void LocalToNormalized_RoundTrips()
        {
            StickerRect r = StickerLayoutMath.NormalizedToLocal(0.73f, 0.26f, 0.3f, 0.15f, 412f, 732f);
            StickerLayoutMath.LocalToNormalized(r.CenterX, r.CenterY, 412f, 732f, out float nx, out float ny);

            Assert.That(nx, Is.EqualTo(0.73f).Within(Tol));
            Assert.That(ny, Is.EqualTo(0.26f).Within(Tol));
        }

        [Test]
        public void DesignRectToNormalized_Poster2FigmaStickers()
        {
            // Figma section "Poster2": close-up at (500, 3780) 2304x4096,
            // sticker1 at (1818, 4912) and sticker2 at (1818, 6505), both 710x603.
            StickerLayoutMath.DesignRectToNormalized(
                500f, 3780f, 2304f, 4096f, 1818f, 4912f, 710f, 603f,
                out float cx1, out float cy1, out float w1, out float h1);
            StickerLayoutMath.DesignRectToNormalized(
                500f, 3780f, 2304f, 4096f, 1818f, 6505f, 710f, 603f,
                out float cx2, out float cy2, out float w2, out float h2);

            Assert.That(cx1, Is.EqualTo(1673f / 2304f).Within(Tol));
            Assert.That(cy1, Is.EqualTo(1f - (1433.5f / 4096f)).Within(Tol));
            Assert.That(w1, Is.EqualTo(710f / 2304f).Within(Tol));
            Assert.That(h1, Is.EqualTo(603f / 4096f).Within(Tol));

            Assert.That(cx2, Is.EqualTo(cx1).Within(Tol));
            Assert.That(cy2, Is.LessThan(cy1), "sticker2 is lower on the poster");
            Assert.That(w2, Is.EqualTo(w1).Within(Tol));
            Assert.That(h2, Is.EqualTo(h1).Within(Tol));
        }

        [Test]
        public void StickerRect_Contains()
        {
            var r = new StickerRect(10f, 20f, 4f, 6f);

            Assert.That(r.Contains(10f, 20f), Is.True);
            Assert.That(r.Contains(12f, 23f), Is.True);
            Assert.That(r.Contains(12.1f, 20f), Is.False);
        }
    }

    public class StickerPeelTrackerTests
    {
        [Test]
        public void Reset_SetsRemainingToCount()
        {
            var t = new StickerPeelTracker();
            t.Reset(2);

            Assert.That(t.Count, Is.EqualTo(2));
            Assert.That(t.Remaining, Is.EqualTo(2));
            Assert.That(t.IsComplete, Is.False);
            Assert.That(t.FirstRemaining(), Is.EqualTo(0));
        }

        [Test]
        public void TryPeel_CountsEachStickerOnce()
        {
            var t = new StickerPeelTracker();
            t.Reset(2);

            Assert.That(t.TryPeel(0, out bool done), Is.True);
            Assert.That(done, Is.False);
            Assert.That(t.TryPeel(0, out done), Is.False, "double tap must not count twice");
            Assert.That(t.Remaining, Is.EqualTo(1));
            Assert.That(t.FirstRemaining(), Is.EqualTo(1));
        }

        [Test]
        public void TryPeel_ReportsCompletionExactlyOnce()
        {
            var t = new StickerPeelTracker();
            t.Reset(2);

            t.TryPeel(1, out bool first);
            t.TryPeel(0, out bool second);
            t.TryPeel(0, out bool third);

            Assert.That(first, Is.False);
            Assert.That(second, Is.True);
            Assert.That(third, Is.False);
            Assert.That(t.IsComplete, Is.True);
            Assert.That(t.FirstRemaining(), Is.EqualTo(-1));
        }

        [Test]
        public void TryPeel_OutOfRange_IsIgnored()
        {
            var t = new StickerPeelTracker();
            t.Reset(1);

            Assert.That(t.TryPeel(-1, out _), Is.False);
            Assert.That(t.TryPeel(5, out _), Is.False);
            Assert.That(t.Remaining, Is.EqualTo(1));
        }

        [Test]
        public void Reset_AfterPeeling_PutsEveryStickerBack()
        {
            var t = new StickerPeelTracker();
            t.Reset(2);
            t.TryPeel(0, out _);
            t.TryPeel(1, out _);

            t.Reset(2);

            Assert.That(t.Remaining, Is.EqualTo(2));
            Assert.That(t.IsRemaining(0), Is.True);
            Assert.That(t.IsRemaining(1), Is.True);
        }

        [Test]
        public void Reset_Zero_IsImmediatelyComplete()
        {
            var t = new StickerPeelTracker();
            t.Reset(0);

            Assert.That(t.IsComplete, Is.True);
            Assert.That(t.FirstRemaining(), Is.EqualTo(-1));

            t.Reset(-3);
            Assert.That(t.Count, Is.EqualTo(0));
        }
    }

    public class StickerPeelMotionTests
    {
        private const float Tol = 0.001f;

        [Test]
        public void Evaluate_AtZero_IsRestPose()
        {
            StickerPose p = StickerPeelMotion.Default.Evaluate(0f, 1);

            Assert.That(p.OffsetX, Is.EqualTo(0f));
            Assert.That(p.OffsetY, Is.EqualTo(0f));
            Assert.That(p.Scale, Is.EqualTo(1f));
            Assert.That(p.RotationDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void Evaluate_EndOfLift_IsLiftedScaledAndTilted()
        {
            StickerPeelMotion m = StickerPeelMotion.Default;
            StickerPose p = m.Evaluate(m.liftSeconds, 1);

            Assert.That(p.OffsetY, Is.EqualTo(m.liftHeight).Within(Tol));
            Assert.That(p.Scale, Is.EqualTo(m.liftScale).Within(Tol));
            Assert.That(p.RotationDegrees, Is.EqualTo(-m.liftTiltDegrees).Within(Tol));
        }

        [Test]
        public void Evaluate_Direction_MirrorsDriftAndSpin()
        {
            StickerPeelMotion m = StickerPeelMotion.Default;
            StickerPose right = m.Evaluate(0.4f, 1);
            StickerPose left = m.Evaluate(0.4f, -1);

            Assert.That(right.OffsetX, Is.GreaterThan(0f));
            Assert.That(left.OffsetX, Is.EqualTo(-right.OffsetX).Within(Tol));
            Assert.That(left.RotationDegrees, Is.EqualTo(-right.RotationDegrees).Within(Tol));
            Assert.That(left.OffsetY, Is.EqualTo(right.OffsetY).Within(Tol));
        }

        [Test]
        public void Evaluate_FallAccelerates()
        {
            StickerPeelMotion m = StickerPeelMotion.Default;
            float t0 = m.liftSeconds + 0.3f;
            float y0 = m.Evaluate(t0, 1).OffsetY;
            float y1 = m.Evaluate(t0 + 0.1f, 1).OffsetY;
            float y2 = m.Evaluate(t0 + 0.2f, 1).OffsetY;

            Assert.That(y1, Is.LessThan(y0));
            Assert.That(y0 - y1, Is.LessThan(y1 - y2), "each step should drop further than the last");
        }

        [Test]
        public void DurationFor_LandsExactlyAtFallDistance()
        {
            StickerPeelMotion m = StickerPeelMotion.Default;
            const float distance = 900f;
            float duration = m.DurationFor(distance);

            Assert.That(m.Evaluate(duration, 1).OffsetY, Is.EqualTo(-distance).Within(0.05f));
            Assert.That(m.IsFinished(duration - 0.01f, distance), Is.False);
            Assert.That(m.IsFinished(duration, distance), Is.True);
        }

        [Test]
        public void DurationFor_LongerFallTakesLonger()
        {
            StickerPeelMotion m = StickerPeelMotion.Default;

            Assert.That(m.DurationFor(1200f), Is.GreaterThan(m.DurationFor(300f)));
            Assert.That(m.DurationFor(-10f), Is.EqualTo(m.DurationFor(0f)).Within(Tol));
        }

        [Test]
        public void ZeroedStruct_DoesNotProduceNaN()
        {
            var m = new StickerPeelMotion();
            float d = m.DurationFor(500f);
            StickerPose p = m.Evaluate(0.5f, 1);

            Assert.That(float.IsNaN(d) || float.IsInfinity(d), Is.False);
            Assert.That(float.IsNaN(p.OffsetY), Is.False);
            Assert.That(float.IsNaN(p.Scale), Is.False);
        }
    }
}
