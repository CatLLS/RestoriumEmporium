// ============================================================
// RevealMask — the paint buffer behind every tool, as pure C#.
// WHAT & WHY: All six restoration tools are one operation: drag, reveal what is
//   underneath the stroke, report how much has been covered. That operation is
//   entirely arithmetic on a small byte grid, so it lives here with no
//   MonoBehaviour, no GameObject and no scene: it can be unit-tested headlessly
//   and reasoned about without opening the Editor. The Texture2D is a write-only
//   mirror of the grid, uploaded for the shader; it is never read back, so the
//   CPU always holds the authoritative copy.
// KEY DECISIONS:
//   - Coarse grid (128 x 218 by default), not screen resolution. 27,904 bytes
//     costs nothing, uploads in microseconds, and bilinear filtering on the
//     Texture2D makes the painted edge look smooth anyway. A full-resolution
//     mask would be ~30x the upload for no visible gain on a phone.
//   - Coverage is maintained INCREMENTALLY. Every write compares the old and the
//     new byte against the threshold and adjusts a running counter, so Coverage
//     is O(1). Nothing here scans the whole buffer per frame, and nothing here
//     calls ReadPixels or AsyncGPUReadback — a GPU readback would stall the
//     pipeline on mobile and is pointless when the CPU already owns the data.
//   - ASPECT CORRECTION, stated explicitly because it is the easy bug here:
//     radiusUv is expressed relative to the poster's WIDTH (that is the unit
//     ToolData.brushRadiusUv is authored in). The grid is NOT square, so
//     converting that one number into cell counts with the same multiplier on
//     both axes would paint an ellipse. Instead:
//         radiusCellsX = radiusUv * Width
//         radiusCellsY = radiusUv * SurfaceAspect * Height
//     where SurfaceAspect is the poster rect's width divided by its height.
//     When the grid's own aspect matches the poster's (the default: 128/218
//     against the poster's 322/577) the two expressions come out equal and the
//     cells are square in physical space. When they do not match — a resized
//     poster, a different grid resolution — setting SurfaceAspect keeps the
//     brush a true circle on screen. The same correction is applied to
//     StampLine's segment length, so stamp spacing is measured in real distance
//     rather than in raw UV units and a vertical drag is as dense as a
//     horizontal one.
//   - StampLine interpolates from the previous point EXCLUSIVE to the new point
//     INCLUSIVE. A stroke is therefore Stamp(first point) followed by one
//     StampLine per drag sample, and no sample point receives its dose twice —
//     double-dosing beads the stroke into visible dots at low strength.
//   - Nothing in this class throws. Every entry point guards NaN, infinity,
//     non-positive radius and out-of-range uv, because all three arrive
//     routinely from real touch input at the edge of the poster.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This is a plain C# class; there is no component to attach and
//     no asset to create. PosterLayerStack creates and owns one instance.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Restoration
{
    /// <summary>
    /// A coarse 8-bit reveal buffer plus its Texture2D mirror. Painted values
    /// accumulate towards 255; Coverage is the fraction of cells at or above the
    /// threshold, tracked incrementally.
    /// </summary>
    public sealed class RevealMask
    {
        /// <summary>Default cell columns. Roughly the poster's 322:577 aspect.</summary>
        public const int DefaultWidth = 128;

        /// <summary>Default cell rows.</summary>
        public const int DefaultHeight = 218;

        /// <summary>Default value at which a cell counts as revealed.</summary>
        public const byte DefaultThreshold = 128;

        /// <summary>Stamp spacing along a segment, as a fraction of the brush radius.</summary>
        private const float StepFactorOfRadius = 0.25f;

        /// <summary>Hard cap on stamps per segment, so a wild input cannot stall a frame.</summary>
        private const int MaxStepsPerSegment = 512;

        private readonly byte[] _cells;
        private readonly int _width;
        private readonly int _height;
        private readonly int _cellCount;
        private readonly byte _threshold;

        private Texture2D _texture;
        private int _coveredCells;
        private bool _dirty = true;
        private float _surfaceAspect;

        /// <summary>
        /// Creates a mask. <paramref name="coverageThreshold"/> is clamped to at
        /// least 1, so an untouched mask can never read as fully covered.
        /// </summary>
        public RevealMask(int width = DefaultWidth,
                          int height = DefaultHeight,
                          byte coverageThreshold = DefaultThreshold)
        {
            _width = width < 1 ? 1 : width;
            _height = height < 1 ? 1 : height;
            _cellCount = _width * _height;
            _threshold = coverageThreshold < 1 ? (byte)1 : coverageThreshold;
            _cells = new byte[_cellCount];
            _surfaceAspect = _width / (float)_height;
        }

        /// <summary>Cell columns.</summary>
        public int Width => _width;

        /// <summary>Cell rows.</summary>
        public int Height => _height;

        /// <summary>Total cells, i.e. Width * Height.</summary>
        public int CellCount => _cellCount;

        /// <summary>The value at or above which a cell counts as revealed.</summary>
        public byte CoverageThreshold => _threshold;

        /// <summary>Cells currently at or above the threshold. Maintained incrementally.</summary>
        public int CoveredCells => _coveredCells;

        /// <summary>Fraction of cells at or above the threshold, 0..1.</summary>
        public float Coverage => _coveredCells / (float)_cellCount;

        /// <summary>True when the grid has changed since the last ApplyToTexture.</summary>
        public bool IsDirty => _dirty;

        /// <summary>True once the Texture2D mirror has actually been allocated.</summary>
        public bool HasTexture => _texture != null;

        /// <summary>
        /// Width divided by height of the surface this mask is painted on, used to
        /// keep the brush circular. Defaults to the grid's own aspect. A
        /// non-finite or non-positive assignment is ignored.
        /// </summary>
        public float SurfaceAspect
        {
            get => _surfaceAspect;
            set
            {
                if (IsFinite(value) && value > 0f)
                {
                    _surfaceAspect = value;
                }
            }
        }

        /// <summary>
        /// The GPU mirror, allocated on first access. R8, bilinear, clamped.
        /// Contents are only current after ApplyToTexture.
        /// </summary>
        public Texture2D Texture
        {
            get
            {
                EnsureTexture();
                return _texture;
            }
        }

        /// <summary>Reads one cell. Returns 0 for out-of-range coordinates.</summary>
        public byte GetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height)
            {
                return 0;
            }

            return _cells[(y * _width) + x];
        }

        /// <summary>Samples the grid at a normalised point. Returns 0 outside 0..1.</summary>
        public byte SampleUv(Vector2 uv)
        {
            if (!IsFinite(uv.x) || !IsFinite(uv.y))
            {
                return 0;
            }

            return GetCell(Mathf.FloorToInt(uv.x * _width), Mathf.FloorToInt(uv.y * _height));
        }

        /// <summary>
        /// Adds one soft radial dab centred on <paramref name="uv"/>.
        /// <paramref name="radiusUv"/> is relative to the surface WIDTH.
        /// <paramref name="strength"/> is 0..1 of the full 0-255 range at the centre.
        /// Values accumulate and saturate at 255. Off-surface centres are allowed:
        /// only the part of the brush overlapping the grid is written, so dragging
        /// past the poster edge behaves the way a real cloth would.
        /// </summary>
        public void Stamp(Vector2 uv, float radiusUv, float strength)
        {
            if (!IsFinite(uv.x) || !IsFinite(uv.y) || !IsFinite(radiusUv) || !IsFinite(strength))
            {
                return;
            }

            if (radiusUv <= 0f || strength <= 0f)
            {
                return;
            }

            if (strength > 1f)
            {
                strength = 1f;
            }

            // Aspect correction: one width-relative radius becomes two cell radii.
            float radiusX = radiusUv * _width;
            float radiusY = radiusUv * _surfaceAspect * _height;

            if (radiusX < 0.5f)
            {
                radiusX = 0.5f;
            }

            if (radiusY < 0.5f)
            {
                radiusY = 0.5f;
            }

            float centreX = uv.x * _width;
            float centreY = uv.y * _height;

            int minX = Mathf.FloorToInt(centreX - radiusX);
            int maxX = Mathf.CeilToInt(centreX + radiusX);
            int minY = Mathf.FloorToInt(centreY - radiusY);
            int maxY = Mathf.CeilToInt(centreY + radiusY);

            if (minX < 0) { minX = 0; }
            if (minY < 0) { minY = 0; }
            if (maxX > _width - 1) { maxX = _width - 1; }
            if (maxY > _height - 1) { maxY = _height - 1; }

            if (minX > maxX || minY > maxY)
            {
                return;
            }

            float invRadiusX = 1f / radiusX;
            float invRadiusY = 1f / radiusY;
            float peak = strength * 255f;

            for (int y = minY; y <= maxY; y++)
            {
                float dy = ((y + 0.5f) - centreY) * invRadiusY;
                float dySq = dy * dy;
                if (dySq >= 1f)
                {
                    continue;
                }

                int row = y * _width;

                for (int x = minX; x <= maxX; x++)
                {
                    float dx = ((x + 0.5f) - centreX) * invRadiusX;
                    float distSq = (dx * dx) + dySq;
                    if (distSq >= 1f)
                    {
                        continue;
                    }

                    // Soft edge: smoothstep applied to (1 - normalised distance),
                    // so the dab is 1 at the centre and eases to 0 at the rim.
                    float t = 1f - Mathf.Sqrt(distSq);
                    float falloff = t * t * (3f - (2f * t));

                    int add = (int)((peak * falloff) + 0.5f);
                    if (add <= 0)
                    {
                        continue;
                    }

                    int index = row + x;
                    byte old = _cells[index];
                    if (old == 255)
                    {
                        continue;
                    }

                    int value = old + add;
                    if (value > 255)
                    {
                        value = 255;
                    }

                    _cells[index] = (byte)value;
                    _dirty = true;

                    if (old < _threshold && value >= _threshold)
                    {
                        _coveredCells++;
                    }
                }
            }
        }

        /// <summary>
        /// Fills the gap between two stroke samples so a fast flick leaves a
        /// continuous band rather than two dots. Stamps are spaced at a quarter of
        /// the brush radius, measured in real (aspect-corrected) distance, so the
        /// density of the band does not change with drag speed or direction.
        /// <para>
        /// <paramref name="fromUv"/> is treated as ALREADY stamped and is not
        /// re-stamped; <paramref name="toUv"/> always is. A stroke is therefore
        /// Stamp(first point) followed by one StampLine per subsequent sample.
        /// A zero-length segment still lays exactly one dab on
        /// <paramref name="toUv"/>, so holding a finger still keeps painting.
        /// </para>
        /// </summary>
        public void StampLine(Vector2 fromUv, Vector2 toUv, float radiusUv, float strength)
        {
            if (!IsFinite(fromUv.x) || !IsFinite(fromUv.y))
            {
                // No usable origin: degrade to a single dab at the destination.
                Stamp(toUv, radiusUv, strength);
                return;
            }

            if (!IsFinite(toUv.x) || !IsFinite(toUv.y) || !IsFinite(radiusUv) || radiusUv <= 0f)
            {
                return;
            }

            float du = toUv.x - fromUv.x;
            float dvRaw = toUv.y - fromUv.y;

            // Height-fraction converted to width-fraction, so the length below is a
            // real distance and the resulting step spacing is isotropic.
            float dv = dvRaw / _surfaceAspect;
            float distance = Mathf.Sqrt((du * du) + (dv * dv));

            int steps = Mathf.CeilToInt(distance / (radiusUv * StepFactorOfRadius));
            if (steps < 1)
            {
                steps = 1;
            }
            else if (steps > MaxStepsPerSegment)
            {
                steps = MaxStepsPerSegment;
            }

            float inverseSteps = 1f / steps;

            for (int i = 1; i <= steps; i++)
            {
                float t = i * inverseSteps;
                Stamp(new Vector2(fromUv.x + (du * t), fromUv.y + (dvRaw * t)), radiusUv, strength);
            }
        }

        /// <summary>Sets every cell to <paramref name="value"/> and refreshes Coverage.</summary>
        public void Fill(byte value)
        {
            for (int i = 0; i < _cellCount; i++)
            {
                _cells[i] = value;
            }

            _coveredCells = value >= _threshold ? _cellCount : 0;
            _dirty = true;
        }

        /// <summary>Resets the mask to nothing revealed.</summary>
        public void Clear()
        {
            Fill(0);
        }

        /// <summary>
        /// Uploads the grid to the Texture2D, but only when something changed.
        /// Safe to call every frame: it is a no-op while IsDirty is false, which
        /// is what keeps the upload to at most once per frame.
        /// </summary>
        public void ApplyToTexture()
        {
            if (!_dirty)
            {
                return;
            }

            EnsureTexture();
            _texture.SetPixelData(_cells, 0);
            _texture.Apply(false, false);
            _dirty = false;
        }

        /// <summary>Destroys the Texture2D mirror. The CPU grid stays usable.</summary>
        public void ReleaseTexture()
        {
            if (_texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(_texture);
            }
            else
            {
                Object.DestroyImmediate(_texture);
            }

            _texture = null;
            _dirty = true;
        }

        private void EnsureTexture()
        {
            if (_texture != null)
            {
                return;
            }

            // linear: the mask is coverage data, not colour, so it must not be
            // sRGB-decoded when the shader samples it.
            _texture = new Texture2D(_width, _height, TextureFormat.R8, false, true)
            {
                name = "RevealMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            _dirty = true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
