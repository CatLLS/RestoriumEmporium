// ============================================================
// StickerRemovalScreen — the zoomed-in poster you tap stickers off.
// WHAT & WHY: Poster 2 arrives with two stickers someone slapped on it during a
//   botched earlier "restoration". Its StageKind.StickerPeel stage plays on this
//   screen instead of the scrub screens: a close-up of the poster fills the view,
//   the stickers sit on it where the stage asset says, and each tap peels one off
//   (sound, lift, tilt, fall off the bottom of the screen). When the last one has
//   fallen, a short beat later, the stage is completed through
//   IRestorationRuntime.ForceCompleteCurrentStage() and the normal flow resumes.
// KEY DECISIONS:
//   - Derives from RestorationScreenBase for the header and the runtime lookup,
//     but leaves Poster Stack Root and Tool Bar Root EMPTY on purpose: with no
//     stack root RestorationPresenter deactivates the shared poster while this
//     screen is up (the close-up IS the poster here), and there are no tools.
//   - The close-up keeps its aspect ratio: its rect is fitted into Close Up Area
//     (Cover by default, Contain optional) and Sticker Root is given exactly the
//     same rect. Sticker positions are 0..1 fractions of the IMAGE, so matching
//     the root to the displayed image is what keeps them on the right spot on
//     every phone shape. The maths is StickerLayoutMath (unit tested).
//   - Stickers are pooled StickerViews, created once and re-bound. Entering the
//     stage (OnEnable) re-spawns EVERY sticker: individual peels are not saved,
//     so a relaunch or a trip to the journal mid-stage simply starts it over.
//     The stage takes seconds; persisting peels would be complexity for nothing.
//   - The tap is accepted by StickerPeelTracker (pure C#, unit tested) before
//     anything happens, so a double tap can never count twice or complete early.
//     GameSignals.StickerPeeled(remaining) is raised on the tap itself, which is
//     the moment the player sees the sticker come free (the tutorial reacts to it).
//   - Completion waits for the LAST sticker to finish falling plus a short beat
//     (unscaled), then calls ForceCompleteCurrentStage once. The controller then
//     applies its own settle before routing on. A stage authored with no
//     stickers (or with empty sprites) completes after the beat with a warning
//     instead of soft-locking the player.
//   - The tutorial anchor "sticker.next" is an invisible marker rect that is
//     moved over the first sticker still on the poster, rather than one anchor
//     per sticker: anchor ids must be unique, and one moving marker always points
//     at the right one.
//   - Editor help: select this object in the Scene view with "Preview Stage" set
//     and the sticker rects are outlined as gizmos over the close-up. The stage
//     asset's own Inspector also has a drag-to-position preview
//     (Editor/Posters/StickerStageInspector.cs).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform values in the 412 x 917 reference layout.
//
// A) THE SCREEN ROOT
// [ ] Right-click Canvas -> Create Empty. Name it exactly: StickerRemovalScreen
// [ ] Rect Transform: anchor preset stretch/stretch (hold Alt when clicking),
//     Left/Right/Top/Bottom = 0.
// [ ] Add Component -> Sticker Removal Screen (this script).
// [ ] Untick the object's checkbox (top-left of the Inspector) so it starts
//     DISABLED. The router enables it.
// [ ] Add the screen to GameFlow -> Screen Router -> Screens.
//
// B) CHILDREN, in this order (first = drawn behind)
// [ ] Right-click StickerRemovalScreen -> UI -> Image. Name: Background
//       Anchor stretch/stretch, all offsets 0.
//       Source Image = Assets/Art/LinnenAssets/LinnenBackingBG(all)
//       Untick Raycast Target.
// [ ] Right-click StickerRemovalScreen -> Create Empty. Name: CloseUpArea
//       Anchor stretch/stretch, Left 0, Right 0, Top 0, Bottom 0 (the close-up
//       covers the whole screen; the header and hamburger float over it).
//       Add Component -> Rect Mask 2D (crops the overflow of the Cover fit).
// [ ] Right-click CloseUpArea -> UI -> Image. Name: CloseUp
//       Leave its Rect Transform alone; this script sizes it at runtime.
//       Source Image = Assets/Art/Posters/poster2/close-upForStickerRemoval
//       (only for the Scene view; the stage asset's Close Up Sprite wins at
//       runtime). Untick Raycast Target.
// [ ] Right-click CloseUpArea -> Create Empty. Name: StickerRoot
//       (must be BELOW CloseUp in the Hierarchy so stickers draw on top of it).
//       Leave its Rect Transform alone; this script sizes it to match CloseUp.
// [ ] Right-click StickerRoot -> Create Empty. Name: NextStickerMarker
//       Add Component -> Tutorial Anchor, Anchor Id = sticker.next
//       (no Image: it is invisible and must not block taps).
// [ ] Right-click StickerRemovalScreen -> UI -> Text - TextMeshPro. Name: Header
//       Anchor top-left. Pos X = 114, Pos Y = -90, Width = 184, Height = 30.
//       Alignment Center + Middle, Font Size 18. No Localized Text component
//       (this script writes the stage title).
// [ ] Add the hamburger: the PauseButton object built by the UI agent's
//     instructions (UI/PauseButton.cs), anchor top-left, Pos X = 14,
//     Pos Y = -38, Width = 53, Height = 57, with a Tutorial Anchor pause.button.
//     It must be the LAST child so nothing covers it.
//
// C) WIRE THE INSPECTOR (select StickerRemovalScreen)
// [ ] Restoration Source   <- the GameFlow object (has RestorationController)
// [ ] Poster Stack Root    <- leave EMPTY (important: hides the shared poster)
// [ ] Tool Bar Root        <- leave EMPTY
// [ ] Header Label         <- Header
// [ ] Close Up Area        <- CloseUpArea
// [ ] Close Up Image       <- CloseUp
// [ ] Sticker Root         <- StickerRoot
// [ ] Next Sticker Marker  <- NextStickerMarker
// [ ] Sticker Template     <- leave EMPTY (stickers are created in code), or a
//                             StickerView you styled whose object you keep
//                             UNTICKED (inactive) in the scene; see StickerView.cs
// [ ] Fit Mode = Cover, Complete Beat Seconds = 0.35, Peel Motion = defaults.
// [ ] Preview Stage        <- Assets/Data/Poster2/Stages/02_Stickers (Editor
//                             only: draws the sticker rects as gizmos).
//
// D) SOUND
// [ ] The peel sound needs a row in Assets/Audio/Data/SfxLibrary:
//     Id = StickerPeel, Clip = (stickerPeel)freesound_community-egg-crack4-85848.
//     Run Restorium -> Posters -> Add Sticker Peel Sound To SfxLibrary once, or
//     add the row by hand (Entries -> + , set Id and drag the clip in).
// ---------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Restoration;
    using RestoriumEmporium.Tutorial;

    [DisallowMultipleComponent]
    public class StickerRemovalScreen : RestorationScreenBase
    {
        [Header("Close-up")]
        [Tooltip("The region the close-up is fitted into. Leave empty to use this screen's own rect.")]
        [SerializeField] private RectTransform closeUpArea;

        [Tooltip("Shows the stage's Close Up Sprite. Sized at runtime to keep its aspect.")]
        [SerializeField] private Image closeUpImage;

        [Tooltip("Parent of the stickers. Sized at runtime to exactly cover the close-up.")]
        [SerializeField] private RectTransform stickerRoot;

        [Tooltip("Cover fills the area (crops two edges); Contain shows the whole image.")]
        [SerializeField] private StickerFitMode fitMode = StickerFitMode.Cover;

        [Header("Stickers")]
        [Tooltip("Optional hidden StickerView to clone. Empty = stickers are built in code.")]
        [SerializeField] private StickerView stickerTemplate;

        [Tooltip("Invisible rect carrying the Tutorial Anchor 'sticker.next'. Moved over " +
                 "the first sticker still on the poster.")]
        [SerializeField] private RectTransform nextStickerMarker;

        [Tooltip("How a sticker lifts and falls.")]
        [SerializeField] private StickerPeelMotion peelMotion = StickerPeelMotion.Default;

        [Range(0f, 2f)]
        [Tooltip("Pause after the last sticker has fallen off before the stage completes.")]
        [SerializeField] private float completeBeatSeconds = 0.35f;

#if UNITY_EDITOR
        [Header("Editor only")]
        [Tooltip("Draws this stage's sticker rects as gizmos when the screen is selected.")]
        [SerializeField] private RestorationStageData previewStage;
#endif

        private readonly List<StickerView> _views = new List<StickerView>();
        private readonly StickerPeelTracker _tracker = new StickerPeelTracker();

        private System.Action<StickerView> _onTapped;
        private System.Action<StickerView> _onFallen;

        private RestorationStageData _stage;
        private TutorialAnchor _markerAnchor;
        private IAudioService _audio;
        private Coroutine _completion;
        private int _fallingCount;
        private bool _completionRequested;

        public override GameScreen Screen => GameScreen.StickerRemoval;

        /// <summary>Stickers still on the close-up (0 when no sticker stage is shown).</summary>
        public int RemainingStickers => _stage != null ? _tracker.Remaining : 0;

        private void Awake()
        {
            // Cached once so binding a view never allocates a delegate.
            _onTapped = HandleStickerTapped;
            _onFallen = HandleStickerFallen;

            if (nextStickerMarker != null)
            {
                _markerAnchor = nextStickerMarker.GetComponent<TutorialAnchor>();
            }

            SetMarkerVisible(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_completion != null)
            {
                StopCoroutine(_completion);
                _completion = null;
            }

            // Leaving the screen mid-stage forgets the peels; OnEnable re-spawns
            // every sticker. See KEY DECISIONS.
            ClearStickers();
            _stage = null;
        }

        protected override void ApplyStage(RestorationStageData stage, int index)
        {
            base.ApplyStage(stage, index);

            if (stage == null || stage.kind != StageKind.StickerPeel)
            {
                ClearStickers();
                _stage = null;
                return;
            }

            BuildStage(stage);
        }

        private void OnRectTransformDimensionsChange()
        {
            // Resolution / safe-area change: re-fit, but never re-spawn.
            if (_stage != null && isActiveAndEnabled)
            {
                Layout();
            }
        }

        // ---- Building -----------------------------------------------------------------

        private void BuildStage(RestorationStageData stage)
        {
            if (_completion != null)
            {
                StopCoroutine(_completion);
                _completion = null;
            }

            ClearStickers();

            _stage = stage;
            _fallingCount = 0;
            _completionRequested = false;

            if (closeUpImage != null)
            {
                Sprite closeUp = stage.closeUpSprite != null ? stage.closeUpSprite : stage.fromSprite;
                closeUpImage.sprite = closeUp;
                closeUpImage.preserveAspect = false; // the rect itself is fitted
                closeUpImage.raycastTarget = false;
                closeUpImage.enabled = closeUp != null;

                if (stage.closeUpSprite == null)
                {
                    Debug.LogWarning(
                        $"[StickerRemovalScreen] Stage '{stage.stageId}' has no Close Up Sprite; " +
                        "showing its From Sprite instead. Run Restorium -> Posters -> Create or " +
                        "Update Poster 2 Data, or drag close-upForStickerRemoval into the stage.", this);
                }
            }

            int count = stage.StickerCount;
            _tracker.Reset(count);

            for (int i = 0; i < count; i++)
            {
                StickerDefinition def = stage.stickers[i];
                StickerView view = GetOrCreateView(i);

                if (def == null || def.sprite == null || view == null)
                {
                    // An empty row must not strand the player on this stage.
                    Debug.LogWarning(
                        $"[StickerRemovalScreen] Sticker {i} on stage '{stage.stageId}' has no sprite; " +
                        "it counts as already peeled.", this);
                    _tracker.TryPeel(i, out _);
                    view?.Unbind();
                    continue;
                }

                view.Bind(i, def.sprite, Vector2.zero, Vector2.zero, def.rotation, _onTapped, _onFallen);
            }

            // Views beyond this stage's count stay hidden.
            for (int i = count; i < _views.Count; i++)
            {
                _views[i]?.Unbind();
            }

            Layout();
            UpdateMarker();

            if (_tracker.IsComplete)
            {
                Debug.LogWarning(
                    $"[StickerRemovalScreen] Stage '{stage.stageId}' has no peelable stickers; " +
                    "completing it so the restoration can go on.", this);
                RequestCompletion();
            }
        }

        private StickerView GetOrCreateView(int index)
        {
            while (_views.Count <= index)
            {
                _views.Add(CreateView(_views.Count));
            }

            return _views[index];
        }

        private StickerView CreateView(int index)
        {
            Transform parent = stickerRoot != null ? stickerRoot : transform;

            if (stickerTemplate != null)
            {
                StickerView clone = Instantiate(stickerTemplate, parent, false);
                clone.name = "Sticker" + (index + 1);

                // The template is kept inactive in the scene; a fresh clone is not
                // part of any activation in progress, so activating it is safe.
                if (!clone.gameObject.activeSelf)
                {
                    clone.gameObject.SetActive(true);
                }

                return clone;
            }

            var go = new GameObject("Sticker" + (index + 1), typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);

            Image image = go.AddComponent<Image>();
            image.raycastTarget = true;
            image.preserveAspect = true;

            return go.AddComponent<StickerView>();
        }

        private void ClearStickers()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _views[i]?.Unbind();
            }

            _fallingCount = 0;
            SetMarkerVisible(false);
        }

        // ---- Layout -------------------------------------------------------------------

        /// <summary>Fits the close-up into its area and puts every resting sticker on its spot.</summary>
        private void Layout()
        {
            if (_stage == null)
            {
                return;
            }

            RectTransform area = closeUpArea != null ? closeUpArea : (RectTransform)transform;
            Rect areaRect = area.rect;

            Sprite sprite = closeUpImage != null ? closeUpImage.sprite : null;
            float contentW = sprite != null ? sprite.rect.width : areaRect.width;
            float contentH = sprite != null ? sprite.rect.height : areaRect.height;

            StickerLayoutMath.Fit(areaRect.width, areaRect.height, contentW, contentH, fitMode,
                out float width, out float height);

            Vector2 size = new Vector2(width, height);

            if (closeUpImage != null)
            {
                CentreIn(closeUpImage.rectTransform, size);
            }

            if (stickerRoot != null)
            {
                CentreIn(stickerRoot, size);
            }

            StickerDefinition[] defs = _stage.stickers;
            int count = _stage.StickerCount;

            for (int i = 0; i < count && i < _views.Count; i++)
            {
                StickerView view = _views[i];
                StickerDefinition def = defs[i];

                if (view == null || def == null || view.Index != i)
                {
                    continue;
                }

                StickerRect r = ResolveRect(def, width, height);
                view.Relayout(new Vector2(r.CenterX, r.CenterY), new Vector2(r.Width, r.Height));
            }

            UpdateMarker();
        }

        private static StickerRect ResolveRect(StickerDefinition def, float rootW, float rootH)
        {
            return StickerLayoutMath.NormalizedToLocal(
                def.normalizedCenter.x, def.normalizedCenter.y,
                def.normalizedSize.x, def.normalizedSize.y,
                rootW, rootH);
        }

        private static void CentreIn(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private void UpdateMarker()
        {
            if (nextStickerMarker == null)
            {
                return;
            }

            int next = _stage != null ? _tracker.FirstRemaining() : -1;

            if (next < 0 || stickerRoot == null)
            {
                SetMarkerVisible(false);
                return;
            }

            StickerDefinition def = _stage.stickers[next];
            Rect root = stickerRoot.rect;
            StickerRect r = ResolveRect(def, root.width, root.height);

            nextStickerMarker.anchorMin = new Vector2(0.5f, 0.5f);
            nextStickerMarker.anchorMax = new Vector2(0.5f, 0.5f);
            nextStickerMarker.pivot = new Vector2(0.5f, 0.5f);
            nextStickerMarker.anchoredPosition = new Vector2(r.CenterX, r.CenterY);
            nextStickerMarker.sizeDelta = new Vector2(r.Width, r.Height);
            nextStickerMarker.localRotation = Quaternion.Euler(0f, 0f, def.rotation);

            SetMarkerVisible(true);
        }

        /// <summary>
        /// Registers / unregisters "sticker.next" by toggling the Tutorial Anchor
        /// COMPONENT (its OnEnable/OnDisable do the registry work). The marker's
        /// GameObject is never toggled: this runs from OnEnable/OnDisable, where
        /// changing a child's active state is not allowed.
        /// </summary>
        private void SetMarkerVisible(bool visible)
        {
            if (_markerAnchor != null && _markerAnchor.enabled != visible)
            {
                _markerAnchor.enabled = visible;
            }
        }

        // ---- Taps and completion ------------------------------------------------------

        private void HandleStickerTapped(StickerView view)
        {
            if (view == null || _stage == null || _completionRequested)
            {
                return;
            }

            if (!_tracker.TryPeel(view.Index, out _))
            {
                return;
            }

            ResolveAudio()?.PlaySfx(SfxId.StickerPeel);

            _fallingCount++;
            view.Peel(peelMotion);

            GameSignals.RaiseStickerPeeled(_tracker.Remaining);
            UpdateMarker();

            // When that was the last one (completedNow), completion still waits for
            // it to finish falling: see HandleStickerFallen.
        }

        private void HandleStickerFallen(StickerView view)
        {
            if (_fallingCount > 0)
            {
                _fallingCount--;
            }

            if (_stage != null && _tracker.IsComplete && _fallingCount == 0)
            {
                RequestCompletion();
            }
        }

        private void RequestCompletion()
        {
            if (_completionRequested)
            {
                return;
            }

            _completionRequested = true;

            if (!isActiveAndEnabled)
            {
                CompleteStage(_stage);
                return;
            }

            _completion = StartCoroutine(CompleteAfterBeat(_stage));
        }

        private IEnumerator CompleteAfterBeat(RestorationStageData stage)
        {
            if (completeBeatSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(completeBeatSeconds);
            }

            _completion = null;
            CompleteStage(stage);
        }

        private void CompleteStage(RestorationStageData stage)
        {
            IRestorationRuntime runtime = Runtime;

            if (runtime == null)
            {
                Debug.LogWarning(
                    "[StickerRemovalScreen] Every sticker is off but no Restoration Source is " +
                    "wired, so the stage cannot complete. Drag GameFlow into Restoration Source.", this);
                return;
            }

            // Only complete the stage these stickers belonged to.
            if (stage == null || runtime.CurrentStage != stage)
            {
                return;
            }

            runtime.ForceCompleteCurrentStage();
        }

        private IAudioService ResolveAudio()
        {
            if (_audio == null)
            {
                ServiceLocator.TryGet(out _audio);
            }

            return _audio;
        }

        // ---- Editor preview -----------------------------------------------------------

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            RestorationStageData stage = previewStage != null ? previewStage : _stage;
            if (stage == null || stage.StickerCount == 0)
            {
                return;
            }

            RectTransform area = closeUpArea != null ? closeUpArea : transform as RectTransform;
            if (area == null)
            {
                return;
            }

            Sprite sprite = stage.closeUpSprite;
            Rect areaRect = area.rect;
            float contentW = sprite != null ? sprite.rect.width : areaRect.width;
            float contentH = sprite != null ? sprite.rect.height : areaRect.height;
            StickerLayoutMath.Fit(areaRect.width, areaRect.height, contentW, contentH, fitMode,
                out float width, out float height);

            Vector2 areaCentre = areaRect.center;
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
            DrawLocalRect(area, areaCentre, width, height, 0f);

            Gizmos.color = new Color(1f, 0.3f, 0.6f, 1f);
            for (int i = 0; i < stage.StickerCount; i++)
            {
                StickerDefinition def = stage.stickers[i];
                if (def == null)
                {
                    continue;
                }

                StickerRect r = ResolveRect(def, width, height);
                DrawLocalRect(area, areaCentre + new Vector2(r.CenterX, r.CenterY), r.Width, r.Height, def.rotation);
            }
        }

        private static void DrawLocalRect(RectTransform space, Vector2 centre, float w, float h, float degrees)
        {
            Quaternion rot = Quaternion.Euler(0f, 0f, degrees);
            Vector2 hx = rot * new Vector3(w * 0.5f, 0f, 0f);
            Vector2 hy = rot * new Vector3(0f, h * 0.5f, 0f);

            Vector3 a = space.TransformPoint(centre - hx - hy);
            Vector3 b = space.TransformPoint(centre + hx - hy);
            Vector3 c = space.TransformPoint(centre + hx + hy);
            Vector3 d = space.TransformPoint(centre - hx + hy);

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
#endif
    }
}
