// ============================================================
// StickerPeelMotion — the curve a sticker follows when it is tapped off.
// WHAT & WHY: The peel is the whole payoff of the sticker stage, so its feel is
//   worth tuning in one place: a quick lift (the sticker pops up, grows a little
//   and tilts as its corner comes free), then a gravity fall with a sideways
//   drift and a lazy spin until it leaves the bottom of the screen. This class is
//   the maths of that motion as a pure function of time, so StickerView only has
//   to sample it once per frame and the curve is unit tested
//   (Tests/EditMode/Logic/StickerPeelMotionTests.cs).
// KEY DECISIONS:
//   - A pure function Evaluate(t) rather than an integrating simulation. Sampling
//     by elapsed time makes the motion frame-rate independent and exactly
//     reproducible, and a hitchy frame cannot make a sticker "tunnel" or stall.
//   - Real gravity (y = v0 t - g t^2 / 2) after the lift, with a small upward pop
//     first. A constant-speed slide reads as UI; the little hop and accelerating
//     drop is what reads as a physical thing falling.
//   - Duration is SOLVED from the fall distance, not authored. The view knows how
//     far it is from the bottom of the screen; the motion answers how long that
//     takes, so a sticker near the top falls for longer, exactly as it should.
//   - `direction` (+1 / -1) makes a sticker drift and spin away from the middle of
//     the close-up, so two stickers never fall in the same boring way.
//   - A struct with public fields so the view can expose it in the Inspector as
//     one tidy foldout; Default gives tuned values.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# helper. Tune the numbers on StickerRemovalScreen ->
//     "Peel Motion" in the Inspector while in Play mode.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Restoration
{
    /// <summary>One sampled frame of the peel: offset from the rest position, scale, rotation.</summary>
    public struct StickerPose
    {
        public float OffsetX;
        public float OffsetY;
        public float Scale;
        public float RotationDegrees;
    }

    [Serializable]
    public struct StickerPeelMotion
    {
        /// <summary>Seconds the lift (pop up, grow, tilt) takes before the fall begins.</summary>
        public float liftSeconds;

        /// <summary>How far the sticker rises during the lift, in local UI units.</summary>
        public float liftHeight;

        /// <summary>Scale at the top of the lift. 1 = no growth.</summary>
        public float liftScale;

        /// <summary>Tilt reached at the end of the lift, degrees.</summary>
        public float liftTiltDegrees;

        /// <summary>Upward speed the fall starts with (units/s). Gives the little hop.</summary>
        public float popVelocity;

        /// <summary>Downward acceleration, units/s^2. ~3000 feels right on a 917-tall layout.</summary>
        public float gravity;

        /// <summary>Sideways drift speed during the fall, units/s.</summary>
        public float driftSpeed;

        /// <summary>Spin during the fall, degrees/s.</summary>
        public float spinDegreesPerSecond;

        public static StickerPeelMotion Default => new StickerPeelMotion
        {
            liftSeconds = 0.16f,
            liftHeight = 24f,
            liftScale = 1.12f,
            liftTiltDegrees = 9f,
            popVelocity = 240f,
            gravity = 3000f,
            driftSpeed = 110f,
            spinDegreesPerSecond = 150f
        };

        private float SafeLift => liftSeconds > 0.0001f ? liftSeconds : 0.0001f;
        private float SafeGravity => gravity > 1f ? gravity : 1f;

        /// <summary>
        /// Samples the motion <paramref name="t"/> seconds after the tap.
        /// <paramref name="direction"/> is +1 (drift right, spin clockwise) or -1.
        /// </summary>
        public StickerPose Evaluate(float t, int direction)
        {
            float dir = direction < 0 ? -1f : 1f;
            float lift = SafeLift;
            float scaleTop = liftScale > 0f ? liftScale : 1f;
            StickerPose pose;

            if (t <= 0f)
            {
                pose.OffsetX = 0f;
                pose.OffsetY = 0f;
                pose.Scale = 1f;
                pose.RotationDegrees = 0f;
                return pose;
            }

            if (t < lift)
            {
                float p = t / lift;
                float eased = EaseOutCubic(p);

                pose.OffsetX = 0f;
                pose.OffsetY = liftHeight * eased;
                pose.Scale = 1f + ((scaleTop - 1f) * EaseOutBack(p));

                // Negative = clockwise in Unity, which is what drifting right looks like.
                pose.RotationDegrees = -dir * liftTiltDegrees * eased;
                return pose;
            }

            float tf = t - lift;
            pose.OffsetX = dir * driftSpeed * tf;
            pose.OffsetY = liftHeight + (popVelocity * tf) - (0.5f * SafeGravity * tf * tf);
            pose.Scale = scaleTop;
            pose.RotationDegrees = -dir * (liftTiltDegrees + (spinDegreesPerSecond * tf));
            return pose;
        }

        /// <summary>
        /// Total seconds until the sticker has dropped <paramref name="fallDistance"/>
        /// units below its rest position (lift included).
        /// </summary>
        public float DurationFor(float fallDistance)
        {
            float d = fallDistance > 0f ? fallDistance : 0f;
            float g = SafeGravity;

            // Solve liftHeight + pop*tf - g*tf^2/2 = -d for the positive root.
            float c = liftHeight + d;
            float disc = (popVelocity * popVelocity) + (2f * g * c);
            float tf = (popVelocity + (float)Math.Sqrt(disc > 0f ? disc : 0f)) / g;

            return SafeLift + (tf > 0f ? tf : 0f);
        }

        /// <summary>True once the sticker is at least fallDistance below its rest position.</summary>
        public bool IsFinished(float t, float fallDistance)
        {
            return t >= DurationFor(fallDistance);
        }

        private static float EaseOutCubic(float p)
        {
            float inv = 1f - Clamp01(p);
            return 1f - (inv * inv * inv);
        }

        /// <summary>Overshoots slightly then settles: the "pop" as the sticker lets go.</summary>
        private static float EaseOutBack(float p)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = Clamp01(p) - 1f;
            return 1f + (c3 * x * x * x) + (c1 * x * x);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
