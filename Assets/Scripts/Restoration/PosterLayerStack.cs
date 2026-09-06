// ============================================================
// PosterLayerStack — two stacked Images and the mask that reveals between them.
// WHAT & WHY: Every restoration stage is "the poster goes from this sprite to
//   that sprite, wherever the player has scrubbed". Two UI Images occupying the
//   same rect give that for free: the bottom one is drawn plainly, the top one
//   is drawn through the RevealMask by the PosterReveal shader. This component
//   owns that pair, owns the mask, and is the IRevealSurface everything else
//   paints against.
// KEY DECISIONS:
//   - The pencil stage inverts the layer order, not just the shader. With
//     invertMask the TOP layer is erased away to expose what is underneath, so
//     the top must hold 'fromSprite' (posterDry) and the bottom 'toSprite'
//     (posterFinal). With a normal stage the top holds 'toSprite' and fades in.
//     SetStage picks the assignment; the shader only ever flips one lerp.
//     revealTint is applied to whichever Image holds toSprite, because "the
//     revealed layer" is what the tint is authored to describe (the roller's
//     wet-adhesive sheen), and that is the top layer in every stage but one.
//   - _Invert is a float, not a shader keyword. A keyword would add a second
//     shader variant and a per-stage material change for what is one lerp in
//     the fragment shader; on a single full-screen quad the branchless version
//     is strictly cheaper and never causes a runtime shader compile hitch.
//   - A per-instance Material is created from the serialized source with
//     new Material(source) and destroyed in OnDestroy. Assigning the shared
//     asset would let one poster's mask leak into another and would dirty the
//     asset on disk in the Editor.
//   - Screen-to-UV goes through RectTransformUtility.ScreenPointToLocalPointIn-
//     Rectangle with the CANVAS CAMERA passed in. The canvas is Screen Space -
//     Camera with a perspective camera; passing null there (the Overlay
//     shortcut) silently produces coordinates that are wrong by a few percent
//     near the screen edges, which reads as "the brush lags my finger".
//   - The mask is uploaded in LateUpdate, once, and only when dirty. Uploading
//     inside the drag callback would upload several times on a frame that
//     delivers several move events.
//   - The mask's SurfaceAspect is refreshed from the live rect whenever the
//     stage changes or a stroke begins, so the brush stays circular even though
//     the mask grid is not square and the poster can be resized by the layout.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [x] IMPORT THE ART FIRST. Select every poster sprite in
//     Assets/Art/Posters/poster1/ (posterBeforeDusting, posterNoDust,
//     posterYellowWet, posterWhiteWet, posterDry, posterFinal) and also
//     Assets/Art/LinnenAssets/PosterBack. In the Inspector set:
//       Texture Type = Sprite (2D and UI)
//       Sprite Mode  = Single
//       Mesh Type    = Full Rect        <-- REQUIRED. The default "Tight" crops
//                                           the quad to the opaque pixels, which
//                                           moves the UVs the reveal shader uses
//                                           and makes the mask land in the wrong
//                                           place. Full Rect is not optional.
//       Generate Physics Shape = off
//     Then press Apply. Do NOT add these sprites to a Sprite Atlas: atlasing
//     remaps their UVs into the atlas and breaks the mask the same way.
// [x] Create the material: right-click in Assets/Art -> Create -> Material,
//     name it "M_PosterReveal". At the top of its Inspector set Shader to
//     Restorium/UI/PosterReveal. Leave every field at its default; this asset is
//     only a template, the component copies it at runtime.
// [x] Build the poster object under the Canvas:
//       Right-click Canvas -> UI -> Image, name it "Poster".
//       On Poster's Rect Transform set Width 322, Height 577 (or whatever the
//       layout calls for - only the ratio matters).
//       On Poster's Image component: Source Image = None, and UNTICK
//       "Raycast Target" only if you put RevealMaskPainter on a separate child;
//       leave it TICKED if RevealMaskPainter goes on this object (recommended).
// [x] Right-click Poster -> UI -> Image, name it "BottomLayer".
//       Set its Rect Transform anchor preset to "stretch/stretch" with
//       Left/Right/Top/Bottom all 0 (hold Alt when clicking the preset).
//       Untick "Raycast Target". Leave Material = None.
// [x] Right-click Poster -> UI -> Image, name it "TopLayer". Same stretch
//       anchors, same 0 offsets. Untick "Raycast Target". Drag the
//       M_PosterReveal material into its Material field.
//       TopLayer must be BELOW BottomLayer in the Hierarchy list (uGUI draws
//       later siblings on top).
// [x] Add this component: select Poster -> Add Component -> Poster Layer Stack.
// [x] Wire its fields:
//       Poster Rect     <- the Poster object itself
//       Bottom Layer    <- BottomLayer
//       Top Layer       <- TopLayer
//       Reveal Material <- the M_PosterReveal asset from the Project window
//       Canvas          <- the root Canvas object (leave empty to auto-find)
// [x] Leave Mask Width 128 / Mask Height 218 unless the poster's aspect ratio
//     changes; they should roughly match the poster's width:height.
// ---------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.Restoration
{
    using RestoriumEmporium.Data;

    [DisallowMultipleComponent]
    public class PosterLayerStack : MonoBehaviour, IRevealSurface
    {
        [Header("Layers")]
        [Tooltip("The rect the mask UV space is measured against. Usually this object.")]
        [SerializeField] private RectTransform posterRect;

        [Tooltip("Drawn plainly. Holds the sprite the player is scrubbing away from.")]
        [SerializeField] private Image bottomLayer;

        [Tooltip("Drawn through the reveal mask. Needs the PosterReveal material.")]
        [SerializeField] private Image topLayer;

        [Header("Material")]
        [Tooltip("Template material using Restorium/UI/PosterReveal. Copied at runtime.")]
        [SerializeField] private Material revealMaterial;

        [Header("Canvas")]
        [Tooltip("Root Canvas. Left empty, the nearest Canvas in the parents is used.")]
        [SerializeField] private Canvas canvas;

        [Header("Mask")]
        [Tooltip("Mask columns. Keep the width:height ratio close to the poster's.")]
        [SerializeField] private int maskWidth = RevealMask.DefaultWidth;

        [Tooltip("Mask rows.")]
        [SerializeField] private int maskHeight = RevealMask.DefaultHeight;

        [Range(1, 255)]
        [Tooltip("A cell counts as revealed at or above this value.")]
        [SerializeField] private int coverageThreshold = RevealMask.DefaultThreshold;

        [Header("Brush")]
        [Range(0.02f, 0.5f)]
        [Tooltip("Fallback radius, as a fraction of poster width. " +
                 "RestorationController overwrites this per stage.")]
        [SerializeField] private float brushRadiusUv = 0.12f;

        [Range(0.05f, 1f)]
        [Tooltip("How much a single dab adds. Lower means the player must scrub " +
                 "over the same spot more than once.")]
        [SerializeField] private float strokeStrength = 0.85f;

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int InvertId = Shader.PropertyToID("_Invert");

        private RevealMask _mask;
        private Material _runtimeMaterial;
        private Canvas _resolvedCanvas;
        private RestorationStageData _stage;

        private Vector2 _lastUv;
        private bool _strokeActive;
        private float _lastReportedCoverage = -1f;

        /// <inheritdoc />
        public float Coverage => _mask != null ? _mask.Coverage : 0f;

        /// <inheritdoc />
        public event Action<float> CoverageChanged;

        /// <inheritdoc />
        public float BrushRadiusUv
        {
            get => brushRadiusUv;
            set => brushRadiusUv = value > 0f ? value : brushRadiusUv;
        }

        /// <summary>How much a single dab adds, 0..1. Set from the tool if wanted.</summary>
        public float StrokeStrength
        {
            get => strokeStrength;
            set => strokeStrength = Mathf.Clamp01(value);
        }

        /// <summary>The stage currently loaded into the two layers, or null.</summary>
        public RestorationStageData Stage => _stage;

        /// <summary>The underlying buffer. Exposed for tests and for the FX layer.</summary>
        public RevealMask Mask
        {
            get
            {
                EnsureMask();
                return _mask;
            }
        }

        private void Awake()
        {
            if (posterRect == null)
            {
                posterRect = transform as RectTransform;
            }

            _resolvedCanvas = canvas != null ? canvas : GetComponentInParent<Canvas>();

            EnsureMask();
            EnsureMaterial();

            // A freshly allocated Texture2D holds undefined bytes; clear and upload
            // once so nothing is revealed before the first stage is loaded.
            ResetMask();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeMaterial);
                }
                else
                {
                    DestroyImmediate(_runtimeMaterial);
                }

                _runtimeMaterial = null;
            }

            _mask?.ReleaseTexture();
        }

        private void LateUpdate()
        {
            // At most one upload per frame, and only when something was painted.
            if (_mask != null && _mask.IsDirty)
            {
                _mask.ApplyToTexture();
            }
        }

        /// <summary>
        /// Loads a stage into the two layers and clears the mask. Safe to call with
        /// null, which blanks both layers.
        /// </summary>
        public void SetStage(RestorationStageData stage)
        {
            EnsureMask();
            EnsureMaterial();

            _stage = stage;
            _strokeActive = false;

            if (stage == null)
            {
                if (bottomLayer != null) { bottomLayer.sprite = null; }
                if (topLayer != null) { topLayer.sprite = null; }
                ResetMask();
                return;
            }

            // With invertMask the top layer is the one that erases away, so it must
            // carry fromSprite and the bottom carries what is exposed underneath.
            bool inverted = stage.invertMask;
            Sprite top = inverted ? stage.fromSprite : stage.toSprite;
            Sprite bottom = inverted ? stage.toSprite : stage.fromSprite;

            // The tint describes the revealed artwork, which is always toSprite.
            Color topColor = inverted ? Color.white : stage.revealTint;
            Color bottomColor = inverted ? stage.revealTint : Color.white;

            if (bottomLayer != null)
            {
                bottomLayer.sprite = bottom;
                bottomLayer.color = bottomColor;
                bottomLayer.enabled = bottom != null;
            }

            if (topLayer != null)
            {
                topLayer.sprite = top;
                topLayer.color = topColor;
                topLayer.enabled = top != null;
            }

            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.SetFloat(InvertId, inverted ? 1f : 0f);
            }

            if (stage.brushRadiusOverride > 0f)
            {
                brushRadiusUv = stage.brushRadiusOverride;
            }

            ResetMask();
        }

        /// <inheritdoc />
        public void BeginStroke(Vector2 uv)
        {
            EnsureMask();
            RefreshSurfaceAspect();

            _lastUv = uv;
            _strokeActive = true;

            _mask.Stamp(uv, brushRadiusUv, strokeStrength);
            ReportCoverage();
        }

        /// <inheritdoc />
        public void ContinueStroke(Vector2 uv)
        {
            EnsureMask();

            if (!_strokeActive)
            {
                // A move without a press: treat it as the start of a stroke rather
                // than dropping the input.
                BeginStroke(uv);
                return;
            }

            _mask.StampLine(_lastUv, uv, brushRadiusUv, strokeStrength);
            _lastUv = uv;
            ReportCoverage();
        }

        /// <inheritdoc />
        public void EndStroke()
        {
            _strokeActive = false;
        }

        /// <inheritdoc />
        public void FillCompletely()
        {
            EnsureMask();
            _mask.Fill(255);
            _strokeActive = false;
            ReportCoverage();
        }

        /// <inheritdoc />
        public void ResetMask()
        {
            EnsureMask();
            _mask.Clear();
            _mask.ApplyToTexture();
            _strokeActive = false;
            ReportCoverage();
        }

        /// <summary>
        /// Converts a screen point into 0..1 coordinates across the poster rect.
        /// Returns false when the point cannot be projected onto the rect's plane.
        /// The result is NOT clamped: a finger dragged past the edge gives values
        /// outside 0..1, and RevealMask handles those by clipping the brush.
        /// </summary>
        public bool TryScreenToUv(Vector2 screenPosition, out Vector2 uv)
        {
            uv = default;

            RectTransform rectTransform = posterRect != null ? posterRect : transform as RectTransform;
            if (rectTransform == null)
            {
                return false;
            }

            Camera eventCamera = ResolveEventCamera();

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPosition, eventCamera, out Vector2 local))
            {
                return false;
            }

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            uv = new Vector2((local.x - rect.xMin) / rect.width,
                             (local.y - rect.yMin) / rect.height);
            return true;
        }

        /// <summary>
        /// Screen-point convenience: converts and paints in one call. Returns false
        /// when the point could not be projected, in which case nothing is painted.
        /// </summary>
        public bool BeginStrokeAtScreenPoint(Vector2 screenPosition)
        {
            if (!TryScreenToUv(screenPosition, out Vector2 uv))
            {
                return false;
            }

            BeginStroke(uv);
            return true;
        }

        /// <summary>Screen-point counterpart of ContinueStroke.</summary>
        public bool ContinueStrokeAtScreenPoint(Vector2 screenPosition)
        {
            if (!TryScreenToUv(screenPosition, out Vector2 uv))
            {
                return false;
            }

            ContinueStroke(uv);
            return true;
        }

        /// <summary>
        /// The camera uGUI would use for this canvas. Screen Space - Camera and
        /// World Space need the canvas camera; Overlay needs null.
        /// </summary>
        private Camera ResolveEventCamera()
        {
            if (_resolvedCanvas == null)
            {
                _resolvedCanvas = canvas != null ? canvas : GetComponentInParent<Canvas>();
            }

            if (_resolvedCanvas == null)
            {
                return null;
            }

            Canvas root = _resolvedCanvas.rootCanvas != null ? _resolvedCanvas.rootCanvas : _resolvedCanvas;
            return root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        }

        private void EnsureMask()
        {
            if (_mask != null)
            {
                return;
            }

            _mask = new RevealMask(maskWidth, maskHeight, (byte)Mathf.Clamp(coverageThreshold, 1, 255));
            RefreshSurfaceAspect();
        }

        private void EnsureMaterial()
        {
            if (_runtimeMaterial != null || topLayer == null)
            {
                return;
            }

            Material source = revealMaterial != null ? revealMaterial : topLayer.material;
            if (source == null)
            {
                Debug.LogWarning(
                    "[PosterLayerStack] No reveal material assigned; the top layer will " +
                    "draw unmasked. Assign the Restorium/UI/PosterReveal material.", this);
                return;
            }

            _runtimeMaterial = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            _runtimeMaterial.SetTexture(MaskTexId, Mask.Texture);
            topLayer.material = _runtimeMaterial;
        }

        /// <summary>
        /// Keeps the brush circular on screen: the mask grid is not square, so the
        /// width-relative radius needs the poster's real aspect to become cell
        /// radii on both axes. See RevealMask's aspect note.
        /// </summary>
        private void RefreshSurfaceAspect()
        {
            if (_mask == null)
            {
                return;
            }

            RectTransform rectTransform = posterRect != null ? posterRect : transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            Rect rect = rectTransform.rect;
            if (rect.width > 0f && rect.height > 0f)
            {
                _mask.SurfaceAspect = rect.width / rect.height;
            }
        }

        private void ReportCoverage()
        {
            float coverage = _mask.Coverage;
            if (Mathf.Approximately(coverage, _lastReportedCoverage))
            {
                return;
            }

            _lastReportedCoverage = coverage;
            CoverageChanged?.Invoke(coverage);
        }
    }
}
