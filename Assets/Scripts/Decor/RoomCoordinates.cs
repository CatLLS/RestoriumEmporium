// ============================================================
// RoomCoordinates — converts decoration positions between the save file's
//   normalised 0..1 room space and the room RectTransform's local pixels.
// WHAT & WHY: OwnedDecoration stores an item's CENTRE as 0..1 inside the desk-hub
//   room, (0,0) = bottom-left, so a save survives any screen size. The view needs
//   local pixels to draw and to follow a finger. This is the one place that maths
//   lives, and it also enforces the rule "an item can never leave the room".
// KEY DECISIONS:
//   - Plain C#, no UnityEngine (not even Vector2): the maths is unit-tested with
//     plain NUnit outside Unity (Tests/EditMode/Logic/RoomCoordinatesTests.cs).
//     The Unity side passes RectTransform.rect as a RoomRect.
//   - Results come back through out parameters, never as new objects, because
//     ToNormalized/Clamp run on every drag event (no per-frame allocation).
//   - Clamping keeps the WHOLE item inside the room, not just its centre: the
//     centre is limited to [halfSize, 1 - halfSize]. If an item is bigger than
//     the room on an axis it is simply centred on that axis.
//   - NaN / infinity (a corrupted or hand-edited save) is treated as the room
//     centre rather than propagated, so one bad value can't make an item vanish.
//   - A zero-size room (layout not built yet) maps everything to the room origin
//     and reports 0.5 back, instead of dividing by zero.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# helper used by DecorationRoom and DecorationView.
// ---------------------------------------------------------------

using System;

namespace RestoriumEmporium.Decor
{
    /// <summary>
    /// The room rectangle in its own local space, like UnityEngine.Rect:
    /// (X, Y) is the bottom-left corner, Width/Height its size.
    /// </summary>
    public readonly struct RoomRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public RoomRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width < 0f ? 0f : width;
            Height = height < 0f ? 0f : height;
        }

        public bool IsEmpty => Width <= 0f || Height <= 0f;
    }

    public static class RoomCoordinates
    {
        /// <summary>Default position for anything invalid: the room centre.</summary>
        public const float Centre = 0.5f;

        /// <summary>Normalised (0..1, bottom-left origin) to room-local pixels.</summary>
        public static void ToLocal(in RoomRect room, float nx, float ny, out float localX, out float localY)
        {
            localX = room.X + Sanitize(nx) * room.Width;
            localY = room.Y + Sanitize(ny) * room.Height;
        }

        /// <summary>Room-local pixels to normalised. Not clamped: call Clamp afterwards.</summary>
        public static void ToNormalized(in RoomRect room, float localX, float localY, out float nx, out float ny)
        {
            nx = room.Width > 0f && IsFinite(localX) ? (localX - room.X) / room.Width : Centre;
            ny = room.Height > 0f && IsFinite(localY) ? (localY - room.Y) / room.Height : Centre;
        }

        /// <summary>
        /// Clamps a normalised centre so an item of <paramref name="itemWidth"/> x
        /// <paramref name="itemHeight"/> pixels stays fully inside the room.
        /// </summary>
        public static void Clamp(in RoomRect room, float itemWidth, float itemHeight, ref float nx, ref float ny)
        {
            nx = ClampAxis(Sanitize(nx), room.Width, itemWidth);
            ny = ClampAxis(Sanitize(ny), room.Height, itemHeight);
        }

        /// <summary>Convenience: local pixels -> clamped normalised, in one call.</summary>
        public static void LocalToClampedNormalized(in RoomRect room, float localX, float localY,
            float itemWidth, float itemHeight, out float nx, out float ny)
        {
            ToNormalized(room, localX, localY, out nx, out ny);
            Clamp(room, itemWidth, itemHeight, ref nx, ref ny);
        }

        /// <summary>Replaces NaN/infinity with the centre. Does not clamp.</summary>
        public static float Sanitize(float value) => IsFinite(value) ? value : Centre;

        private static float ClampAxis(float n, float roomSize, float itemSize)
        {
            if (roomSize <= 0f)
            {
                return Centre;
            }

            if (!IsFinite(itemSize) || itemSize < 0f)
            {
                itemSize = 0f;
            }

            var half = itemSize * 0.5f / roomSize;

            if (half >= 0.5f)
            {
                // Item wider/taller than the room: the only position that shows
                // as much of it as possible is the middle.
                return Centre;
            }

            return Math.Max(half, Math.Min(1f - half, n));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
