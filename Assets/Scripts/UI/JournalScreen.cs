// ============================================================
// JournalScreen — the poster picker: thumbnail, title, and the Restore button.
// WHAT & WHY: The first thing the player sees in the Game scene. It shows the
//   poster waiting to be restored and hands the decision to start back to the
//   GameFlowController, which owns the actual transition.
// KEY DECISIONS:
//   - Raises an event instead of calling the router. A screen that routes is a
//     screen that has to know the whole flow; keeping the decision here and the
//     consequence outside means the journal can be reused for poster #2 without
//     touching it. Both a C# event (for code) and a UnityEvent (for the
//     Inspector) are exposed, because Agent B wires in code and the human wires
//     in the Inspector.
//   - The page arrows are present but disabled, not hidden. The MVP has one
//     poster; deleting the arrows would mean re-doing the layout for v2, and a
//     missing control reads as a bug while a greyed-out one reads as "later".
//   - Title text is resolved here rather than by a LocalizedText binder, because
//     the key lives on the PosterData asset and changes per poster. Static
//     labels (the Restore caption, the page hint) keep their binders.
//   - Refreshes in OnShown rather than Awake. The poster can be swapped between
//     visits, and Awake never runs again after the first Show.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// All positions below are Pos X / Pos Y in the Rect Transform, with the anchor
// preset set to top-left (click the anchor square, hold Alt+Shift, pick the
// TOP-LEFT box). Pos Y values are NEGATIVE because Y grows downward in the
// Figma frame but upward in Unity: type -175 where the design says y = 175.
//
// A) THE CANVAS (do this once for the whole Game scene)
// [ ] GameObject -> UI -> Canvas. Name it exactly: Canvas
// [ ] Canvas component: Render Mode = Screen Space - Camera.
//     Drag the Main Camera into Render Camera. Plane Distance = 100.
//     Sorting Layer = Default, Order in Layer = 0.
// [ ] Canvas Scaler component: UI Scale Mode = Scale With Screen Size,
//     Reference Resolution X = 412, Y = 917, Screen Match Mode = Match Width
//     Or Height, Match = 0.5.
//
// B) THE SCREEN ROOT
// [ ] Right-click Canvas -> Create Empty. Name it exactly: JournalScreen
// [ ] Rect Transform: anchor preset = stretch/stretch (Alt+Shift, bottom-right
//     box), Left/Right/Top/Bottom all 0.
// [ ] Add Component -> Journal Screen (this script).
//
// C) CHILDREN, in this order (order = draw order, first is behind)
// [ ] Right-click JournalScreen -> UI -> Image. Name: Background
//       Anchor preset stretch/stretch, Left/Right/Top/Bottom = 0.
//       Source Image = Assets/Art/journalAssets/bg
// [ ] Right-click JournalScreen -> UI -> Image. Name: Paper
//       Anchor top-left. Pos X = 0, Pos Y = -175, Width = 412, Height = 541.
//       Source Image = Assets/Art/journalAssets/paper 1
// [ ] Right-click JournalScreen -> UI -> Image. Name: PosterThumbnail
//       Anchor top-left. Pos X = 120, Pos Y = -270, Width = 173, Height = 309.
//       Source Image = Assets/Art/journalAssets/
//         posterBeforeDusting(30opacity,beforeRestoring)
//       (This is only the placeholder look; the script overwrites it at runtime
//        from PosterData.journalThumbnail.)
// [ ] Right-click JournalScreen -> UI -> Text - TextMeshPro. Name: PosterTitle
//       If Unity asks to import TMP Essentials, click Import TMP Essentials.
//       Anchor top-left. Pos X = 120, Pos Y = -240, Width = 173, Height = 28.
//       Alignment = Center + Middle. Font Size = 18.
// [ ] Right-click JournalScreen -> UI -> Button - TextMeshPro. Name: RestoreButton
//       Anchor top-left. Pos X = 104, Pos Y = -592, Width = 202, Height = 69.
//       Its Image -> Source Image = Assets/Art/journalAssets/buttonBase
//       Select its child "Text (TMP)": anchor stretch/stretch, all offsets 0,
//       Alignment = Center + Middle, Font Size = 24.
//       Add Component -> Localized Text on that child, Key = ui.journal.restore
//       Add Component -> Button Sfx on RestoreButton, Sfx = Button Click.
// [ ] Right-click JournalScreen -> UI -> Button - TextMeshPro. Name: NextPageButton
//       Anchor top-left. Pos X = 330, Pos Y = -787, Width = 31, Height = 59.
//       Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Page Flip.
// [ ] Right-click JournalScreen -> UI -> Button - TextMeshPro. Name: PrevPageButton
//       Anchor top-left. Pos X = 82, Pos Y = -846, Width = 31, Height = 59.
//       Delete its child Text (TMP). Add Component -> Button Sfx, Sfx = Page Flip.
// [ ] Right-click JournalScreen -> UI -> Text - TextMeshPro. Name: PageHint
//       Anchor top-left. Pos X = 128, Pos Y = -810, Width = 160, Height = 20.
//       Alignment = Center + Middle. Font Size = 12.
//       Add Component -> Localized Text, Key = ui.journal.pageHint
//
// D) WIRE THE INSPECTOR (select JournalScreen and drag these in)
// [ ] Poster            <- Assets/Data/Poster1/Poster01
// [ ] Thumbnail Image   <- the PosterThumbnail child
// [ ] Title Label       <- the PosterTitle child
// [ ] Restore Button    <- the RestoreButton child
// [ ] Prev Page Button  <- the PrevPageButton child
// [ ] Next Page Button  <- the NextPageButton child
// [ ] Leave Disabled Page Arrow Alpha at 0.35.
// [ ] On Restore Requested (+): drag the GameFlow object in and pick the method
//     the GameFlowController exposes for starting the restoration.
// [ ] Add Component -> Tutorial Anchor on RestoreButton,
//     Anchor Id = journal.restoreButton
// ---------------------------------------------------------------

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    public class JournalScreen : ScreenView
    {
        [Header("Content")]
        [Tooltip("The poster this page shows. The MVP has exactly one.")]
        [SerializeField] private PosterData poster;

        [Header("References")]
        [Tooltip("Image that shows PosterData.journalThumbnail.")]
        [SerializeField] private Image thumbnailImage;

        [Tooltip("Label that shows the poster title, resolved from PosterData.titleKey.")]
        [SerializeField] private TMP_Text titleLabel;

        [SerializeField] private Button restoreButton;

        [Header("Page arrows (MVP: present but disabled)")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;

        [Range(0f, 1f)]
        [Tooltip("How faded a disabled page arrow looks. 1 = no fading.")]
        [SerializeField] private float disabledPageArrowAlpha = 0.35f;

        [Header("Events")]
        [Tooltip("Raised when the player taps Restore. The GameFlowController listens.")]
        [SerializeField] private UnityEvent onRestoreRequested = new UnityEvent();

        /// <summary>Code-side twin of onRestoreRequested, for listeners wired in script.</summary>
        public event Action RestoreRequested;

        public override GameScreen Screen => GameScreen.Journal;

        /// <summary>The poster this page is showing. Null until one is assigned.</summary>
        public PosterData Poster => poster;

        private void Awake()
        {
            if (restoreButton != null)
            {
                restoreButton.onClick.AddListener(RaiseRestoreRequested);
            }

            ApplyPageArrowState();
        }

        private void OnDestroy()
        {
            if (restoreButton != null)
            {
                restoreButton.onClick.RemoveListener(RaiseRestoreRequested);
            }
        }

        protected override void OnShown()
        {
            Refresh();
        }

        /// <summary>Swaps the poster shown on this page. Public for the v2 page turn.</summary>
        public void SetPoster(PosterData value)
        {
            poster = value;
            Refresh();
        }

        /// <summary>Re-reads the poster asset and repaints thumbnail and title.</summary>
        public void Refresh()
        {
            if (thumbnailImage != null)
            {
                Sprite thumb = poster != null ? poster.journalThumbnail : null;
                thumbnailImage.sprite = thumb;
                thumbnailImage.enabled = thumb != null;
            }

            if (titleLabel != null)
            {
                titleLabel.text = Localize(poster != null ? poster.titleKey : string.Empty);
            }

            ApplyPageArrowState();
        }

        private void RaiseRestoreRequested()
        {
            onRestoreRequested?.Invoke();
            RestoreRequested?.Invoke();
        }

        /// <summary>
        /// The MVP has a single page, so both arrows are shown greyed out. When
        /// poster #2 arrives this becomes a real range check.
        /// </summary>
        private void ApplyPageArrowState()
        {
            SetArrowEnabled(prevPageButton, false);
            SetArrowEnabled(nextPageButton, false);
        }

        private void SetArrowEnabled(Button arrow, bool enabledState)
        {
            if (arrow == null)
            {
                return;
            }

            arrow.interactable = enabledState;

            // Interactable alone leaves the sprite at full strength on a custom
            // button image, so fade it explicitly to read as "not yet available".
            Graphic graphic = arrow.targetGraphic;

            if (graphic == null)
            {
                return;
            }

            Color color = graphic.color;
            color.a = enabledState ? 1f : disabledPageArrowAlpha;
            graphic.color = color;
        }

        private static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            // Degrades to the raw key when the scene is opened without the
            // Systems object, which is exactly what ILocalizationService promises.
            return ServiceLocator.TryGet(out ILocalizationService loc) ? loc.Get(key) : key;
        }
    }
}
