// ============================================================
// FinishedRepairScreen — the before/after reveal and the Continue button.
// WHAT & WHY: The payoff screen. It owns the card that flips from the ruined
//   poster to the restored one, and the button that leaves the restoration.
//   It supplies the pieces; it does not animate them.
// KEY DECISIONS:
//   - The flip itself belongs to Agent B CardFlipAnimator, and this screen does
//     NOT hold a reference to it. Instead it raises onShown when the screen
//     appears and the human wires that UnityEvent to the animator Play method in
//     the Inspector. A serialised MonoBehaviour cast to some local interface
//     would compile today and break the moment either side renamed a method;
//     a UnityEvent keeps the two halves genuinely independent and lets the human
//     re-point the animation without a recompile.
//   - Front and back faces are filled from PosterData.beforeSprite and
//     finalSprite, not from the first and last stage sprites. PosterData already
//     promises those two are the authored bookends; reaching into the stage list
//     would break the moment a stage is reordered.
//   - Continue raises an event rather than routing. Whether Continue goes to the
//     journal or to the ThanksForPlaying scene is a flow decision, and the flow
//     controller owns it.
//   - Refresh happens in OnShown, so a second poster shows the right art without
//     any extra call from outside.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y with the anchor preset set to
// TOP-LEFT (click the anchor square, hold Alt+Shift, pick the top-left box).
// Pos Y is NEGATIVE: type -174 where the design says y = 174.
//
// A) THE SCREEN ROOT
// [ ] Right-click Canvas -> Create Empty. Name it exactly: FinishedRepairScreen
// [ ] Rect Transform: anchor stretch/stretch, Left/Right/Top/Bottom = 0.
// [ ] Add Component -> Finished Repair Screen (this script).
// [ ] Start it DISABLED (untick the box at the top-left of the Inspector).
//
// B) CHILDREN, in this order (order = draw order, first is behind)
// [ ] Right-click FinishedRepairScreen -> UI -> Image. Name: Background
//       Anchor stretch/stretch, all offsets 0.
//       Source Image = Assets/Art/FinishedRepairBG
// [ ] Right-click FinishedRepairScreen -> UI -> Text - TextMeshPro. Name: Title
//       Anchor top-left. Pos X = 89, Pos Y = -128, Width = 235, Height = 20.
//       Alignment = Center + Middle. Font Size = 18.
//       Add Component -> Localized Text, Key = ui.finishedRepair.title
// [ ] Right-click FinishedRepairScreen -> Create Empty. Name: FlipCard
//       Anchor top-left. Pos X = 65, Pos Y = -174, Width = 283, Height = 506.
// [ ] Right-click FlipCard -> UI -> Image. Name: FrontFace
//       Anchor stretch/stretch, Left/Right/Top/Bottom = 0.
//       Source Image = Assets/Art/Posters/poster1/posterBeforeDusting
//       (Overwritten at runtime from PosterData.beforeSprite.)
// [ ] Right-click FlipCard -> UI -> Image. Name: BackFace
//       Anchor stretch/stretch, Left/Right/Top/Bottom = 0.
//       Source Image = Assets/Art/Posters/poster1/posterFinal
//       Set its Rect Transform Rotation Y = 180 so it reads correctly once the
//       card has flipped. Untick the checkbox at the top of the Inspector so it
//       starts hidden; the flip animator turns it on halfway through.
// [ ] Right-click FinishedRepairScreen -> UI -> Button - TextMeshPro.
//       Name: ContinueButton
//       Anchor top-left. Pos X = 105, Pos Y = -734, Width = 202, Height = 69.
//       Image -> Source Image = Assets/Art/journalAssets/buttonBase
//       Its child Text (TMP): anchor stretch/stretch, all offsets 0,
//       Alignment = Center + Middle, Font Size = 24.
//       Add Component -> Localized Text on that child,
//       Key = ui.finishedRepair.continue
//       Add Component -> Button Sfx on ContinueButton, Sfx = Button Click.
//       Add Component -> Tutorial Anchor on ContinueButton,
//       Anchor Id = finishedRepair.continueButton
//
// C) WIRE THE INSPECTOR (select FinishedRepairScreen and drag these in)
// [ ] Poster          <- Assets/Data/Poster1/Poster01
// [ ] Flip Card Root  <- the FlipCard child
// [ ] Front Face      <- the FrontFace child
// [ ] Back Face       <- the BackFace child
// [ ] Continue Button <- the ContinueButton child
// [ ] On Shown (+): drag the FlipCard object in and pick
//     CardFlipAnimator -> Play(). THIS is what starts the flip; without it the
//     card just sits there. (Agent B provides CardFlipAnimator; add that
//     component to the FlipCard object first.)
// [ ] On Continue Requested (+): drag the GameFlow object in and pick the method
//     the GameFlowController exposes for leaving the finished repair.
// ---------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    [DisallowMultipleComponent]
    public class FinishedRepairScreen : ScreenView
    {
        [Header("Content")]
        [Tooltip("Supplies the before and after sprites for the flip card.")]
        [SerializeField] private PosterData poster;

        [Header("References")]
        [Tooltip("The card that flips. Put Agent B CardFlipAnimator on this object.")]
        [SerializeField] private RectTransform flipCardRoot;

        [Tooltip("Face shown first: the poster before restoration.")]
        [SerializeField] private Image frontFace;

        [Tooltip("Face revealed by the flip: the restored poster.")]
        [SerializeField] private Image backFace;

        [SerializeField] private Button continueButton;

        [Header("Events")]
        [Tooltip("Raised after this screen becomes visible and its faces are set. " +
                 "Wire the flip animation here.")]
        [SerializeField] private UnityEvent onShown = new UnityEvent();

        [Tooltip("Raised when the player taps Continue. The GameFlowController listens.")]
        [SerializeField] private UnityEvent onContinueRequested = new UnityEvent();

        /// <summary>Code-side twin of onContinueRequested.</summary>
        public event Action ContinueRequested;

        public override GameScreen Screen => GameScreen.FinishedRepair;

        /// <summary>The card object, for whoever animates it.</summary>
        public RectTransform FlipCardRoot => flipCardRoot;

        public Image FrontFace => frontFace;

        public Image BackFace => backFace;

        private void Awake()
        {
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(RaiseContinueRequested);
            }
        }

        private void OnDestroy()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(RaiseContinueRequested);
            }
        }

        protected override void OnShown()
        {
            ApplyFaces();

            // Raised last, so anything listening (the flip) starts from a card
            // that already shows the right art.
            onShown?.Invoke();
        }

        /// <summary>Swaps the poster whose before/after this screen reveals.</summary>
        public void SetPoster(PosterData value)
        {
            poster = value;
            ApplyFaces();
        }

        private void ApplyFaces()
        {
            if (poster == null)
            {
                return;
            }

            if (frontFace != null && poster.beforeSprite != null)
            {
                frontFace.sprite = poster.beforeSprite;
            }

            if (backFace != null && poster.finalSprite != null)
            {
                backFace.sprite = poster.finalSprite;
            }
        }

        private void RaiseContinueRequested()
        {
            onContinueRequested?.Invoke();
            ContinueRequested?.Invoke();
        }
    }
}
