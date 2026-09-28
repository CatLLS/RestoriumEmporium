// ============================================================
// FinishedRepairScreen — the before/after reveal, the coin payout and Double Reward
// (Figma FinishedRepairBG 342:497).
// WHAT & WHY: The payoff screen after every poster. It shows the linen-framed
//   reveal card (before -> final), the coins just earned, an optional Double
//   Reward (rewarded ad) button, and Continue back to the desk hub. It owns the
//   content; the actual card-flip animation stays a separate component wired
//   through a UnityEvent (see KEY DECISIONS), same as the MVP.
// KEY DECISIONS:
//   - Reads flow.LastCompletedPoster rather than holding its own poster
//     reference: GameFlowController is the only writer of poster progress, so
//     it is also the only correct source for "which poster just finished",
//     including after a relaunch where this screen's own field would be stale.
//   - The flip stays Agent B's CardFlipAnimator, referenced only by NAME through
//     onShown (a UnityEvent wired in the Inspector), never by type. A serialized
//     MonoBehaviour cast to some local interface would compile today and break
//     the moment either side renamed a method; the UnityEvent keeps the two
//     halves independent.
//   - onShown fires after Flip Delay Seconds, once the BEFORE artwork has been
//     on screen long enough to register as a reveal rather than a jump-cut.
//   - The coins label always shows the TOTAL paid on this screen (base, or base
//     x2 once doubled), not just the base reward, so a doubled payout does not
//     read as "+100" twice.
//   - Double Reward / the "or" separator are shown ONLY while
//     flow.CanDoubleReward is true, and re-checked after RequestDoubleReward's
//     callback returns: CORE's own notes call this out explicitly
//     (CanDoubleReward goes false while the ad is in flight AND after a
//     successful double), so refreshing on the callback is not optional polish,
//     it is what stops a second tap from double-paying.
//   - Continue calls flow.GoToDeskHub() directly rather than raising an event:
//     unlike the MVP there is no longer a choice of destination to defer to
//     another owner (Batch 2 always goes to the desk hub from here).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y, anchor preset TOP-LEFT (Alt+Shift,
// top-left box). Pos Y is NEGATIVE: type -174 where the design says y = 174.
// Full layout in Docs/Batch2/FigmaLayout.md (FinishedRepairBG 342:497).
//
// A) THE SCREEN ROOT
// [ ] Right-click Canvas -> Create Empty. Name it exactly: FinishedRepairScreen
// [ ] Rect Transform: anchor stretch/stretch, Left/Right/Top/Bottom = 0.
// [ ] Add Component -> Finished Repair Screen (this script).
//
// B) CHILDREN, in this order (order = draw order, first is behind)
// [ ] Background       Image, stretch/stretch, Source = Art/FinishedRepairBG
// [ ] Title             TMP text. Localized Text, Key = ui.finishedRepair.title
//                       (a steady line: it does not change while the card turns).
// [ ] ChapterLabel      TMP text, per Figma, below the title. No LocalizedText:
//                       the script sets it from the poster's own chapterKey and
//                       hides the object when a poster has none.
// [ ] FlipCard          Create Empty, per Figma size, PIVOT 0.5/0.5 (the card
//                       spins around its own Y axis; an edge pivot swings it off
//                       screen). Put the linen-frame + stars art here or as its
//                       background child, per Figma.
//     - FrontFace       Image, stretch/stretch inside FlipCard.
//                       Source = a poster's beforeSprite placeholder.
//     - BackFace        Image, stretch/stretch inside FlipCard. Rotation Y = 180.
//                       Start INACTIVE (the flip animator turns it on halfway).
// [ ] CoinsLabel        TMP text, near the reveal, per Figma ("+100" style). No
//                       LocalizedText: the script formats ui.finishedRepair.coins.
// [ ] DoubleButton       Button - TextMeshPro. Label: Localized Text,
//                       Key = ui.finishedRepair.double. Add Button Sfx.
//                       Add Tutorial Anchor, Anchor Id = finishedRepair.doubleButton.
// [ ] OrLabel            TMP text between the two buttons. Localized Text,
//                       Key = ui.finishedRepair.or. Hidden together with
//                       DoubleButton — drag the SAME object (or a shared parent
//                       holding both) into "Or Label Object" below, OR its own
//                       object if it should hide independently.
// [ ] ContinueButton     Button - TextMeshPro. Label: Localized Text,
//                       Key = ui.finishedRepair.continue. Add Button Sfx.
//                       Add Tutorial Anchor, Anchor Id = finishedRepair.continueButton.
//
// C) WIRE THE INSPECTOR (select FinishedRepairScreen and drag these in)
// [ ] Flow             <- the GameFlow object (GameFlowController)
// [ ] Chapter Label / Coins Label
// [ ] Flip Card Root   <- the FlipCard child
// [ ] Front Face / Back Face
// [ ] Double Button / Or Label Object / Continue Button
// [ ] On Shown (+): drag the FlipCard object in and pick
//     CardFlipAnimator -> Play() (RESTORATION/FX agent's component; add it to
//     FlipCard first). This is what starts the flip.
// ---------------------------------------------------------------

using System;
using System.Collections;
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
    public class FinishedRepairScreen : ScreenView
    {
        [Header("Flow")]
        [Tooltip("GameFlowController on the GameFlow object. Supplies LastCompletedPoster " +
                 "and CanDoubleReward, and owns Continue / RequestDoubleReward.")]
        [SerializeField] private GameFlowController flow;

        [Header("Content")]
        [Tooltip("Poster.chapterKey. Hidden automatically when a poster has none.")]
        [SerializeField] private TMP_Text chapterLabel;

        [Tooltip("Formats ui.finishedRepair.coins ('+{0}') with the total paid on this screen.")]
        [SerializeField] private TMP_Text coinsLabel;

        [Header("Reveal card")]
        [Tooltip("The card that flips. Put the FX agent's CardFlipAnimator on this object.")]
        [SerializeField] private RectTransform flipCardRoot;
        [SerializeField] private Image frontFace;
        [SerializeField] private Image backFace;

        [Header("Buttons")]
        [SerializeField] private Button continueButton;

        [Tooltip("Shown only while flow.CanDoubleReward is true.")]
        [SerializeField] private Button doubleButton;

        [Tooltip("The 'or' separator between Double Reward and Continue. Shown/hidden " +
                 "together with Double Button.")]
        [SerializeField] private GameObject orLabelObject;

        [Header("Pacing")]
        [Tooltip("Seconds the BEFORE artwork is held before the card turns.")]
        [Range(0f, 5f)]
        [SerializeField] private float flipDelaySeconds = 1.2f;

        [Header("Events")]
        [Tooltip("Raised after this screen's content is set, once Flip Delay Seconds has " +
                 "passed. Wire the flip animation here.")]
        [SerializeField] private UnityEvent onShown = new UnityEvent();

        private PosterData _poster;
        private ILocalizationService _localization;
        private bool _doubling;
        private bool _doubledPaid;

        public override GameScreen Screen => GameScreen.FinishedRepair;

        public RectTransform FlipCardRoot => flipCardRoot;
        public Image FrontFace => frontFace;
        public Image BackFace => backFace;

        private void Awake()
        {
            AddClick(continueButton, OnContinue);
            AddClick(doubleButton, OnDouble);
        }

        private void OnDestroy()
        {
            RemoveClick(continueButton, OnContinue);
            RemoveClick(doubleButton, OnDouble);
        }

        protected override void OnShown()
        {
            ResolveServices();

            _poster = flow != null ? flow.LastCompletedPoster : null;
            _doubling = false;
            _doubledPaid = false;

            ApplyContent();

            if (flipDelaySeconds <= 0f)
            {
                onShown?.Invoke();
                return;
            }

            StartCoroutine(RaiseOnShownAfterDelay());
        }

        private IEnumerator RaiseOnShownAfterDelay()
        {
            yield return new WaitForSecondsRealtime(flipDelaySeconds);

            // The screen can be hidden again inside the delay; firing the flip at a
            // card nobody is looking at would leave it mid-turn on the way back.
            if (IsVisible)
            {
                onShown?.Invoke();
            }
        }

        /// <summary>Swaps the poster this screen reveals. Public for a direct call/test.</summary>
        public void SetPoster(PosterData value)
        {
            _poster = value;
            ApplyContent();
        }

        // ---- Buttons ----------------------------------------------------------------

        private void OnContinue()
        {
            flow?.GoToDeskHub();
        }

        private void OnDouble()
        {
            if (_doubling || flow == null || !flow.CanDoubleReward)
            {
                return;
            }

            _doubling = true;
            SetInteractable(doubleButton, false);

            flow.RequestDoubleReward(OnDoubleRewardFinished);
        }

        private void OnDoubleRewardFinished(bool paid)
        {
            // flow can be torn down (scene change) by the time an ad callback returns.
            if (this == null)
            {
                return;
            }

            _doubling = false;

            if (paid)
            {
                _doubledPaid = true;
                RefreshCoinsLabel();
            }

            RefreshDoubleButton();
        }

        // ---- Drawing --------------------------------------------------------------------

        private void ApplyContent()
        {
            if (frontFace != null && _poster != null && _poster.beforeSprite != null)
            {
                frontFace.sprite = _poster.beforeSprite;
            }

            if (backFace != null && _poster != null && _poster.finalSprite != null)
            {
                backFace.sprite = _poster.finalSprite;
            }

            if (chapterLabel != null)
            {
                var hasChapter = _poster != null && !string.IsNullOrEmpty(_poster.chapterKey);
                chapterLabel.gameObject.SetActive(hasChapter);
                chapterLabel.text = hasChapter ? Localize(_poster.chapterKey) : string.Empty;
            }

            RefreshCoinsLabel();
            RefreshDoubleButton();
        }

        private void RefreshCoinsLabel()
        {
            if (coinsLabel == null || _poster == null)
            {
                return;
            }

            var total = _poster.coinReward * (_doubledPaid ? 2 : 1);
            coinsLabel.text = Format("ui.finishedRepair.coins", total.ToString());
        }

        private void RefreshDoubleButton()
        {
            var canDouble = !_doubling && flow != null && flow.CanDoubleReward;

            if (doubleButton != null)
            {
                doubleButton.gameObject.SetActive(canDouble);
                SetInteractable(doubleButton, canDouble);
            }

            if (orLabelObject != null)
            {
                orLabelObject.SetActive(canDouble);
            }
        }

        // ---- Helpers ----------------------------------------------------------------

        private void ResolveServices()
        {
            if (_localization == null)
            {
                _localization = ServiceLocator.Get<ILocalizationService>();
            }
        }

        private string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return _localization != null ? _localization.Get(key) : key;
        }

        private string Format(string key, string arg)
        {
            return _localization != null ? _localization.Format(key, arg) : key + " " + arg;
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }

        private static void AddClick(Button button, UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveClick(Button button, UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
