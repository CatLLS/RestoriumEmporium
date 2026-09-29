// ============================================================
// JournalScreen — the poster picker: one page per poster (Figma JounalPage 173:164).
// WHAT & WHY: The player's home base for choosing what to restore. Batch 2 turns
//   the MVP's single hard-coded page into one page per poster in
//   GameFlowController.Posters, with Locked / Available / InProgress / Completed
//   states decided by the plain-C# JournalPageRules (unit-tested separately) so
//   this script only ever asks "what should THIS page look like" and draws it.
// KEY DECISIONS:
//   - The page index is local UI state, not something JournalPageRules or the
//     flow tracks. JournalPageRules.InitialPageIndex decides where to OPEN
//     (the poster on the bench, else the one just finished, else the first with
//     work left), and after that arrows just move the index by one, clamped.
//   - Raises no navigation events: it calls straight into GameFlowController
//     (StartOrContinue / GoToDeskHub), because those calls already carry the
//     flow's own guards (locked/completed refusal, cutscene busy-guard) and
//     duplicating that logic here would be a second place to get it wrong.
//   - The action button's caption AND whether it can be pressed both come from
//     JournalPageRules for the SAME evaluated state, so the label and the
//     enabled-ness can never disagree (no separate "is it locked" check).
//   - Locked / disabled buttons are dimmed by hand (SetButtonEnabled), matching
//     the MVP's page-arrow code: this project's custom-art buttons do not
//     visibly react to Selectable.interactable on their own.
//   - The poster art shown is: the restored (final) sprite once Completed,
//     otherwise journalThumbnail if the poster has one, otherwise beforeSprite
//     faded in code (PosterData's own contract: "when journalThumbnail is null
//     the journal draws beforeSprite at ~30% alpha" — a new poster then needs
//     no extra art).
//   - Fades in via a CanvasGroup on OnShown, every time, not just after the book
//     video: a cheap, harmless fade covers both the "just watched a cutscene"
//     case and a plain screen change, with no special-casing needed here.
//   - The back-to-workbench button is toggled by GameObject.SetActive rather
//     than Button.interactable: contract §1.8 says it is only VISIBLE once the
//     desk hub is unlocked, not merely disabled before that.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Positions are Rect Transform Pos X / Pos Y, anchor preset TOP-LEFT (Alt+Shift,
// top-left box). Pos Y is NEGATIVE: type -175 where the design says y = 175.
// Full layout in Docs/Batch2/FigmaLayout.md (JounalPage 173:164).
//
// A) THE SCREEN ROOT
// [ ] Right-click Canvas -> Create Empty. Name it exactly: JournalScreen
// [ ] Rect Transform: anchor stretch/stretch, Left/Right/Top/Bottom = 0.
// [ ] Add Component -> Canvas Group. Name it "FadeGroup" wiring below.
// [ ] Add Component -> Journal Screen (this script).
//
// B) CHILDREN, in this order (order = draw order, first is behind)
// [ ] Background        Image, stretch/stretch, Source = Art/journalAssets/bg
// [ ] Paper              Image, per Figma (the open-book page art).
// [ ] PosterImage        Image, centred on the page per Figma. This is what the
//                        script repaints every page turn.
// [ ] TitleLabel         TMP text, per Figma. No LocalizedText: the script sets
//                        it from the poster's own titleKey.
// [ ] ActionButton        Button - TextMeshPro, per Figma ("Restore"/"Continue"/
//                        "Restored" position). Its child TMP is ActionButtonLabel
//                        (no LocalizedText: the script sets the key per state).
//                        Add Button Sfx, Sfx = Button Click.
//                        Add Tutorial Anchor, Anchor Id = journal.restoreButton.
// [ ] PrevPageButton     Button - TextMeshPro, delete its Text (TMP) child (icon
//                        only). Do NOT add Button Sfx here — the script plays
//                        Page Flip only on an actual page change.
//                        Add Tutorial Anchor, Anchor Id = journal.prevPage.
// [ ] NextPageButton     Same as PrevPageButton, Anchor Id = journal.nextPage.
// [ ] BackButton          Button - TextMeshPro (top-left "back to workbench"
//                        arrow). Its label: Localized Text, Key =
//                        ui.journal.back. Add Button Sfx.
//                        Add Tutorial Anchor, Anchor Id = journal.back.
// [ ] LockedHint (optional) TMP text shown only on a Locked page. Localized
//                        Text, Key = ui.journal.lockedHint.
//
// C) WIRE THE INSPECTOR (select JournalScreen and drag these in)
// [ ] Flow              <- the GameFlow object (GameFlowController)
// [ ] Fade Group        <- JournalScreen's own Canvas Group (step A)
// [ ] Poster Image / Title Label / Action Button / Action Button Label
// [ ] Prev Page Button / Next Page Button
// [ ] Back Button
// [ ] Locked Hint (optional)
// ---------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Localization;

    [DisallowMultipleComponent]
    public class JournalScreen : ScreenView
    {
        private static readonly IReadOnlyList<string> EmptyIds = System.Array.Empty<string>();

        [Header("Flow")]
        [Tooltip("GameFlowController on the GameFlow object. Owns the poster list and " +
                 "every navigation decision this screen asks for.")]
        [SerializeField] private GameFlowController flow;

        [Header("Fade in")]
        [SerializeField] private CanvasGroup fadeGroup;
        [Range(0f, 2f)]
        [SerializeField] private float fadeInSeconds = 0.35f;

        [Header("Page content")]
        [SerializeField] private Image posterImage;
        [SerializeField] private TMP_Text titleLabel;

        [Range(0f, 1f)]
        [Tooltip("Alpha for beforeSprite when a poster has no journalThumbnail of its own.")]
        [SerializeField] private float fallbackPreviewAlpha = 0.3f;

        [Header("Action button (Restore / Continue / Restored)")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;

        [Header("Page arrows")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;

        [Range(0f, 1f)]
        [SerializeField] private float disabledButtonAlpha = 0.35f;

        [Header("Back to workbench")]
        [Tooltip("Visible only once flow.DeskHubUnlocked (contract §1.8).")]
        [SerializeField] private Button backButton;

        [Header("Optional")]
        [Tooltip("Shown only while the current page is Locked.")]
        [SerializeField] private GameObject lockedHint;

        private IPosterProgress _progress;
        private ILocalizationService _localization;
        private IAudioService _audio;
        private ISaveService _save;

        private int _pageIndex;
        private Coroutine _fade;

        public override GameScreen Screen => GameScreen.Journal;

        /// <summary>The poster the current page shows, or null when the catalogue is empty.</summary>
        public PosterData CurrentPoster => Poster(_pageIndex);

        private void Awake()
        {
            AddClick(actionButton, OnActionClicked);
            AddClick(prevPageButton, OnPrevPage);
            AddClick(nextPageButton, OnNextPage);
            AddClick(backButton, OnBack);
        }

        private void OnDestroy()
        {
            RemoveClick(actionButton, OnActionClicked);
            RemoveClick(prevPageButton, OnPrevPage);
            RemoveClick(nextPageButton, OnNextPage);
            RemoveClick(backButton, OnBack);
        }

        protected override void OnShown()
        {
            ResolveServices();

            var active = flow != null ? flow.ActivePoster : null;
            var lastCompleted = flow != null ? flow.LastCompletedPoster : null;

            _pageIndex = JournalPageRules.InitialPageIndex(
                OrderedIds(), _progress,
                active != null ? active.posterId : string.Empty,
                lastCompleted != null ? lastCompleted.posterId : string.Empty);

            Refresh();
            FadeIn();
        }

        // ---- Navigation ---------------------------------------------------------------

        private void OnPrevPage()
        {
            if (!JournalPageRules.HasPreviousPage(_pageIndex))
            {
                return;
            }

            _pageIndex--;
            _audio?.PlaySfx(SfxId.PageFlip);
            Refresh();
        }

        private void OnNextPage()
        {
            if (!JournalPageRules.HasNextPage(_pageIndex, PageCount()))
            {
                return;
            }

            _pageIndex++;
            _audio?.PlaySfx(SfxId.PageFlip);
            Refresh();
        }

        private void OnActionClicked()
        {
            var poster = CurrentPoster;

            if (poster == null || flow == null)
            {
                return;
            }

            var state = JournalPageRules.Evaluate(_progress, poster.posterId, OrderedIds());

            if (!JournalPageRules.IsButtonInteractable(state))
            {
                return;
            }

            flow.StartOrContinue(poster);
        }

        private void OnBack()
        {
            flow?.GoToDeskHub();
        }

        // ---- Drawing --------------------------------------------------------------------

        private void Refresh()
        {
            var count = PageCount();
            _pageIndex = JournalPageRules.ClampPage(_pageIndex, count);

            var poster = CurrentPoster;
            var state = poster != null
                ? JournalPageRules.Evaluate(_progress, poster.posterId, OrderedIds())
                : JournalPageState.Locked;

            DrawArt(poster, state);

            if (titleLabel != null)
            {
                titleLabel.text = poster != null ? Localize(poster.titleKey) : string.Empty;
            }

            if (actionButtonLabel != null)
            {
                actionButtonLabel.text = Localize(JournalPageRules.ButtonLabelKey(state));
            }

            SetButtonEnabled(actionButton, poster != null && JournalPageRules.IsButtonInteractable(state));
            SetButtonEnabled(prevPageButton, JournalPageRules.HasPreviousPage(_pageIndex));

            var showNext = FirstPosterTutorialDone();

            if (nextPageButton != null)
            {
                nextPageButton.gameObject.SetActive(showNext);
            }

            SetButtonEnabled(nextPageButton, showNext && JournalPageRules.HasNextPage(_pageIndex, count));

            if (backButton != null)
            {
                backButton.gameObject.SetActive(flow != null && flow.DeskHubUnlocked);
            }

            if (lockedHint != null)
            {
                lockedHint.SetActive(poster != null && state == JournalPageState.Locked);
            }
        }

        private void DrawArt(PosterData poster, JournalPageState state)
        {
            if (posterImage == null)
            {
                return;
            }

            if (poster == null)
            {
                posterImage.enabled = false;
                return;
            }

            var showFinal = JournalPageRules.ShowsFinalArt(state);
            var sprite = showFinal ? poster.finalSprite
                : (poster.journalThumbnail != null ? poster.journalThumbnail : poster.beforeSprite);

            posterImage.sprite = sprite;
            posterImage.enabled = sprite != null;

            var usingFallbackFade = !showFinal && poster.journalThumbnail == null;
            var color = posterImage.color;
            color.a = usingFallbackFade ? fallbackPreviewAlpha : 1f;
            posterImage.color = color;
        }

        private void FadeIn()
        {
            if (fadeGroup == null)
            {
                return;
            }

            if (_fade != null)
            {
                StopCoroutine(_fade);
            }

            fadeGroup.alpha = 0f;
            _fade = StartCoroutine(FadeRoutine());
        }

        private IEnumerator FadeRoutine()
        {
            var t = 0f;

            while (fadeInSeconds > 0f && t < fadeInSeconds)
            {
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Clamp01(t / fadeInSeconds);
                yield return null;
            }

            fadeGroup.alpha = 1f;
            _fade = null;
        }

        // ---- Helpers ----------------------------------------------------------------

        private void ResolveServices()
        {
            if (_progress == null)
            {
                _progress = ServiceLocator.Get<IPosterProgress>();
            }

            if (_localization == null)
            {
                _localization = ServiceLocator.Get<ILocalizationService>();
            }

            if (_audio == null)
            {
                _audio = ServiceLocator.Get<IAudioService>();
            }

            if (_save == null)
            {
                _save = ServiceLocator.Get<ISaveService>();
            }
        }

        // The next-page arrow stays hidden through the first poster's guided tutorial so a
        // first-time player cannot wander off the page it is teaching. Finishing poster 1
        // also unlocks it, so a skipped or reset tutorial can never hide page 2 for good.
        private bool FirstPosterTutorialDone()
        {
            var data = _save != null ? _save.Data : null;

            if (data == null)
            {
                return true;
            }

            if (data.completedTutorialSequences != null &&
                data.completedTutorialSequences.Contains(SaveMigration.FirstRestorationSequence))
            {
                return true;
            }

            var ids = OrderedIds();
            return _progress != null && ids.Count > 0 && _progress.IsCompleted(ids[0]);
        }

        private int PageCount() => flow != null && flow.Posters != null ? flow.Posters.Count : 0;

        private PosterData Poster(int index) =>
            flow != null && flow.Posters != null ? flow.Posters.Get(index) : null;

        private IReadOnlyList<string> OrderedIds() =>
            flow != null && flow.Posters != null ? flow.Posters.OrderedIds : EmptyIds;

        private string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return _localization != null ? _localization.Get(key) : key;
        }

        private void SetButtonEnabled(Button button, bool enabledState)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = enabledState;

            var graphic = button.targetGraphic;

            if (graphic == null)
            {
                return;
            }

            var color = graphic.color;
            color.a = enabledState ? 1f : disabledButtonAlpha;
            graphic.color = color;
        }

        private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}
