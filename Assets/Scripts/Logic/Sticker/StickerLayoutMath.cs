// ============================================================
// StickerLayoutMath — the rect arithmetic behind the sticker removal close-up.
// WHAT & WHY: The close-up image has to be fitted into whatever area the screen
//   gives it without stretching (2304 x 4096 art on a 412 x 917 phone layout),
//   and every sticker is authored as a 0..1 rectangle ACROSS THE IMAGE, not across
//   the screen. Turning "0.73 across, 0.65 up, 31% wide" into a local position and
//   size is plain maths, so it lives here with no UnityEngine reference and is
//   covered by NUnit tests (Tests/EditMode/Logic/StickerLayoutTests.cs).
// KEY DECISIONS:
//   - Plain floats and a tiny StickerRect struct instead of Vector2/Rect, so the
//     tests run under `dotnet test` without Unity's DLLs.
//   - Positions are returned relative to the CENTRE of the sticker root. The
//     root's children use a centred pivot/anchor, which keeps the maths identical
//     whatever size the root ends up at.
//   - Fit supports Contain (whole image visible, letterboxed) and Cover (fills the
//     area, crops the overflow). The screen picks one in the Inspector; both keep
//     the image's aspect ratio, which is what keeps the stickers on the right spot.
//   - Degenerate input (zero or negative sizes) returns zero sizes instead of
//     NaN/Infinity: a missing sprite in the Editor should draw nothing, not throw.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Plain C# helper used by StickerRemovalScreen and the stage
//     inspector preview. There is no component to attach.
// ---------------------------------------------------------------

namespace RestoriumEmporium.Restoration
{
    /// <summary>A centre + size rectangle in some local space. Plain floats on purpose.</summary>
    public struct StickerRect
    {
        public float CenterX;
        public float CenterY;
        public float Width;
        public float Height;

        public StickerRect(float centerX, float centerY, float width, float height)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
        }

        public float Left => CenterX - (Width * 0.5f);
        public float Right => CenterX + (Width * 0.5f);
        public float Bottom => CenterY - (Height * 0.5f);
        public float Top => CenterY + (Height * 0.5f);

        /// <summary>True when the point lies inside (edges included).</summary>
        public bool Contains(float x, float y)
        {
            return x >= Left && x <= Right && y >= Bottom && y <= Top;
        }
    }

    public enum StickerFitMode
    {
        /// <summary>The whole image is visible; empty bands appear on two sides if the aspects differ.</summary>
        Contain = 0,

        /// <summary>The image fills the area; the overflow on two sides is cropped by the screen edge.</summary>
        Cover = 1
    }

    public static class StickerLayoutMath
    {
        /// <summary>
        /// Size of an image with aspect contentW:contentH fitted into areaW x areaH
        /// without distortion. Returns (0, 0) for degenerate input.
        /// </summary>
        public static void Fit(
            float areaW, float areaH, float contentW, float contentH, StickerFitMode mode,
            out float width, out float height)
        {
            width = 0f;
            height = 0f;

            if (areaW <= 0f || areaH <= 0f || contentW <= 0f || contentH <= 0f)
            {
                return;
            }

            float scaleX = areaW / contentW;
            float scaleY = areaH / contentH;
            float scale = mode == StickerFitMode.Cover
                ? (scaleX > scaleY ? scaleX : scaleY)
                : (scaleX < scaleY ? scaleX : scaleY);

            width = contentW * scale;
            height = contentH * scale;
        }

        /// <summary>
        /// Converts a normalised sticker (centre and size as 0..1 fractions of the
        /// image, (0,0) = bottom-left) into a rect relative to the CENTRE of a
        /// root that is rootW x rootH and exactly covers the image.
        /// </summary>
        public static StickerRect NormalizedToLocal(
            float normCenterX, float normCenterY, float normWidth, float normHeight,
            float rootW, float rootH)
        {
            if (rootW <= 0f || rootH <= 0f)
            {
                return new StickerRect(0f, 0f, 0f, 0f);
            }

            float w = normWidth > 0f ? normWidth * rootW : 0f;
            float h = normHeight > 0f ? normHeight * rootH : 0f;

            return new StickerRect(
                (normCenterX - 0.5f) * rootW,
                (normCenterY - 0.5f) * rootH,
                w,
                h);
        }

        /// <summary>
        /// The inverse of NormalizedToLocal for a point: a position relative to the
        /// root's centre back to 0..1 across the image. Used by the Editor preview
        /// when the human drags a sticker around. Not clamped.
        /// </summary>
        public static void LocalToNormalized(
            float localX, float localY, float rootW, float rootH,
            out float normX, out float normY)
        {
            normX = rootW > 0f ? (localX / rootW) + 0.5f : 0.5f;
            normY = rootH > 0f ? (localY / rootH) + 0.5f : 0.5f;
        }

        /// <summary>
        /// Converts a Figma-style top-left pixel rect of a sticker placed on top of
        /// the close-up (both in the same design space) into normalised values with
        /// a bottom-left origin. This is how the default positions were derived.
        /// </summary>
        public static void DesignRectToNormalized(
            float imageX, float imageY, float imageW, float imageH,
            float stickerX, float stickerY, float stickerW, float stickerH,
            out float normCenterX, out float normCenterY, out float normWidth, out float normHeight)
        {
            if (imageW <= 0f || imageH <= 0f)
            {
                normCenterX = 0.5f;
                normCenterY = 0.5f;
                normWidth = 0f;
                normHeight = 0f;
                return;
            }

            float centreXFromLeft = (stickerX - imageX) + (stickerW * 0.5f);
            float centreYFromTop = (stickerY - imageY) + (stickerH * 0.5f);

            normCenterX = centreXFromLeft / imageW;
            normCenterY = 1f - (centreYFromTop / imageH);
            normWidth = stickerW / imageW;
            normHeight = stickerH / imageH;
        }
    }
}
