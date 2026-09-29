# Batch 2 — Human Checklist (Unity Editor Setup & Verification)

This is a step-by-step checklist for getting "Batch 2" (Level 2 content, Shop, Restoration/Stickers,
UI overlays, Journal, Tutorial sequences, and the Scene Builder editor tooling) actually working in the
running game. It assumes **you have never used Unity before** — every menu path and UI concept is
explained the first time it comes up.

Source material this checklist is built from (read these if you want more detail on any step):
- `Docs/Batch2/README.md` — quick-start summary
- `Docs/Batch2/CONTRACT.md` — full architecture/flow spec
- `Docs/Batch2/CONSISTENCY.md` — cross-agent audit, the authoritative "scene builder input" section, and the known-issues list
- `Docs/Batch2/FigmaLayout.md` — pixel-level layout reference
- `Docs/Batch2/handoff/CORE.md`, `RESTORATION.md`, `SCENE_BUILDER.md`, `SHOP.md`, `UI.md` — per-system detail
- `Docs/Batch2/screens/*.png` — reference screenshots of what each screen should look like

**Some Unity terms used throughout, defined once:**
- **Project window**: the panel (usually bottom-left or bottom of the Unity Editor window) that shows your
  folder/file structure under `Assets/`. This is your project's file browser inside Unity.
- **Hierarchy window**: the panel (usually top-left) that lists every object in the currently open scene, as
  a nested tree (like a folder tree, but for game objects).
- **Inspector window**: the panel (usually right side) that shows the details/settings of whatever object
  you currently have selected (in the Hierarchy or Project window). This is where you assign fields, tick
  checkboxes, and see a script's exposed settings.
- **Console window**: the panel that shows log messages, warnings, and errors (`Window > General > Console`
  if it isn't already open). Always check this after running an editor tool or pressing Play.
- **Scene**: a single "level" or screen-container file (ends in `.unity`). This project has three:
  `Title.unity`, `Game.unity`, `ThanksForPlaying.unity`, all in `Assets/Scenes/`. Batch 2's work lives almost
  entirely in `Game.unity`.
- **GameObject**: any object that exists in a scene (shown in the Hierarchy). Can be empty or have visuals.
- **Component**: a chunk of behavior or data attached to a GameObject (e.g. an `Image`, a `Button`, a custom
  script like `DeskHubScreen`). You see and edit a GameObject's components in the Inspector.
- **Prefab**: a reusable, saved-to-disk template for a GameObject (with all its children/components) that you
  can stamp into scenes. Not heavily relevant here — Batch 2 mostly builds objects directly in `Game.unity`.
- **ScriptableObject**: a data asset (not a GameObject) — e.g. a Shop item's stats, a Poster's data, a
  Tutorial sequence's steps. These live in the Project window under `Assets/Data/**` and are what the
  Editor menu tools (below) generate.
- **Menu item / Editor menu**: a custom command added to Unity's top menu bar (e.g. `Restorium > Shop >
  Create Starter Items`). These run C# tools this batch's authors wrote specifically to build the data/scene
  for you, instead of you doing it by hand.
- **Play button**: the triangular ▶ button at the top-middle of the Unity Editor. Pressing it runs the game
  inside the Editor (Play Mode) using the currently open scene.
- **Play Mode**: the state Unity is in while running the game inside the Editor (as opposed to Edit Mode,
  where you're just editing scenes/assets). You cannot edit scene objects while in Play Mode (changes don't
  save).

---

## Phase 0 — Prerequisites / Opening the Project

- [ ] 0.1 Make sure Unity Hub (or Unity directly) is installed with editor version **6000.5.3f1** (this
  project's declared Unity version — CONTRACT.md §"Project"). If you don't have this exact version, Unity
  Hub will offer to install it when you try to open the project; let it.
- [ ] 0.2 Open Unity Hub, click **Open** (or **Add project from disk**), and select the folder
  `C:\Games(shared)\RestoriumEmporium\RestoriumEmporium`. Wait for Unity to finish importing/compiling
  (a progress bar appears bottom-right; this can take several minutes the first time).
- [ ] 0.3 Once the Editor is open, check the **Console window** (`Window > General > Console` if not
  visible) for red error text. Per the batch's own compile check, this should compile clean
  ("COMPILE OK" was the last verified state before merge). If you see red errors immediately on open,
  stop and investigate before continuing — none of the menu items below will work reliably with compile
  errors present.
  - **If it fails:** note the exact error text and file/line from the Console; that tells you which script
    has a problem. This would be unexpected given the pre-merge compile check passed — it may mean
    something didn't merge cleanly.
- [ ] 0.4 In the **Project window**, navigate to `Assets/Scenes/` and double-click **`Game.unity`** to open
  it (double-clicking a scene file loads it as the currently active scene, shown in the Hierarchy window).
  Most of the work below happens with this scene open.
- [ ] 0.5 **Known pre-merge gotcha (per README.md), check if still relevant:** the original instructions
  warned that your checkout might have *untracked* copies of `Assets/Art/Posters/poster2/`,
  `Assets/videos/`, and `Assets/Audio/(stickerPeel)...mp3` that would block the merge. The git status at
  the time this checklist was written shows the merge is **already done and the working tree is clean**,
  so this step is very likely already resolved — but if you ever see Unity complain about duplicate/
  conflicting asset files in these folders, that's the symptom this note is about.

---

## Phase 1 — Run the Asset-Building Editor Menus (in order)

These are custom menu items (added to Unity's menu bar under a top-level **Restorium** menu) that generate
the ScriptableObject data assets (Posters, Shop items, Tutorial sequences, Localization tables, Catalogs)
that the scene needs to reference. **Run them in this exact order** — each one is safe to re-run later
(they update in place, never duplicate), so if you're not sure whether one already ran, running it again is
harmless.

For every step below: after clicking the menu item, check the **Console window** for `[ToolName]`-prefixed
log lines or errors. Most of these tools print a summary of what they created/updated.

- [ ] 1.1 **`Restorium > Fix Art Import Settings (Sprites)`**
  Fixes texture import settings (compression format ASTC, Full Rect for posters, etc.) across
  `Assets/Art/**`, and also transcodes videos under `Assets/videos/` to H.264 (this menu item internally
  runs the same video-fix routine as 1.1b below, so running 1.1 covers both).
  - **How to know it worked:** Console shows a summary of files reimported (harmless if it reports "0
    changed" — the art already landed in a previous commit).
  - **If it fails:** check the Console for the exact texture path it choked on; it batches the reimport so
    one bad file shouldn't stop the others, but investigate any red error.
- [ ] 1.1b **`Restorium > Fix Video Import Settings`** (optional/redundant — run it anyway, it's cheap)
  Same effect on videos as part of 1.1; harmless to run again.
- [ ] 1.2 **`Restorium > Posters > Create or Update All Posters`**
  (Equivalent to running `Create or Update Poster 1 Data` then `Create or Update Poster 2 Data`
  individually, if you prefer to see them run one at a time.)
  Creates/updates:
  - Poster 1 data (`Assets/Data/Poster1/Poster01.asset` + its stages) — including wiring the `tracysc2`
    completion cutscene onto it.
  - Poster 2 data (`Assets/Data/Poster2/Poster02.asset` + its stages, including the new **Sticker Removal**
    stage).
  - Adds a `StickerPeel` sound-effect row to the SFX library automatically.
  - Calls the Shop/Catalog rebuild internally at the end (see 1.5), so running this step alone already
    partially covers later steps — no harm in still running them explicitly.
  - **How to know it worked:** Console reports each Poster/Stage asset created or updated; check
    `Assets/Data/Poster1/` and `Assets/Data/Poster2/` in the Project window afterward — you should see the
    `.asset` files and a `Stages/` subfolder with individual stage assets in each.
  - **If it fails / reports a missing art file:** it reports the exact expected path. Confirm that file
    exists under `Assets/Art/Posters/**`; if the batch's art really is missing, that's a real content gap —
    flag it, don't invent a substitute.
  - Optional follow-ups if you ever need them later: `Restorium > Posters > Reset Poster 2 Sticker
    Positions (Figma defaults)` (resets sticker hit-areas if someone hand-dragged them off in the
    `StickerStageInspector`), and `Restorium > Posters > Add Sticker Peel Sound To SfxLibrary` (re-runs just
    the SFX-wiring part on its own).
- [ ] 1.3 **`Restorium > Shop > Create Starter Items`**
  Creates the three starter shop items from art under `Assets/Art/ShopItems/<id>/{icon,placed}.png`:
  - **Lamp** — id `lamp`, price **100** coins (this exact id/price is hard-referenced by the
    `deskhub_lamp` tutorial sequence — see Phase 3.6 — do not rename or reprice it without also updating
    that tutorial data).
  - **Plant Pot** — id `plant`, price 50 coins.
  - **Book Pile** — id `books`, price 100 coins.
  - **How to know it worked:** `Assets/Data/ShopItems/` now contains `Lamp.asset`, `Plant.asset` (or
    similarly named), `Books.asset` (exact file names may vary slightly — check the folder).
  - **If it fails:** it will report per-missing-file if the icon/placed PNGs aren't found at
    `Assets/Art/ShopItems/lamp|plant|books/icon.png` and `placed.png`. Confirm those exist in the Project
    window before re-running.
- [ ] 1.4 **`Restorium > Tutorial > Rebuild Tutorial Sequences`**
  Builds the four tutorial sequence data assets under `Assets/Data/Tutorial/Sequences/`:
  `first_restoration`, `deskhub_lamp`, `journal_page2`, `stickers`. Must run **after** steps 1.2/1.3 (it
  references posters and the lamp item) and after locale keys exist (step 1.5 below) — per UI.md, the
  intended order is locale rebuild → tutorial rebuild, though CONSISTENCY.md's audit found the order is
  actually not strictly load-bearing since `LocaleSource.All()` is pure compiled code. Running 1.4 after 1.5
  (as listed here) is the safe choice either way.
  - **How to know it worked:** `Assets/Data/Tutorial/Sequences/` contains 4 sequence assets; Console
    reports any of the 16 pre-existing `first_restoration` step assets it couldn't find by path (it reuses
    them by GUID, it does not recreate them).
- [ ] 1.5 **`Restorium > Localization > Rebuild Locale Tables`**
  Writes/updates `Assets/Data/Localization/pt-BR.asset` and `en.asset` from all the `LocaleSource.*.cs`
  files (130 keys total per CONSISTENCY.md's audit — Legacy, Flow, Restoration, Shop, UI, Tutorial). Upsert
  only — never deletes existing translated keys.
  - **How to know it worked:** select `Assets/Data/Localization/en.asset` in the Project window; the
    Inspector should show a long list of key/value string entries.
- [ ] 1.6 **`Restorium > Rebuild Catalogs & Locale Tables`**
  Final consolidation pass: rebuilds `Assets/Data/Catalogs/PosterCatalog.asset` (every Poster, ordered) and
  `Assets/Data/Catalogs/ShopCatalog.asset` (every Shop item, ordered), and internally re-runs the locale
  table rebuild too. Safe/cheap to run again here even though earlier steps already triggered parts of it.
  This also auto-runs in the background from now on any time you import/delete a `PosterData` or
  `ShopItemData` asset, so future one-off additions (see "Adding content later" below) don't need this step
  repeated manually.
  - **How to know it worked:** `Assets/Data/Catalogs/PosterCatalog.asset` and `ShopCatalog.asset` exist and,
    when selected, show the Poster/Item entries in the Inspector.

---

## Phase 2 — Run the Scene Builder

The Scene Builder is an Editor-only tool (`Assets/Editor/SceneBuilder/*.cs`) written specifically for this
batch. It does **not** ship in the actual game — it exists purely so you don't have to hand-build ~10
new/redesigned screens and hand-wire ~15 components by dragging things in the Inspector yourself. It reads
the data assets from Phase 1 and the handoff docs' specifications, and constructs/updates the actual
GameObjects inside `Game.unity`.

Per its own handoff (`SCENE_BUILDER.md`), it has **never been executed against a real Unity Editor** before
you run it now (Unity could not be run on the machine that built it) — it is "correct by construction"
(every field assignment is validated, every missing asset is reported by exact path, nothing is silently
skipped), but treat this as a genuine first run, not a formality.

- [ ] 2.1 Confirm `Game.unity` is open (Phase 0.4) and that you are **not** in Play Mode (the Play button at
  the top of the Editor should not be highlighted/active — the tool refuses to run during Play Mode).
- [ ] 2.2 Run **`Restorium > Scene > Build Batch 2 Scene Objects`**.
  This single command builds/updates, in one pass: `CutscenePlayer`, `Overlays` (Pause Menu + Settings),
  `HubTracyOverlay`, `DeskHubScreen`, `ShopScreen`, `StickerRemovalScreen`, rewires the existing
  `JournalScreen`/`FinishedRepairScreen`, adds pause hamburger buttons to `CleaningScreen`/
  `LinenBackingFrontScreen`/`LinenBackingBackScreen`/`LinenBackingFinalScreen`, and wires up the `GameFlow`
  object's components (`ScreenRouter`, `GameFlowController`, `TutorialController`, `TutorialInputGate`,
  forcing `RestorationController.beginOnStart` to **unticked** every run).
- [ ] 2.3 **Read the summary dialog** that appears when it finishes. It reports counts of objects
  **Created**, **Updated**, and **Problems** (with the first 20 problem lines shown directly). This is the
  "what to do if it fails" mechanism for the whole batch:
  - A "Problem" line names the **exact object/field/asset path** that's wrong — most commonly "asset not
    found at `<path>`", meaning a Phase 1 menu item wasn't run yet, or that asset genuinely doesn't exist.
  - **What to do:** go run whichever Phase 1 menu step produces that missing asset, then come back and
    re-run **`Restorium > Scene > Build Batch 2 Scene Objects`** again. It is idempotent — re-running never
    duplicates objects, it only updates what's already there. Repeat this loop (build → read problems →
    fix the upstream cause → rebuild) until the Problems count is 0, or until every remaining problem is
    something you've confirmed is expected (see Known Issues in Phase 5).
  - It asks for confirmation before saving the scene — say yes once you're satisfied with a run, so the
    changes persist to `Game.unity`.
- [ ] 2.4 Run **`Restorium > Scene > Validate Game Scene`**. This is a separate, **read-only** tool (safe to
  run any time, doesn't change anything) that does an independent second pass:
  - Scans every Batch-2 script (`RestoriumEmporium.*` component) in the scene for null/unassigned Inspector
    fields that shouldn't be null.
  - Cross-checks every Tutorial step's target anchor id against actual `TutorialAnchor` components present
    in the scene.
  - Confirms `ScreenRouter` covers every screen a restoration stage or the flow can route to.
  - **How to know it worked:** per README.md, it "should report nothing missing." If it reports something,
    the message names the exact component/field/screen at fault — go fix that (usually by re-running the
    relevant Phase 1/2.2 step, or by manually assigning a field the tool couldn't infer).
- [ ] 2.5 **Manually double-check one specific checkbox** (this is explicitly called out in both
  `CONSISTENCY.md` and `SCENE_BUILDER.md` as the one thing with *no compile-time signal if it's wrong*):
  Select the **`GameFlow`** GameObject in the Hierarchy window, find the **Restoration Controller**
  component in the Inspector, and confirm **`Begin On Start`** is **unticked**. The Scene Builder forces
  this every run, but verify it by eye anyway — if it's ticked, `RestorationController` will race
  `GameFlowController` at startup and silently break the intro flow.

---

## Phase 3 — Verify Each System

Do a quick visual pass of every new/changed screen against its reference screenshot in
`Docs/Batch2/screens/`. Per README.md: "The layout numbers come from Figma, but nobody has seen them
rendered yet. The DeskHub edit/preview panels are the likeliest to be a few pixels off." Minor pixel
offsets are expected and not blocking; look for objects that are missing entirely, wildly mispositioned, or
non-functional.

You can inspect a screen without playing the whole game by selecting its GameObject in the Hierarchy (e.g.
`DeskHubScreen`) and toggling it active/visible in the **Scene view** (the 3D/2D editable view, as opposed
to the **Game view**, which is the actual rendered-camera output) — but the real functional test is
pressing Play and going through the flow (Phase 4 covers that end-to-end). Use this phase for a static,
per-screen visual/wiring sanity check first.

### 3.1 Desk Hub
- [ ] Compare against `Docs/Batch2/screens/Desk_Hub.png` (the reference frame shows a full-body Tracy with
  a dialogue box, hamburger/bag/edit-mode buttons top-right/left, a coin balance pill, and a corkboard with
  pinned notes behind her).
- [ ] Note: the corkboard (`catBoard.png`) and the three Tracy full-body poses are **not** children of
  `DeskHubScreen` — per a resolved dispute in `CONSISTENCY.md`, they belong to the separate
  `HubTracyOverlay` object (only shown during the tutorial's dialogue beats). Don't be alarmed if
  `DeskHubScreen` alone (with no tutorial running) shows a plainer room — that's correct; compare
  `Docs/Batch2/screens/enteredEditMode.png` / `PreviewMode.png` instead for what the bare room looks like.
- [ ] Also compare Edit mode against `Docs/Batch2/screens/enteredEditMode.png` and
  `Docs/Batch2/screens/SelectedObjectToMove.png`, and Preview mode against
  `Docs/Batch2/screens/PreviewMode.png`. These three states are the ones flagged as most likely to be a
  few pixels off (nested panel coordinate math), per `SCENE_BUILDER.md` §4.
- [ ] Hamburger (top area) opens the Settings overlay directly (not the Pause menu) — this is intentional,
  confirm it does so rather than opening Pause.
- [ ] Bag/Shop button routes to the Shop screen.
- [ ] Edit-mode button enters Edit mode; "Back to workshop" exits it.
- [ ] Book prop (bottom of the room) plays the book-open video transition, then opens the Journal.

### 3.2 Shop
- [ ] Compare against `Docs/Batch2/screens/Shop.png` (Décor tab active showing Lamp/Books/Plant cards with
  prices, a coin balance row, and "Remove ads"/"Buy More Coins" pills).
- [ ] "Remove ads" and "Buy More Coins" buttons should be **visible but not clickable** — this is
  intentional for this build (IAP/ads land in a future batch), not a bug.
- [ ] "Misc." tab should be present but empty this build (also intentional).
- [ ] Tapping an item you don't yet own routes to DeskHub in **Preview mode** with that item selected.
- [ ] Owned items show as owned and cannot be tapped/re-bought.
- [ ] "Go back to workbench" returns to DeskHub.

### 3.3 Restoration / Stickers (Poster 2)
- [ ] The new **Sticker Removal** stage (between Dust and Wash on Poster 2) shows a close-up image with two
  tappable "stickers" (tape pieces) over it.
- [ ] Tapping a sticker plays the sticker-peel sound effect and the sticker visually falls off-screen.
- [ ] Once both stickers are removed, the stage auto-completes after a short pause (~0.35s, plus the
  controller's own ~1s "stage settle" pause — so roughly 1.35s total is expected and intentional, per
  RESTORATION.md §3B, not a hang).
- [ ] This screen (like Cleaning/LinenBacking) has its own hamburger (top-left) that opens the Pause menu.
- [ ] Compare general restoration-tool-screen look against `Docs/Batch2/screens/CleaningScreen.png` (for
  the general desk/tool-tray style continuity, even though that particular PNG is the Cleaning stage, not
  the sticker stage specifically — no separate sticker-stage screenshot exists in `screens/`).

### 3.4 UI Overlays (Pause / Settings)
- [ ] From any restoration screen or the Desk Hub (via hamburger), the **Pause** overlay should appear
  (except: Desk Hub's hamburger opens Settings directly instead, per 3.1; Journal has **no** hamburger by
  design — don't expect one there).
- [ ] Compare Pause overlay against `Docs/Batch2/screens/GamePausedOverlay.png` — Journal / Settings / Quit
  buttons, sleeping-Tracy art, "GAME PAUSED" title.
- [ ] Compare Settings overlay against `Docs/Batch2/screens/SettingsScene.png` — language picker (cycles
  between English and pt-BR), SFX and Music sliders, a Tracy portrait.
- [ ] Android back button (or Escape key in the Editor, via the new Input System) should close whichever
  overlay is on top; Journal/Settings/Quit routes should each work.
- [ ] Opening Journal from Pause should keep the current restoration progress saved (the poster shows
  "Continue" rather than "Restore" when you go back into it).

### 3.5 Journal
- [ ] Compare against `Docs/Batch2/screens/JounalPage.png`.
- [ ] One page per poster, in `journalOrder` (Poster 1 then Poster 2).
- [ ] A **locked** poster (previous one not completed) shows its Restore button **disabled**.
- [ ] An **in-progress** poster shows "Continue" instead of "Restore".
- [ ] A **completed** poster shows the finished artwork and a disabled "Restored" button.
- [ ] "Go back to workbench" button (top-left) is only visible once the Desk Hub is unlocked (i.e. at least
  one poster has ever been completed).
- [ ] Left/right page-turn arrows only enabled when there's a page in that direction; turning a page plays
  a page-flip sound.

### 3.6 Finished Repair Screen
- [ ] Compare against `Docs/Batch2/screens/FinishedRepairBG.png`.
- [ ] Shows a before→after card-flip reveal of the completed poster, a "+100" coins label, a "Double
  Reward" button (only shown if a reward is still available to double via a simulated rewarded ad — this
  is a stand-in for real ads/IAP, always available in Editor/Development builds after ~1.5 seconds), an
  "or", and a "Continue" button.
- [ ] The card-flip animation firing correctly on this screen is the one cross-file wiring point flagged as
  "easy to miss" in `CONSISTENCY.md` (dispute c) — if the poster art does **not** flip/reveal, check that
  the `FlipCard` object's `On Shown` event is wired to call `CardFlipAnimator.Play()` (Scene Builder should
  have done this automatically; verify by selecting `FinishedRepairScreen` in the Hierarchy, and in the
  Inspector looking at its exposed `On Shown` UnityEvent list).

### 3.7 Tutorial Sequences
There are four tutorial sequences, gated by save-file identity (their ids are stored in your save, so don't
rename them once you've playtested with a save file that references them):
- [ ] **`first_restoration`** — starts on entering the Journal for the first time; the existing 16-step
  MVP tutorial (welcome → restore → six tools → congrats → Continue), reused by GUID.
- [ ] **`deskhub_lamp`** — starts on first entering DeskHub after Poster 1: Tracy explains she needs a new
  lamp, points at Shop, hand lands on the Lamp card, previews it, hand on Buy Item, purchase completes,
  mentions Edit mode, hand on the book. Requires the lamp to cost exactly 100 coins (matching Poster 1's
  reward) — confirmed already true in the shipped data per `CONSISTENCY.md` issue #4, but re-verify if
  you ever reprice the lamp.
  - **Known limitation, not a bug:** the contract's stated "safety auto-advance timer" that's supposed to
    prevent any tutorial step from soft-locking is actually **inert in production** — it only fires when
    `Use Safety Timeout` is ticked on `TutorialController`, and that flag is intentionally left **unticked**
    in the real scene. So in practice, soft-lock protection comes entirely from every gated step having an
    always-achievable advance condition — which is true today, but be aware if you ever author a new gated
    tutorial step pointing at an anchor id that could stop existing (e.g. if an item id is renamed), it
    could hang forever with no built-in timeout to save it.
- [ ] **`journal_page2`** — starts on returning to Journal after `deskhub_lamp` and completing Poster 1:
  "a new poster arrived" → hand on the next-page arrow → hand on Restore.
- [ ] **`stickers`** — starts when the Sticker Removal stage begins: explains the mechanic, hand lands on
  the next sticker to peel, ends automatically once all stickers are gone.
- [ ] For each: confirm the hand-pointer graphic actually appears over the correct button/target, and that
  tapping the target advances the tutorial step (rather than requiring a tap elsewhere).

---

## Phase 4 — Full Playtest / Smoke Test

- [ ] 4.1 **Delete your save file** so you test as a brand-new player. The save file lives outside the
  project folder, at (Windows): `%USERPROFILE%\AppData\LocalLow\<Company Name>\<Product Name>\save.json`
  (exact company/product folder names come from Unity's Player Settings — check
  `Edit > Project Settings > Player` if you're not sure of the exact folder, or just search your
  `AppData\LocalLow` folder for `save.json`). Delete this file (or rename it if you want to keep it as a
  backup) before every fresh playtest.
- [ ] 4.2 Open **`Assets/Scenes/Title.unity`** (double-click it in the Project window) — the smoke test
  must start from the **Title** scene, not `Game.unity` directly, since Title is what sets up the
  `Systems`/`GameBootstrap` object that registers all the required services.
- [ ] 4.3 Press the **Play button** (▶, top-middle of the Editor). Watch the **Game view** tab (should be
  selected automatically) — this is the actual rendered game output, as opposed to the Scene view which is
  just the editable layout.
- [ ] 4.4 Walk through the full expected sequence, checking each beat off as it happens (per
  README.md/CONSISTENCY.md's consolidated flow):
  1. [ ] `tracysc1` intro cutscene plays (forced, not skippable — this one specifically should NOT show a
     skip hint since it's a forced first-launch video).
  2. [ ] Journal opens; `first_restoration` tutorial begins (welcome message, hand lands on Restore).
  3. [ ] Poster 1 restoration stages run in sequence (Cleaning → Linen Front/Back/Final), tutorial guiding
     you through each tool.
  4. [ ] Poster 1 completes → base coin reward (+100) is paid → `tracysc2` cutscene plays (forced, once) →
     **Finished Repair** screen (new design — before/after reveal, +100 coins, Double Reward option).
  5. [ ] Continue → **Desk Hub** (not the old "Thanks For Playing" screen) → `deskhub_lamp` tutorial starts.
  6. [ ] Follow the tutorial: Shop → buy Lamp (100 coins) → lamp appears placed in the Desk Hub room.
  7. [ ] Tap the book → `openBookTransition` video plays → Journal fades in → `journal_page2` tutorial
     (hand on next-page arrow → poster 2 page → Restore).
  8. [ ] Poster 2 stages run: Dust → **Sticker Removal** (tap both stickers, `stickers` tutorial explains
     it once) → Wash → Deacidify → Squeegee → Adhesive → Mend.
  9. [ ] Poster 2 completes → Finished Repair screen again → Continue → back to Desk Hub.
- [ ] 4.5 **Mid-restoration pause test:** at any point during a restoration stage, tap the hamburger
  (top-left) → confirm the Game Paused overlay appears with Journal/Settings/Quit options, and that
  resuming (tap the hamburger again, or Android back) returns you exactly where you were.
- [ ] 4.6 **Pause → Journal test:** from Pause, open Journal mid-restoration; confirm the restoration
  progress is preserved (the poster now shows "Continue" instead of "Restore").
- [ ] 4.7 **Relaunch test:** stop Play Mode, then press Play again from Title (without deleting the save
  this time). Confirm it resumes correctly per the documented relaunch rule: if a restoration was in
  progress, it resumes that stage; else if the Desk Hub is unlocked, it goes straight there; else it opens
  the Journal. (No intro video replay, since it's already been marked seen.)
- [ ] 4.8 **Settings test:** open Settings (via Pause, or directly via the Desk Hub hamburger), cycle the
  language and confirm text throughout the game switches between English and pt-BR (Portuguese), and that
  the SFX/Music sliders audibly change volume.
- [ ] 4.9 Stop Play Mode when done (press the Play button again to exit). Remember: any changes made to
  scene objects *while* in Play Mode are **not saved** — if you needed to fix something, exit Play Mode
  first, fix it in Edit Mode, then re-test.

---

## Phase 5 — Known Issues / Troubleshooting

These are documented, non-blocking items from `CONSISTENCY.md` — read them before assuming something you
notice is a new bug:

- [ ] **Tutorial safety-timeout is inert in production** (see Phase 3.7) — by design, not a bug, but a
  latent risk for future tutorial authoring. No action needed today.
- [ ] **DRAFT localization strings need a native-speaker/content review.** These lines were written by the
  batch's authors as reasonable placeholders, not sourced from the Figma design, and are flagged in the
  handoffs for human review:
  - `poster.poster02.title` / `.chapter` (RESTORATION's content guess: "Eternal Theatre" / cinema program).
  - `ui.shop.titleTop` / `.titleMain` (SHOP's pt-BR split of "Tracy's Emporium Shop").
  - `ui.shop.owned` / `.empty` / `ui.preview.notEnoughCoins` (SHOP, no Figma reference for these).
  - `shop.item.lamp.desc` (SHOP's draft, ties into the `deskhub_lamp` tutorial beat about Tracy's lamp).
  - Every `tutorial.deskhub_lamp.*` / `journal_page2.*` / `stickers.*` line, and an optional, **not yet
    wired-in** `ui.settings.resetProgress` string group (strings exist, feature not built — see below).
  - **Confirm with the batch2 author** whether any of these need rewriting before players see them.
- [ ] **"Reset Progress" setting is drafted but not built.** Localization strings exist
  (`ui.settings.resetProgress` etc.) but there is no actual button/confirm-flow wired into the Settings
  overlay. Not a bug — it was deliberately left out pending a design decision on the confirm-dialog look.
  Only relevant if you want that feature added.
- [ ] **The "×" close glyph on the future Remove Ads / Buy Coins screens** would need a Quicksand font
  (not currently in the project's font set) for a pixel-exact match to Figma. **Not in scope for this
  batch** — those two monetization screens aren't built yet (IAP/ads land in a future batch per the
  contract). No action needed now; noted so it isn't forgotten later.
- [ ] **If the Scene Builder reports "Problem" lines you don't understand:** re-read Phase 2.3 — each
  Problem names an exact object/field/asset path. The overwhelming majority of possible problems are "an
  upstream Phase 1 menu step wasn't run yet" — go run it, then re-run
  `Restorium > Scene > Build Batch 2 Scene Objects`. This build → check problems → fix upstream → rebuild
  loop is described by the tool's own author as "the intended workflow, not a failure sign."
  - Two specific things the Scene Builder is known to have gotten right per the consistency audit, in case
    you doubt them while troubleshooting: (a) the `catBoard`/Tracy full-body art belongs on
    `HubTracyOverlay`, not `DeskHubScreen` (confirmed against the reference screenshots); (b)
    `RestorationController.beginOnStart` should be **false** (unticked) in the real scene, even though it
    defaults to `true` in the raw script (that default exists on purpose, to let a single poster be tested
    in isolation — see the script's own tooltip).
- [ ] **Purely decorative Figma elements were intentionally skipped** by the Scene Builder (sparkle stars,
  the Shop grid's scrollbar track/thumb graphic, some "or" labels above buttons) because they have no
  functional/serialized purpose. If you want 100% pixel-parity with the Figma mockups, these can be added
  by hand later — their absence is not a defect.
- [ ] **Coordinate math has not been visually verified by the batch's authors** (Unity couldn't be run when
  it was built) — a transcription slip anywhere in the ~150 layout calls across the Scene Builder's files
  would only show up as a slightly-off element in the Scene view, not a compile or runtime error. This is
  exactly why Phase 3's per-screen visual comparison against `Docs/Batch2/screens/*.png` matters — treat
  that phase as the primary way such a slip would surface.
- [ ] **`Tools/compilecheck/EditTests.csproj`** may get silently regenerated the first time you open the
  project in Unity (a `git`-ignored, auto-generated file was hand-patched as a stopgap before this). Nothing
  to do — if you notice it change on its own, that's expected and fine.

---

## Adding New Content Later (reference, not part of the initial checklist)

- **New shop item:** `Restorium > Shop > New Shop Item` — fill in id, price, icon, placed-in-room art, size,
  and name/description in both English and pt-BR, then press Create. It appears in the shop automatically
  (no scene edits needed).
- **New poster:** add a recipe next to Poster 2's in `Assets/Editor/Posters/PosterRecipes.cs` and run the
  corresponding `Restorium > Posters > ...` menu. The Journal page and unlock order follow each poster's
  `journalOrder` field.
