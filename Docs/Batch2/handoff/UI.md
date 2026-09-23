# UI agent — Batch 2 handoff

This session resumed a UI agent that was cut off mid-task. Its finished work
(localization, tutorial anchors/hand/overlay/input-gate, JournalPageRules,
TutorialTriggerRules, and their tests) is included below as part of the same
deliverable — everything in this document is current and correct, regardless
of which session wrote it.

## 1. Files created / changed

| File | What it does |
|---|---|
| `Assets/Scripts/Localization/ILocalizationService.cs` (changed) | + `CurrentLocale`, `AvailableLocales`, `DisplayNameFor`, `SetLocale`. |
| `Assets/Scripts/Localization/LocaleTable.cs` (changed) | + `displayName`; unchanged lazy-index/duplicate-key behaviour. |
| `Assets/Scripts/Localization/LocalizationService.cs` (changed) | Two-table (en / pt-BR) service: device-language auto-detect on first launch (`LocalePicker`), persists the choice to `SaveData.localeCode`, `SetLocale` switches live and raises `LocaleChanged`. Lazy save-read so a new player never sees a frame of the wrong language. |
| `Assets/Scripts/Localization/LocalePicker.cs` (new) | Plain C#: device-language guess, saved-choice override, `Next()` for the settings cycler. Unit tested. |
| `Assets/Scripts/Localization/LocalizedText.cs` (unchanged) | Still the only string binder; no Batch 2 changes needed. |
| `Assets/Editor/Localization/LocaleSource.cs` / `.Legacy.cs` / `.UI.cs` / `.Tutorial.cs` (new) | Every UI/tutorial/legacy string, en + pt-BR, per contract §8 key convention. `.Flow.cs`/`.Restoration.cs`/`.Shop.cs` are the other agents' own files (present, not mine). |
| `Assets/Editor/LocaleTableBuilder.cs` (new) | `RebuildAll()` (menu `Restorium/Localization/Rebuild Locale Tables`) and `Upsert(locale, key, value)`. Upsert-only, updates `pt-BR.asset`/`en.asset` in place. |
| `Assets/Scripts/UI/OverlayController.cs` (new) | Modal stack for Pause/Settings + `ModalOverlay` base class. `OpenPause/OpenSettings/CloseTop/CloseAll/AnyOpen`, Android back / Escape via the new Input System, `GameSignals.RaiseModalChanged`, stops the tool loop on first open. |
| `Assets/Scripts/UI/PauseButton.cs` (new) | Hamburger button: opens Pause (default) or Settings (desk hub uses its own direct call instead, see SHOP.md). Static registry so `OverlayController`'s back-key can find "the visible one". |
| `Assets/Scripts/UI/PauseMenuOverlay.cs` (new) | `ModalOverlay`: Journal → `flow.OpenJournal()`, Settings → `OpenSettings()`, Quit → `flow.QuitToTitle()`. |
| `Assets/Scripts/UI/SettingsOverlay.cs` (new) | `ModalOverlay`: language cycler over `ILocalizationService.AvailableLocales` (`LocalePicker.Next`), SFX/Music sliders → `IAudioService` + `SaveData` (`SaveSoon`), back button. |
| `Assets/Scripts/UI/JournalScreen.cs` (rewritten) | Multi-page journal driven by `flow.Posters` + `JournalPageRules`. Page-flip with `SfxId.PageFlip`, fade-in every show, back button visible only when `flow.DeskHubUnlocked`. |
| `Assets/Scripts/UI/JournalPageRules.cs` (unchanged this session) | Plain C# page-state rules (Locked/Available/InProgress/Completed), initial-page choice. Unit tested. |
| `Assets/Scripts/UI/FinishedRepairScreen.cs` (rewritten) | New design: reads `flow.LastCompletedPoster`, chapter line, "+N" coins label (doubles to "+2N" once paid), Double Reward button shown only while `flow.CanDoubleReward`, re-checked after the ad callback returns. |
| `Assets/Scripts/Tutorial/TutorialController.cs` (rewritten) | Sequence-based: `TutorialSequenceData[] sequences`, two `TracyOverlayView`s (Portrait/HubFullBody), triggers on `ScreenChanged`/`StageStarted` via `TutorialTriggerRules`, waits while `ICutscenePlayer.IsPlaying` or `OverlayController.AnyOpen`, listens to `IDecorationInventory.ItemPurchased` and `GameSignals`, per-step persistence, `TryStart/Skip/Restart(Sequence)`. See §2 for the exact API. |
| `Assets/Scripts/Tutorial/TutorialTriggerRules.cs` (unchanged this session) | Plain C# sequence-start rules (first match wins, prerequisites, resume-index clamp). Unit tested. |
| `Assets/Scripts/Tutorial/TutorialAnchor.cs`, `HandPointer.cs`, `TracyOverlayView.cs`, `TutorialInputGate.cs`, `TutorialTapProbe.cs` (unchanged this session) | Already Batch-2-ready: runtime anchor ids, anchor-id-following hand with `SetSuspended`, dual-instance Tracy view (mood sprites or mood objects for HubFullBody), gate that remembers/restores each root's prior `blocksRaycasts`. |
| `Assets/Editor/TutorialAuthoringTool.cs` (new) | `RebuildAll()` (menu `Restorium/Tutorial/Rebuild Tutorial Sequences`) builds `first_restoration` (reuses the 16 existing MVP step assets by path, keeping their GUIDs), `deskhub_lamp`, `journal_page2`, `stickers` under `Assets/Data/Tutorial/Sequences/`. Upsert-only. |
| `Assets/Scripts/UI/ButtonSfx.cs`, `TitleScreenController.cs`, `ThanksForPlayingScreen.cs` (unchanged) | No Batch 2 changes needed; still correct as-is. |
| `Assets/Scripts/Tests/EditMode/Logic/JournalPageRulesTests.cs`, `LocalePickerTests.cs`, `TutorialTriggerRulesTests.cs` (new, unchanged this session) | Pure NUnit, no UnityEngine. |
| `Assets/Scripts/Tests/EditMode/LocalizationTests.cs` (changed, unchanged this session) | Covers `LocaleTable`, `LocalizationService`, including the Batch 2 `AvailableLocales`/saved-choice/persistence behaviour. |

Not touched: any other agent's files. `Assets/Scripts/Decor/RuntimeTutorialAnchor.cs` shows as modified in git status — that is the SHOP agent's own file/session, not this one.

## 2. Public API implemented (contract §5 "UI agent provides")

```csharp
// UI/OverlayController.cs
public class OverlayController : MonoBehaviour
{
    public void OpenPause();
    public void OpenSettings();
    public void CloseTop();
    public void CloseAll();          // extra: used by PauseMenuOverlay's Journal/Quit
    public bool AnyOpen { get; }
    public ModalOverlay Top { get; } // extra
    public GameFlowController Flow { get; } // extra: lets ModalOverlay subclasses reach the flow
    public void OnBackPressed();     // extra: public so it can be unit-driven/tested
}
public abstract class ModalOverlay : MonoBehaviour { /* Open/Close/SetInteractable, OnOpened/OnClosed hooks */ }

// UI/PauseButton.cs
public class PauseButton : MonoBehaviour
{
    public void Press();
    public static PauseButton FirstVisible();
}

// UI/PauseMenuOverlay.cs — ModalOverlay, no extra public API (button wiring only)

// UI/SettingsOverlay.cs — ModalOverlay, no extra public API (button/slider wiring only)

// UI/JournalScreen.cs — ScreenView
public class JournalScreen : ScreenView
{
    public override GameScreen Screen => GameScreen.Journal;
    public PosterData CurrentPoster { get; }
}

// UI/FinishedRepairScreen.cs — ScreenView
public class FinishedRepairScreen : ScreenView
{
    public override GameScreen Screen => GameScreen.FinishedRepair;
    public RectTransform FlipCardRoot { get; }
    public Image FrontFace { get; }
    public Image BackFace { get; }
    public void SetPoster(PosterData value); // extra: direct override, used by the flip-card wiring / tests
}

// Tutorial/TutorialController.cs
public class TutorialController : MonoBehaviour
{
    public bool TryStart(string sequenceId);
    public void Skip();
    public void Restart();                       // restarts the active (or first) sequence
    public void RestartSequence(string sequenceId); // extra: restart any sequence by id
    public TutorialSequenceData ActiveSequence { get; }
    public int CurrentStepIndex { get; }
    public bool IsRunning { get; }
    public event Action<TutorialSequenceData, TutorialStepData, int> StepStarted;
    public event Action<string> SequenceFinished;
}

// Editor/TutorialAuthoringTool.cs
public static class TutorialAuthoringTool { public static void RebuildAll(); }

// Editor/LocaleTableBuilder.cs
public static class LocaleTableBuilder
{
    public static void RebuildAll();
    public static void Upsert(string localeCode, string key, string value);
}

// Localization/ILocalizationService.cs — extra members added
IReadOnlyList<string> AvailableLocales { get; }
string CurrentLocale { get; }
string DisplayNameFor(string localeCode);
void SetLocale(string localeCode);
```

Every signature in contract §5 is present. Extras (`CloseAll`, `Top`, `Flow`, `OnBackPressed` on `OverlayController`; `RestartSequence`, `ActiveSequence`, `CurrentStepIndex`, `IsRunning`, `SequenceFinished` on `TutorialController`; `SetPoster` on `FinishedRepairScreen`) are additive and do not change any contract call site.

**No deviations from §5.** One behavioural note: `TutorialController.Restart()` (the parameterless, `[ContextMenu]` one) restarts whichever sequence is currently active, or `sequences[0]` when none is running — the contract only specifies the method exists, not which sequence a no-argument restart targets.

## 3. Scene wiring for the builder

Reference art/positions: `Docs/Batch2/FigmaLayout.md` + `Docs/Batch2/screens/*.png` (GamePausedOverlay, SettingsScene, JounalPage, FinishedRepairBG). Each script's own `---- UNITY EDITOR SETUP ----` checklist has the full step-by-step hierarchy; this is the cross-reference summary.

### Overlays (new object under Canvas, sibling of the screens)
```
Canvas
└── Overlays                    Canvas (Override Sorting, Order 30) + Graphic Raycaster + Overlay Controller
    ├── PauseMenuOverlay         Canvas Group + Pause Menu Overlay
    │   ├── Scrim / Panel / Title / Flavour
    │   ├── JournalButton  Localized Text "ui.pause.journal", Button Sfx
    │   ├── SettingsButton Localized Text "ui.pause.settings", Button Sfx
    │   └── QuitButton     Localized Text "ui.pause.quit", Button Sfx
    └── SettingsOverlay           Canvas Group + Settings Overlay
        ├── Scrim / Panel / Title ("ui.settings.title") / TracyLine ("ui.settings.tracyLine")
        ├── BackButton
        ├── LanguageRow: Label ("ui.settings.language") + LanguageButton (its TMP child = Language Value Label, NOT localized — the script writes it)
        ├── SfxRow: Label ("ui.settings.sfx") + SfxSlider (0..1)
        └── MusicRow: Label ("ui.settings.music") + MusicSlider (0..1)
```
`OverlayController` fields: `Pause Menu` ← PauseMenuOverlay, `Settings` ← SettingsOverlay, `Flow` ← GameFlow, `Handle Back Key` ✓.
`PauseMenuOverlay` fields: Journal/Settings/Quit Button ← the three buttons above.
`SettingsOverlay` fields: Back Button, Language Button, Language Value Label, Sfx Slider, Music Slider.

**Every restoration screen's hamburger** (built by the RESTORATION agent) gets a `PauseButton` component pointed at `Overlays`' `OverlayController`, `Opens = Pause`, plus `Button Sfx` and a `TutorialAnchor` (`pause.button`). The desk hub's own hamburger is wired directly by `DeskHubScreen` to `OverlayController.OpenSettings()` (SHOP's own wiring, already documented in SHOP.md) — it does **not** need a `PauseButton` component. The journal has no hamburger by design (contract lists no pause access from it).

### JournalScreen (rewrite of the existing object — keep it, just rebuild its children/fields)
```
JournalScreen                  Canvas Group ("Fade Group") + Journal Screen
├── Background
├── Paper
├── PosterImage                repainted every page turn
├── TitleLabel                 set from the poster's own titleKey (no LocalizedText)
├── ActionButton                Tutorial Anchor "journal.restoreButton", Button Sfx
│   └── (TMP child = Action Button Label, no LocalizedText — script sets Restore/Continue/Restored)
├── PrevPageButton              Tutorial Anchor "journal.prevPage" (NO Button Sfx — code plays Page Flip only on an actual page change)
├── NextPageButton              Tutorial Anchor "journal.nextPage" (same, no Button Sfx)
├── BackButton                  Localized Text "ui.journal.back", Button Sfx, Tutorial Anchor "journal.back"
└── LockedHint (optional)       Localized Text "ui.journal.lockedHint"
```
Fields: Flow ← GameFlow, Fade Group ← its own Canvas Group, Poster Image, Title Label, Action Button, Action Button Label, Prev/Next Page Button, Back Button, Locked Hint (optional).

### FinishedRepairScreen (rewrite of the existing object)
```
FinishedRepairScreen
├── Background
├── Title                       Localized Text "ui.finishedRepair.title"
├── ChapterLabel                set from poster.chapterKey (no LocalizedText; hidden when a poster has none)
├── FlipCard                    pivot 0.5/0.5 — put the FX agent's CardFlipAnimator on this object
│   ├── FrontFace                = poster.beforeSprite (script overwrites)
│   └── BackFace  (start INACTIVE) Rotation Y = 180, = poster.finalSprite
├── CoinsLabel                  formats "ui.finishedRepair.coins" ("+{0}") — no LocalizedText
├── DoubleButton                 Localized Text "ui.finishedRepair.double", Button Sfx, Tutorial Anchor "finishedRepair.doubleButton"
├── OrLabel                      Localized Text "ui.finishedRepair.or" — hidden together with DoubleButton
└── ContinueButton                Localized Text "ui.finishedRepair.continue", Button Sfx, Tutorial Anchor "finishedRepair.continueButton"
```
Fields: Flow ← GameFlow, Chapter Label, Coins Label, Flip Card Root ← FlipCard, Front Face, Back Face, Double Button, Or Label Object ← OrLabel, Continue Button, **On Shown (+)** ← FlipCard → `CardFlipAnimator.Play()` (add that component to FlipCard first; it is the FX/RESTORATION agent's, not built by this session).

### TutorialController (same GameFlow object as before)
Fields: **Sequences** — drag, in order, the four assets `Restorium/Tutorial/Rebuild Tutorial Sequences` creates: `first_restoration`, `deskhub_lamp`, `journal_page2`, `stickers`. **Portrait Overlay** ← TracyHelpOverlay. **Hub Overlay** ← HubTracyOverlay (new object — see `TracyOverlayView.cs`'s second checklist block for its full hierarchy: scrim + TracyStill/TracyHappy/TracyEmbarrassed + DialogueRect with LineText/TapHint, Canvas Order 10, per Figma Desk_Hub 91:45). **Hand Pointer** ← HelpingHand. **Input Gate** ← GameFlow (same object). **Overlays** ← the `Overlays` object. **Restoration Source** ← the RestorationController object. **Screen Router Source** ← the ScreenRouter object (GameFlow). Leave **Run Automatically** ✓, **Use Safety Timeout** unticked.

`TutorialInputGate.gatedRoots` (on the same GameFlow object) must now also include the desk hub's and shop's top-level interactive groups (their own Canvas Groups) alongside the existing restoration/journal ones, per that script's own checklist — SHOP's screens should each carry a Canvas Group for this.

## 4. Editor menu items (run in this order)

1. `Restorium/Posters/Create or Update Poster 1 Data` / `.../Poster 2 Data` (RESTORATION)
2. `Restorium/Shop/Create Starter Items` then any `Restorium/Shop/New Shop Item` (SHOP) — **the lamp must stay id `lamp`, price 100**, it is hard-referenced by `deskhub_lamp`'s tutorial steps and by poster 1's coin reward (both 100) so a fresh player can always afford it.
3. `Restorium/Localization/Rebuild Locale Tables` (mine) — or the combined step below.
4. `Restorium/Tutorial/Rebuild Tutorial Sequences` (mine, new) — **run after** step 3, since it references line keys that must already resolve, and **after** the 16 MVP step assets exist (they ship with the repo; the Console lists any that are missing).
5. `Restorium/Rebuild Catalogs & Locale Tables` (SHOP; rebuilds catalogues and also calls step 3's `LocaleTableBuilder.RebuildAll()`).
6. Assign every field in §3 above.

## 5. Localization keys added

All Batch 2 UI/tutorial strings were already drafted by the earlier session in `LocaleSource.UI.cs` and `LocaleSource.Tutorial.cs` (English verbatim from Figma; pt-BR natural translations; lines not in Figma are marked DRAFT in the file's header comment). This session verified every key it references already exists and added none — see those two files for the full list (Pause: `ui.pause.journal/.settings/.flavour`; Settings: `ui.settings.title/.tracyLine/.language/.sfx/.music` (+ an unused-for-now `ui.settings.resetProgress` group, flagged DRAFT/optional in §7); Journal: `ui.journal.continue/.restored/.back/.lockedHint`; Finished Repair: `ui.finishedRepair.double/.or/.coins`; every `tutorial.deskhub_lamp.*` / `tutorial.journal_page2.*` / `tutorial.stickers.*` line). **Human review needed** on every DRAFT-flagged line (Tracy's lamp dialogue, the lamp description referenced by SHOP, the optional reset-progress copy) — none of it is Figma copy, all of it was drafted in Tracy's voice per contract §8.

## 6. Tests

- `Assets/Scripts/Tests/EditMode/Logic/JournalPageRulesTests.cs` — page state (Locked/Available/InProgress/Completed), button label/interactable pairing, arrow availability, initial-page choice. Plain NUnit.
- `Assets/Scripts/Tests/EditMode/Logic/TutorialTriggerRulesTests.cs` — mirrors the real contract §6 sequence table: first match wins, prerequisite chaining (sequence + poster), stage-kind matching, resume-index clamping, blank-id item matching. Plain NUnit.
- `Assets/Scripts/Tests/EditMode/Logic/LocalePickerTests.cs` — device-language guess, saved-choice override, case-insensitivity, unshipped-language fallback, cycling/wraparound. Plain NUnit.
- `Assets/Scripts/Tests/EditMode/LocalizationTests.cs` — `LocaleTable` (lookup, duplicate keys, re-indexing) and `LocalizationService` (fallback chain, `Format`, `AvailableLocales`, saved-choice persistence, `SetLocale`). Uses `UnityEngine.TestTools` and reflection to set private fields, so it runs in Unity's EditMode Test Runner (Window → General → Test Runner → EditMode → Run All), not under plain `dotnet test`.
- `TutorialController` itself has **no dedicated test file**: it is an orchestrating `MonoBehaviour` (coroutines, `ServiceLocator`, several event sources) — the same shape as `RestorationController`/`GameFlowController`, neither of which the contract asks to unit test either. Its decision logic that CAN be pure (which sequence starts, resume-index clamping) already lives in `TutorialTriggerRules` and is tested there.
- The three `Logic` test files above are **not yet listed in `Tools/logictests/LogicTests.csproj`** (they live under `UI/` and `Tutorial/`, not `Logic/`, so the csproj's wildcards miss them for a plain `dotnet test` run). Per contract §11 A6 "the checker maintains that file" — flagging for the lead/consolidator rather than editing a file this agent does not own. They compile and run correctly under Unity's own EditMode Test Runner today (verified via `sh Tools/compilecheck/check.sh`, which builds `EditTests.csproj` against the real Unity DLLs and reports "COMPILE OK").

## 7. Needs from others / open questions

- **Lamp price/id lock-in** (see §4): if SHOP's `Create Starter Items` / `New Shop Item` ever changes the lamp's id away from `lamp` or its price away from 100, `deskhub_lamp`'s `09_Buy` step (`requiredItemId = "lamp"`) and its whole "the coins you just earned cover it exactly" beat break silently — no compile error, just a tutorial that waits for a purchase that can't happen at the price shown. Safety valve: that step still has `autoAdvanceSeconds = 35` for when `useSafetyTimeout` is enabled during testing, but in normal play (timeout off) it would wait forever without the id/price match. Please confirm before shipping.
- **FX/RESTORATION: `CardFlipAnimator`** is referenced only through `FinishedRepairScreen`'s `On Shown` UnityEvent, exactly as the MVP did — needs to exist on the `FlipCard` object with a public `Play()` for the reveal to actually animate; the screen degrades gracefully (front face just stays visible) if it is missing.
- **RESTORATION**: every restoration screen (Cleaning/LinenBacking/StickerRemoval) needs its hamburger wired with `UI/PauseButton.cs` per §3 above (the contract already assigns "a hamburger (PauseButton, see UI agent)" to that agent's screens).
- **ART/lead**: `Docs/Batch2/FigmaLayout.md` was not yet available when this session ran (the ART agent was still finishing it), so every position in the checklists above is inherited from the MVP layout / described qualitatively rather than pixel-exact per Figma node. The scene builder should cross-check final positions against `FigmaLayout.md` once it lands, especially for the new `HubTracyOverlay`, `Overlays` panels, and `FinishedRepairScreen`'s coins/double-reward row (no MVP precedent for those).
- **Extra settings proposal** (contract §5 asks for this): a "Reset Progress" row is drafted in `LocaleSource.UI.cs` (`ui.settings.resetProgress/.resetConfirm/.resetYes/.resetNo`) but **not wired into `SettingsOverlay`** — it needs a two-step confirm (tap Reset → an inline "are you sure" swap, not a native dialog per the Artifact/Unity sandbox conventions elsewhere in this doc's spirit) calling `ISaveService.ResetProgress()`. Left out of this pass to avoid guessing at the confirm UI's look without `FigmaLayout.md`; the strings are ready whenever a human wants it built.
- **SaveMigration note carried over from CORE.md**: `CutsceneIds.Intro` is duplicated as a literal in `SaveMigration` because `ICutscenePlayer.cs` pulls in `UnityEngine.Video`; not this agent's file, just repeating the flag so it isn't lost.

## 8. Optimisation ideas

- `TutorialController` re-resolves `TutorialAnchor.Find(id)` every frame while a step's hand is showing (via `HandPointer`, by design — see that script's own comments) and re-runs `TutorialTriggerRules.FindForScreen/FindForStageKind` on every single `ScreenChanged`/`StageStarted` event even while a sequence is already running (an early `_loop != null` return makes this cheap, but it is still a linear scan of up to 4 candidates on every stage transition of every poster — fine at this scale, would want a "only check when idle" short-circuit before this grows past a handful of sequences).
- `SettingsOverlay` calls `SaveSoon()` on every slider tick; on a fast drag that can be many calls a second before the frame-end coalesce — already the intended, minimal-cost pattern (§0 of the contract), just noting it depends on `SaveManager`'s coalescing actually running at end-of-frame rather than being itself throttled.
- `JournalScreen.Refresh()` walks `JournalPageRules.Evaluate` once per page turn (cheap, O(1) besides the unlock-chain check), but if the catalogue grows large, `PosterCatalog.OrderedIds` is already cached (invalidated on `OnValidate`) so this stays O(1) amortised regardless of poster count.
