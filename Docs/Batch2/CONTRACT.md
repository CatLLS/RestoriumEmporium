# Batch 2 — Shared Contract (single source of truth for every agent)

Read this whole file before writing code. If something you need is not here, **do not invent it and do not guess what another agent built** — code against this contract only, and list the gap under "Needs from others" in your handoff file. The consistency checker resolves gaps. The lead (not you) commits.

Project: Unity 6000.5.3f1, C#, Android, portrait, fully 2D uGUI, reference resolution **412 × 917**, Canvas = Screen Space – Camera (perspective), TextMeshPro, new Input System (legacy input disabled). Build spec: `RESTORIUM EMPORIUM — Production & Design Plan.md` (§1 engineering principles and §2 comment convention are MANDATORY: every script starts with the WHAT & WHY / KEY DECISIONS block and the `---- UNITY EDITOR SETUP ----` tickable checklist, written for someone who does not know Unity well). Existing setup guide: `SETUP.md`. Match the surrounding code style (look at `Assets/Scripts/Core/ScreenRouter.cs` and `RestorationController.cs`).

## 0. Ground rules

- Work only inside `C:\Games(shared)\RestoriumEmporium\RestoriumEmporium\.claude\worktrees\batch2`. Never run git. Never touch the parent checkout.
- **Compile check:** `sh Tools/compilecheck/check.sh` (≈10 s, run from the worktree root). It compiles Runtime + Editor + EditTests against the real Unity DLLs. Other agents are editing in parallel, so errors in files you do NOT own may appear mid-flight — ignore those; **your own files must be error-free** when you finish. Unity itself cannot be run here (no licence), so no play mode / batch mode.
- Only edit files you own (§9). Files marked LEAD-OWNED (the contract types in §3) may not be changed — if one is wrong, say so in your handoff.
- **Never edit `.unity` / `.prefab` YAML by hand, never create `.meta` files, never generate or placeholder art.** Scenes are built later by the scene-builder agent from your handoff notes. Art comes from `Assets/Art/**` (Figma exports land in `Assets/Art/UI`, `DeskHub`, `Shop`, `Settings`, `FinishedRepair`, `ShopItems/<id>/icon.png|placed.png`, etc. — see `Docs/Batch2/FigmaLayout.md` and screenshots in `Docs/Batch2/screens/` once the art agent finishes; you may also call the read-only Figma tools yourself: file key `liAY3FddBN7jtIHUvEs4Uu`, section `585:133`).
- Pure logic (rules, maths, migration) goes in **plain C# classes with no `UnityEngine` reference** so it can be unit-tested outside Unity. Log through an injected `Action<string>` (GameBootstrap passes `Debug.LogWarning`). Put NUnit tests for them in `Assets/Scripts/Tests/EditMode/Logic/*.cs` using only NUnit + the classes under test (no UnityEngine, no `LogAssert`) — the checker runs these with `dotnet test`.
- Performance hygiene: no per-frame allocations in Update/drag paths, no `Find*`/`GetComponent` per frame, cache references, pool what repeats, unsubscribe events in `OnDisable`/`OnDestroy`.
- Persistence (§4 of the spec): every meaningful change saves (`Save()` for milestones, `SaveSoon()` for high-frequency). SaveManager already flushes on pause/focus-loss/quit.

## 1. Target flow (what the player experiences)

1. **First launch:** Title → New Game → Game scene → **`tracysc1` plays (forced, not skippable)** → mark `CutsceneIds.Intro` seen → Journal (tutorial `first_restoration` starts: welcome, hand on Restore).
2. Journal → Restore → poster 1 stages (Cleaning, Linen Front/Back/Final) as today, guided by `first_restoration`.
3. Poster 1's last stage completes → progress marked completed → base coin reward paid (100) → **`tracysc2` plays (forced)** once → **Finished Repair** (new design: +100 coins, "Double Reward" (ad) button, "or", Continue).
4. Continue → **DeskHub** (not Thanks For Playing). Tutorial `deskhub_lamp`: Tracy (full-body) explains she needs a new lamp for her desk — maybe the poor lighting is why she didn't notice the last poster was a fake; hand points at the **Shop** button → Shop: hand on the **Lamp** card (100 coins) → Preview mode: "drag it where you want, then buy" → hand on **Buy Item** → lamp placed on the desk → Tracy: tap the book to open the journal (hand on book), mention edit mode.
5. DeskHub book → **`openBookTransition` video** plays → Journal fades in. Tutorial `journal_page2`: hand on the right arrow → page flips → poster 2 page (preview = poster 2's before sprite at low opacity) → Restore.
6. Poster 2 stages: Dust → **Sticker removal** (new, `StageKind.StickerPeel` on `GameScreen.StickerRemoval`: close-up image, tap each sticker → stickerPeel SFX, the sticker falls off screen; all gone → stage completes; tutorial `stickers` explains once) → Wash → Deacidify → Squeegee → Adhesive → Mend → Finished Repair → DeskHub.
7. Anywhere during restoration: **hamburger** (top-left) → Game Paused overlay: Journal / Settings / Quit (+ Android back or tapping the hamburger again resumes). Journal from pause keeps the restoration saved; the journal then shows **Continue** instead of Restore on that poster.
8. Journal pages: one per poster in `PosterCatalog` order. Locked posters (previous not completed) → Restore **disabled**. In progress → "Continue". Completed → finished sprite, button shows "Restored" and is disabled. "Go back to workbench" button (top-left) → DeskHub, visible only once the desk hub is unlocked (`IPosterProgress.AnyCompleted`).
9. DeskHub: hamburger → Settings overlay; coins label; Shop button; **Edit mode** button (select an owned item → drag → Place Item / Undo; "Back to workshop" leaves edit mode); book → journal.
10. Shop: Décor tab (active) / Misc. tab (empty this build), coin balance, "Remove ads" and "Buy More Coins" buttons are **visible but disabled** this build (IAP/ads come next build), item grid, "Go back to workbench". Tapping an item you don't own → DeskHub **Preview mode** with that item (Buy Item / Give up). Owned items show as owned and cannot be re-bought.
11. Relaunch: restoration in progress → resume that stage. Else desk hub unlocked → DeskHub. Else → Journal. (Intro video if never seen.)

## 2. Screens and overlays

`GameScreen` (Core/GameEnums.cs): Journal=1, Cleaning=2, LinenBackingFront=3, LinenBackingBack=4, LinenBackingFinal=5, FinishedRepair=6, **DeskHub=7, Shop=8, StickerRemoval=9**. All live in `Game.unity` under the Canvas, routed by the existing `ScreenRouter` on `GameFlow`.
Edit mode and Preview mode are **modes of DeskHubScreen**, not screens. **Pause** and **Settings** are **modal overlays** (managed by `OverlayController`), not screens. The **CutscenePlayer** is a full-screen overlay above everything (except nothing — it is topmost).

Scene object names the builder will create (reference them in your handoff): `DeskHubScreen`, `ShopScreen`, `StickerRemovalScreen`, `Overlays/PauseMenuOverlay`, `Overlays/SettingsOverlay`, `CutscenePlayer`, `HubTracyOverlay` (the full-body Tracy dialogue view).

## 3. LEAD-OWNED shared types (already in the code — read them, do not change them)

- `Core/GameEnums.cs` — new: `GameScreen.DeskHub/Shop/StickerRemoval`, `StageKind {Scrub, StickerPeel}`, `TracyPresentation {Portrait, HubFullBody}`, `ShopCategory {Decor, Misc}`, `SfxId.StickerPeel=20, CoinsGained=21, Purchase=22, ItemPlaced=23`.
- `Core/SaveData.cs` — **version 2**: `localeCode` ("" = auto), volumes, `lastScreen`, `hasPlayedBefore`, `seenCutscenes`, `activePosterId`, `posters: List<PosterProgressEntry{posterId, stageIndex, completed, rewardClaimed, rewardDoubled}>`, `coins`, `decorations: List<OwnedDecoration{itemId, x, y}>` (normalised 0..1 room coords, centre, (0,0) bottom-left), `completedTutorialSequences`, `activeTutorialSequence`, `activeTutorialStepIndex`, `EnsureCollections()`, `FindPoster()`, `FindDecoration()`. The v1 fields (`tutorialCompleted, tutorialStepIndex, currentPosterId, currentStageIndex, posterCompleted`) are `[Obsolete]` — **only SaveMigration may read them** (`#pragma warning disable CS0618` there only). `IsRestorationInProgress` no longer exists.
- `Core/IPosterProgress.cs`, `Economy/IWallet.cs`, `Economy/IRewardedAd.cs`, `Economy/IDecorationInventory.cs` (+ `PurchaseResult`), `Cinematics/ICutscenePlayer.cs` (+ `CutsceneIds.Intro`, `CutsceneIds.PosterComplete(id)`), `Core/GameSignals.cs` (static notifications: `StickerPeeled(int remaining)`, `PreviewOpened(string itemId)`, `EditModeChanged(bool)`, `ModalChanged(bool)`).
- `Restoration/IRestorationRuntime.cs` — now also has `void ForceCompleteCurrentStage()` (RestorationController already implements it publicly).
- `Data/ShopItemData.cs`, `Data/ShopCatalog.cs`, `Data/PosterCatalog.cs`, `Data/TutorialSequenceData.cs` (+ `TutorialTrigger {ScreenEntered, StageKindStarted, Manual}`).
- `Data/PosterData.cs` — new fields `journalOrder`, `chapterKey`, `completionCutscene` (VideoClip), `coinReward` (default 100). `journalThumbnail` optional: when null the journal draws `beforeSprite` at ~30 % alpha.
- `Data/RestorationStageData.cs` — new `kind`, `closeUpSprite`, `stickers: StickerDefinition[] {sprite, normalizedCenter, normalizedSize, rotation}`, `StickerCount`.
- `Data/TutorialStepData.cs` — new `presentation` (TracyPresentation), `requiredItemId`, `TutorialAdvance.ItemPurchased=6, StickerPeeled=7, PreviewOpened=8`.
- `Assets/Editor/Localization/LocaleSource.cs` — `LocaleEntry(key, ptBR, en)`, `LocaleSource.All()`, helper `E(key, pt, en)`; one partial file per owner: `LocaleSource.Legacy/Flow/Restoration/Shop/UI/Tutorial.cs` (fill **only your own**).

## 4. Services (process lifetime, on the `Systems` object from the Title scene)

GameBootstrap (owned by CORE agent) creates and registers in `ServiceLocator`, in this order, in Awake:
`ISaveService` (existing SaveManager) → `ILocalizationService` (existing) → `IAudioService` (existing) → `IPosterProgress` = `new PosterProgressService(save, warn)` → `IWallet` = `new PlayerWallet(save, warn)` → `IDecorationInventory` = `new DecorationInventory(save, wallet, warn)` → `IRewardedAd` = `SimulatedRewardedAd` (added with `AddComponent` at runtime if absent; always ready + earns after ~1.5 s in Editor/Development builds, never ready in release). No Inspector wiring needed for the new ones. GameBootstrap also calls `GameSignals.Clear()` on every `SceneManager.sceneLoaded`.
Scene-lifetime service: `ICutscenePlayer` — the Game scene's `CutscenePlayer` registers itself in `OnEnable`/`Awake` and unregisters in `OnDestroy`.
Get services with `ServiceLocator.Get<T>()` in `Start` (never Awake), and tolerate null (Game scene opened directly in the Editor) with a warning, like existing code.

## 5. Cross-agent public APIs (exact signatures — implement yours, call others' as written)

### CORE agent provides
- `Core/PosterProgressService : IPosterProgress` (plain C#).
- `Economy/PlayerWallet : IWallet` (plain C#). `Economy/DecorationInventory : IDecorationInventory` (plain C#, spends via IWallet, one Save per purchase).
- `Economy/SimulatedRewardedAd : MonoBehaviour, IRewardedAd`.
- `Core/SaveMigration` (plain C#): `public static bool MigrateInPlace(SaveData data, Action<string> warn)` — v1→v2: `currentPosterId`/`currentStageIndex`/`posterCompleted` → a `posters` entry (+`activePosterId` if in progress; if completed: `completed=true, rewardClaimed=true` and add that poster's reward (100) to coins since v1 never paid it); `tutorialCompleted` → add `"first_restoration"` to completed sequences, else `activeTutorialSequence="first_restoration"`, `activeTutorialStepIndex=tutorialStepIndex`; `seenCutscenes` gets `"intro"` when `hasPlayedBefore` (v1 players already started). SaveManager calls it after load, then `EnsureCollections()`.
- `Cinematics/CutscenePlayer : MonoBehaviour, ICutscenePlayer` — full-screen RawImage + VideoPlayer (RenderTexture created at runtime at the clip's size, released after), letterboxed with AspectRatioFitter (Envelope — fill the screen), CanvasGroup fade in/out ~0.25 s, blocks raycasts while playing, pauses/ducks music via IAudioService while playing, calls onFinished exactly once (end, skip tap if skippable, error, or a timeout of clip length + 3 s). Fields: `RawImage videoSurface`, `VideoPlayer videoPlayer`, `CanvasGroup group`, `AspectRatioFitter fitter`, `TMP_Text skipHint` (optional).
- `Core/GameFlowController` (rewrite of the existing one; keeps its GameObject/GUID). Fields: `ScreenRouter router`, `MonoBehaviour restorationSource`, `PosterCatalog posters`, `MonoBehaviour cutsceneSource` (cast to ICutscenePlayer; falls back to ServiceLocator), `VideoClip introCutscene` (tracysc1), `VideoClip bookOpenCutscene` (openBookTransition). Public API used by UI:
  - `PosterCatalog Posters { get; }`
  - `PosterData ActivePoster { get; }` — poster on the bench (null if none)
  - `PosterData LastCompletedPoster { get; }` — what Finished Repair shows
  - `bool DeskHubUnlocked { get; }` — = IPosterProgress.AnyCompleted
  - `event Action<GameScreen> FlowScreenRequested` — optional, for listeners
  - `void StartOrContinue(PosterData poster)` — Journal's Restore/Continue button
  - `void OpenJournal()` — from Pause (restoration stays saved; tool loop stopped)
  - `void OpenJournalFromDesk()` — plays bookOpenCutscene, then `router.GoNow(Journal)`
  - `void GoToDeskHub()`, `void GoToShop()`
  - `void QuitToTitle()` — saves, `SceneLoader.LoadTitle()`
  - `bool CanDoubleReward { get; }` — last completed poster not yet doubled AND IRewardedAd.IsReady
  - `void RequestDoubleReward(Action<bool> onDone)` — shows the ad; on earned: wallet.Add(coinReward), TryMarkRewardDoubled; onDone(true/false)
  - Rule kept from the MVP: **GameFlowController never touches UI components**; screens hold a reference to it and call in / read from it. **Only GameFlowController writes poster progress** (through IPosterProgress). RestorationController no longer touches the save.

### RESTORATION agent provides
- `RestorationController` without save I/O; supports `StageKind.StickerPeel` (painter disabled, no tool required, no coverage completion — completion only via `ForceCompleteCurrentStage()`).
- `UI/StickerRemovalScreen : RestorationScreenBase` (or ScreenView — agent's call) serving `GameScreen.StickerRemoval`: close-up `Image`, a `RectTransform stickerRoot` sized to the close-up, spawns one `Restoration/StickerView` per `StickerDefinition` (pooled/reused), tap → `IAudioService.PlaySfx(SfxId.StickerPeel)` + peel-and-fall animation (unscaled time, then disable) + `GameSignals.RaiseStickerPeeled(remaining)`; last one → short beat → `runtime.ForceCompleteCurrentStage()`. Keeps a `TutorialAnchor` with id `sticker.next` on the first un-peeled sticker. Header label (stage titleKey) and a hamburger (PauseButton, see UI agent).
- Editor: `PosterAuthoringTool` generalised — menu `Restorium/Posters/Create or Update Poster 1 Data` and `.../Poster 2 Data` (shared recipe code, re-runnable, updates in place). Poster 2: `Assets/Data/Poster2/Poster02.asset` + `Stages/` with Dust (BeforeDusting→NoDust), Stickers (StickerPeel, close-up + sticker1/2), Wash (NoSticker→YellowWet), Deacidify (YellowWet→WhiteWet), Squeegee, Adhesive (roller on back), Mend (Dry→Final), `journalOrder=2`, `coinReward` 100, no completion cutscene. Poster 1: `journalOrder=1`, `coinReward=100`, `completionCutscene = Assets/videos/tracysc2.mp4`. It must NOT create locale tables or tutorial steps any more (those moved to the UI agent's tools); at the end it calls `CatalogBuilder.RebuildAll()` (SHOP agent's).
- Keep/extend `Restorium/Fix Art Import Settings (Sprites)` to cover `Art/Posters/**`, ShopItems and the new UI folders (posters: Full Rect; UI: Tight is fine), and set VideoClip importer settings for `Assets/videos` (transcode H.264, sensible bitrate for mobile).

### SHOP agent provides
- `UI/DeskHubScreen : ScreenView` (GameScreen.DeskHub) with modes `Normal / Edit / Preview`; `public void RequestPreview(ShopItemData item)` (call before routing to DeskHub; applied OnShown); raises `GameSignals.PreviewOpened` / `EditModeChanged`. Buttons: hamburger → `OverlayController.OpenSettings()`, Shop → `flow.GoToShop()`, Edit → Edit mode, book → `flow.OpenJournalFromDesk()`.
- `Decor/DecorationRoom` (draws every owned decoration inside a room RectTransform, sorted by `sortingOrder`), `Decor/DecorationView` (Image + drag in Edit/Preview), `Decor/RoomCoordinates` (plain C#: normalised ↔ local point, clamping so an item can't leave the room).
- `UI/ShopScreen : ScreenView` (GameScreen.Shop) + `UI/ShopItemCard` + `UI/CoinBalanceLabel` (reusable: binds a TMP_Text to IWallet.CoinsChanged; the UI agent may use it too).
- Editor: `Editor/CatalogBuilder` — `public static void RebuildAll()` (menu `Restorium/Rebuild Catalogs & Locale Tables`): refreshes `Assets/Data/Catalogs/PosterCatalog.asset` (every PosterData, by journalOrder) and `ShopCatalog.asset` (every ShopItemData under `Assets/Data/ShopItems/`, by displayOrder), then calls `LocaleTableBuilder.RebuildAll()`; plus an `AssetPostprocessor` that re-runs the catalogue part when a PosterData/ShopItemData is imported/deleted. `Editor/ShopItemWizard` (menu `Restorium/Shop/New Shop Item`: id, category, price, shop icon, placed sprite, placed size, name+description in pt-BR and en → creates `Assets/Data/ShopItems/<Id>.asset`, upserts `shop.item.<id>.name/.desc` into both locale tables via `LocaleTableBuilder.Upsert`, rebuilds catalogs). `Restorium/Shop/Create Starter Items` creates lamp (100), plant (50), books (100) from `Assets/Art/ShopItems/<id>/`.

### UI agent provides
- `UI/OverlayController : MonoBehaviour` — `public void OpenPause()`, `public void OpenSettings()`, `public void CloseTop()`, `public bool AnyOpen { get; }`; Android back / Escape (Input System) closes the top overlay; raises `GameSignals.ModalChanged`. `UI/PauseButton` (hamburger; calls OpenPause, finds the controller via a serialized field). `UI/PauseMenuOverlay` (Journal → `flow.OpenJournal()`, Settings → OpenSettings, Quit → `flow.QuitToTitle()`). `UI/SettingsOverlay` (language picker cycling `ILocalizationService` locales, SFX + Music sliders → IAudioService + SaveData volumes, back button; propose extras in the handoff rather than adding store-dependent ones).
- `UI/JournalScreen` rewrite (pages from `flow.Posters`, states Locked / Available / InProgress / Completed, Restore/Continue/Restored labels, page-flip animation + `SfxId.PageFlip`, arrows enabled only when there is a page that way, back button → `flow.GoToDeskHub()` visible when `flow.DeskHubUnlocked`, fade-in when shown after the book video).
- `UI/FinishedRepairScreen` rewrite to the new design (reads `flow.LastCompletedPoster`; before→final reveal in the linen frame; "+N" coin label; Double Reward button shown only if `flow.CanDoubleReward`, calls `flow.RequestDoubleReward`; Continue → `flow.GoToDeskHub()`).
- Tutorial: `TutorialController` rewrite to sequences (`TutorialSequenceData[] sequences`), two views (`TracyOverlayView portraitOverlay`, `TracyOverlayView hubOverlay` for HubFullBody), waits while `ICutscenePlayer.IsPlaying` or `OverlayController.AnyOpen`, listens to IDecorationInventory.ItemPurchased and GameSignals, `public bool TryStart(string sequenceId)`, `Skip()`, `Restart()`, persists `activeTutorialSequence/StepIndex` after every step. `Editor/TutorialAuthoringTool.RebuildAll()` (menu `Restorium/Tutorial/Rebuild Tutorial Sequences`) builds the sequences below, reusing the 16 existing step assets in `Assets/Data/Tutorial/` for `first_restoration` (keep their GUIDs; update their text keys if needed).
- Localization: `Editor/LocaleTableBuilder` — `public static void RebuildAll()` (writes `Assets/Data/Localization/pt-BR.asset` and `en.asset` from `LocaleSource.All()`, upsert-only) and `public static void Upsert(string localeCode, string key, string value)`. `ILocalizationService` gains `IReadOnlyList<string> AvailableLocales { get; }` and `string CurrentLocale { get; }`; `LocalizationService` auto-picks from `Application.systemLanguage` (Portuguese → pt-BR, else en) when `SaveData.localeCode` is empty, and persists `SetLocale`. English is the language of the Figma copy; pt-BR stays fully supported.

## 6. Tutorial sequences (ids are save-file identities)

| sequenceId | trigger | requires | content |
|---|---|---|---|
| `first_restoration` | ScreenEntered Journal | — | the existing 16 steps (welcome → restore → six tools → congrats → Continue). Step 15/16 text should now mention coins and that Continue leads to the workshop. |
| `deskhub_lamp` | ScreenEntered DeskHub | `first_restoration` | lamp/lighting lines (HubFullBody) → TapTarget `desk.shop` → (ScreenEntered Shop) line → TapTarget `shop.item.lamp` → PreviewOpened `lamp` → line + drag hint → ItemPurchased `lamp` with hand on `preview.buy` → happy line → mention edit mode (`desk.edit`, TapAnywhere) → TapTarget `desk.book`. |
| `journal_page2` | ScreenEntered Journal | `deskhub_lamp`, poster `poster01` completed | "a new poster arrived" → TapTarget `journal.nextPage` → TapTarget `journal.restoreButton`. |
| `stickers` | StageKindStarted StickerPeel | — | explain stickers → StickerPeeled with hand on `sticker.next` → TapAnywhere/auto end. |

Every gated step keeps a safety `autoAdvanceSeconds` or a non-gated fallback so nothing can soft-lock. The shop purchase in `deskhub_lamp` must be affordable: poster 1 pays 100, lamp costs 100.

## 7. Tutorial anchor ids (TutorialAnchor.anchorId)

Existing: `journal.restoreButton`, `toolbar.dustRemover|waterSpray|deacidifier|squeegee|roller|pencil`, `poster.surface`, `finishedRepair.continueButton`.
New: `journal.nextPage`, `journal.prevPage`, `journal.back`, `finishedRepair.doubleButton`, `desk.shop`, `desk.book`, `desk.edit`, `desk.menu`, `desk.coins`, `shop.back`, `shop.item.<itemId>` (added by ShopItemCard at runtime for each card), `preview.buy`, `preview.cancel`, `preview.item`, `edit.place`, `edit.undo`, `edit.done`, `sticker.next`, `pause.button`.

## 8. Localization

Locale codes: `pt-BR`, `en`. Keys: `ui.<screen>.<element>`, `tutorial.<sequenceId>.<stepId>`, `stage.<stageId>.title`, `poster.<posterId>.title|chapter`, `shop.item.<itemId>.name|desc`. Every key you reference in code or data must exist in your LocaleSource partial (both languages). Existing MVP keys (e.g. `ui.journal.restore`, `ui.dialogue.tapToContinue`) keep their names; the UI agent moves them from PosterAuthoringTool into `LocaleSource.Legacy.cs` and adds English. Figma copy is English — use it verbatim for `en`, translate naturally for `pt-BR`. Draft missing lines (e.g. the lamp description, Tracy's lamp dialogue) in Tracy's warm, slightly flustered voice and list them in your handoff for the human to review.

## 9. File ownership

| Agent | Owns (create/edit) |
|---|---|
| LEAD | everything in §3, `Docs/Batch2/CONTRACT.md` |
| ART | `Assets/Art/**` new files, `Docs/Batch2/FigmaLayout.md`, `Docs/Batch2/screens/` |
| CORE | `Core/GameFlowController.cs`, `Core/GameBootstrap.cs`, `Core/SaveManager.cs`, `Core/SaveMigration.cs`, `Core/PosterProgressService.cs`, `Core/ScreenRouter.cs` (only if needed), `Core/SceneLoader.cs` (only if needed), `Economy/PlayerWallet.cs`, `Economy/DecorationInventory.cs`, `Economy/SimulatedRewardedAd.cs`, `Cinematics/CutscenePlayer.cs`, `Audio/*` (only if needed for ducking), `Tests/EditMode/Logic/{SaveMigration,PosterProgress,Wallet,DecorationInventory}Tests.cs`, `LocaleSource.Flow.cs` |
| RESTORATION | `Restoration/*` (all existing + `StickerView.cs`), `UI/StickerRemovalScreen.cs`, `UI/CleaningScreen.cs`, `UI/LinenBackingScreen.cs`, `UI/RestorationScreenBase.cs`, `FX/*`, `Editor/PosterAuthoringTool.cs` (+ any `Editor/Posters/*`), `Assets/Data/Poster1/**`, `Assets/Data/Poster2/**`, `Assets/Data/Tools/**`, `Tests/EditMode/RevealMaskTests.cs`, `Tests/EditMode/Logic/Sticker*Tests.cs`, `LocaleSource.Restoration.cs` |
| SHOP | `UI/DeskHubScreen.cs`, `UI/ShopScreen.cs`, `UI/ShopItemCard.cs`, `UI/CoinBalanceLabel.cs`, `Decor/*`, `Editor/CatalogBuilder.cs`, `Editor/ShopItemWizard.cs`, `Assets/Data/ShopItems/**`, `Assets/Data/Catalogs/**`, `Tests/EditMode/Logic/RoomCoordinatesTests.cs`, `LocaleSource.Shop.cs` |
| UI | `UI/OverlayController.cs`, `UI/PauseButton.cs`, `UI/PauseMenuOverlay.cs`, `UI/SettingsOverlay.cs`, `UI/JournalScreen.cs`, `UI/FinishedRepairScreen.cs`, `UI/ButtonSfx.cs`, `UI/TitleScreenController.cs`, `UI/ThanksForPlayingScreen.cs`, `Tutorial/*`, `Localization/*`, `Editor/TutorialAuthoringTool.cs`, `Editor/LocaleTableBuilder.cs`, `Assets/Data/Tutorial/**`, `Assets/Data/Localization/**`, `Tests/EditMode/LocalizationTests.cs`, `LocaleSource.{Legacy,UI,Tutorial}.cs` |
| SCENE BUILDER (phase 2) | `Editor/SceneBuilder/**`, `Tests/EditMode/SceneValidationTests.cs` |

ScriptableObject `.asset` files are generated by the editor menu tools **when the human runs them in Unity** — agents write the tools, not the assets (you cannot run Unity here).

## 10. Handoff (required, last thing you do)

Write `Docs/Batch2/handoff/<AGENT>.md` containing:
1. Files created/changed (one line each, what it does).
2. Public API actually implemented (signatures) — must match §5; flag any deviation loudly.
3. **Scene wiring for the builder**: for every MonoBehaviour a scene needs — which GameObject (name from §2 or an existing one), required child hierarchy (names, component types, which art file from `Assets/Art/**`, which FigmaLayout element), and every serialized field and what to assign. Include TutorialAnchor ids to attach and where.
4. Editor menu items you added and the order the human should run them.
5. Localization keys you added.
6. Tests you wrote and how they run.
7. Needs from others / open questions / anything you drafted that the human must review.
8. Optimisation ideas you noticed.

## 11. Amendments (approved by the lead after the CORE agent finished — these override the sections above)

- **A1** `GameSignals.Clear()` runs in `SceneLoader` just before the new scene activates (not on `sceneLoaded`, which fires after the new scene's Awake/OnEnable). Subscribe in `OnEnable` as normal.
- **A2** `IAudioService` gained `void SetMusicPaused(bool paused)` (AudioManager implements it; also exposes `MusicMixerGroup`). Any test fake of IAudioService must implement it.
- **A3** `CutscenePlayer` has two extra optional fields: `videoAudioSource`, `overlayCanvas` (+ timing settings). Video audio routes into the mixer's Music group.
- **A4** `GameFlowController` has an extra field `bool bookCutsceneSkippable` (default true). Intro and completion cutscenes are always forced.
- **A5** `RestorationController.beginOnStart` must be false in the scene (the flow starts posters). `Poster01.completionCutscene = Assets/videos/tracysc2.mp4` (set by the poster recipe).
- **A6** Pure-logic code lives in `Assets/Scripts/Logic/**` or next to its feature; every pure file used by a test in `Tests/EditMode/Logic/` must also be listed in `Tools/logictests/LogicTests.csproj` (the checker maintains that file).
