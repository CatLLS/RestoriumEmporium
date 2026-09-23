# RESTORATION agent — Batch 2 handoff

## 0. Note on this handoff

This picks up a same-role agent that was cut off by a rate limit. On resuming I inventoried every file it had left on disk, read each one fully against the contract, and ran `sh Tools/compilecheck/check.sh`. The prior agent's work was in fact complete and internally consistent — including `PosterRecipes.cs` (its "Now the recipe file" note turned out to be its last checkpoint, not an unfinished file). I made no code changes; this handoff documents what is actually on disk. `sh Tools/compilecheck/check.sh` currently reports exactly 2 errors, both in `Assets/Scripts/UI/OverlayController.cs` (missing `PauseMenuOverlay`/`SettingsOverlay`, the UI agent's files) — nothing in RESTORATION-owned files.

## 1. Files created / changed

| File | What it does |
|---|---|
| `Assets/Scripts/Restoration/RestorationController.cs` | State machine, unchanged in shape from MVP but with **all save I/O removed** and `StageKind.StickerPeel` support: no tool, no painter, no coverage completion for sticker stages; completes only via `ForceCompleteCurrentStage()`; still calls `posterStack.SetStage(stage)` so the (hidden) poster's from/to sprites stay correct for the resume path and the next stage. `beginOnStart` defaults true but must be unticked in the real scene (A5). |
| `Assets/Scripts/Restoration/IRestorationRuntime.cs` | Unchanged (LEAD-owned); confirmed `ForceCompleteCurrentStage()` signature matches what the controller implements. |
| `Assets/Scripts/Restoration/StickerView.cs` (new) | One pooled, reusable tappable sticker: `Bind/Relayout/Unbind/Peel/SetInteractable`. Pointer-down (not click) starts the peel; `StickerPeelMotion` samples on unscaled time in `Update`, which is only enabled while falling; measures its own fall distance to the root canvas's bottom edge so it clears the screen on any phone shape; moved to last sibling while falling so it passes over the stickers still in place. |
| `Assets/Scripts/UI/StickerRemovalScreen.cs` (new) | `RestorationScreenBase` for `GameScreen.StickerRemoval`. Fits the close-up into `closeUpArea` (Cover/Contain), sizes `stickerRoot` to match, spawns/re-binds one `StickerView` per `StickerDefinition` through a pooled list, taps go through `StickerPeelTracker` before anything happens (no double-count), plays `SfxId.StickerPeel`, raises `GameSignals.RaiseStickerPeeled(remaining)`, waits for the last sticker to finish falling + `completeBeatSeconds` (0.35s default), then calls `runtime.ForceCompleteCurrentStage()`. Moves the `sticker.next` `TutorialAnchor` marker over the first remaining sticker. `OnEnable` (inherited) re-spawns every sticker — individual peels are not persisted. Header text comes from `RestorationScreenBase`. Editor gizmo preview of sticker rects when selected (`previewStage` field). |
| `Assets/Scripts/Logic/Sticker/StickerLayoutMath.cs` (new) | Plain C#, no UnityEngine. `Fit` (Contain/Cover aspect fit), `NormalizedToLocal`/`LocalToNormalized` (0..1 image fraction ↔ root-centred local units), `DesignRectToNormalized` (Figma top-left pixel rect → normalised bottom-left). |
| `Assets/Scripts/Logic/Sticker/StickerPeelMotion.cs` (new) | Plain C#. Pure function of elapsed time: lift (pop/grow/tilt, ease-out-back) then a solved-duration gravity fall with drift and spin. `DurationFor(fallDistance)` / `IsFinished`. |
| `Assets/Scripts/Logic/Sticker/StickerPeelTracker.cs` (new) | Plain C#. Which stickers are still on (`bool[]`), `TryPeel` (true exactly once per sticker, reports `completedNow` on the tap that empties it), `FirstRemaining()` for the tutorial marker, `Reset(count)` for re-entry. |
| `Assets/Scripts/Tests/EditMode/Logic/StickerLogicTests.cs` (new) | NUnit, no UnityEngine: `StickerLayoutTests` (Fit contain/cover/degenerate, NormalizedToLocal, round-trip, the Poster2 Figma sticker positions pinned by test, `StickerRect.Contains`), `StickerPeelTrackerTests` (6 cases incl. double-tap, out-of-range, reset), `StickerPeelMotionTests` (8 cases incl. mirrored direction, monotonic fall acceleration, `DurationFor` round-trip, zeroed-struct NaN safety). Already listed under `Logic\**\*.cs` / `Tests\EditMode\Logic\**\*.cs` in `Tools/logictests/LogicTests.csproj` — no csproj change was needed. |
| `Assets/Editor/PosterAuthoringTool.cs` | Generalised: `Restorium/Posters/Create or Update Poster 1 Data`, `.../Poster 2 Data`, `.../Create or Update All Posters`, plus two helper menus (`Reset Poster 2 Sticker Positions`, `Add Sticker Peel Sound To SfxLibrary`). Idempotent (loads-or-creates, never blanks a filled sprite reference, reports missing art with the exact expected path). Creates the 6 shared `ToolData` assets. Calls `CatalogBuilder.RebuildAll()` at the end of every run. No longer touches locale tables or tutorial steps. |
| `Assets/Editor/Posters/PosterRecipes.cs` (new) | The authored content, data-only, separate from the asset-writing code. `Poster1` (unchanged asset names/paths: `Assets/Data/Poster1`, `Poster01`, stages `01_Dust`..`06_Mend`; `journalOrder=1`, `coinReward=100`, `completionCutscene=Assets/videos/tracysc2.mp4`). `Poster2` (`Assets/Data/Poster2`, `Poster02`, stages `01_Dust`..`07_Mend` incl. `02_Stickers` on `GameScreen.StickerRemoval`/`StageKind.StickerPeel`; `journalOrder=2`, `coinReward=100`, no completion cutscene). Sticker default positions derived from Figma pixel data (see §7) via `StickerLayoutMath.DesignRectToNormalized`. |
| `Assets/Editor/Posters/StickerStageInspector.cs` (new) | `CustomEditor(RestorationStageData)`: for `kind == StickerPeel` only, draws the default inspector then a to-scale close-up preview with every sticker outlined; click-drag moves a sticker (clamped 0..1), scroll resizes it; edits go through `Undo.RecordObject` + `SetDirty`. Scrub stages get the untouched default inspector. |
| `Assets/Editor/Posters/ArtImportSettingsTool.cs` (new) | `Restorium/Fix Art Import Settings (Sprites)`: classifies every texture under `Assets/Art/**` (excluding `Particles/`) into Poster (`Art/Posters/**` + `PosterBack*`/`linnenBacking` — Full Rect, Clamp, ASTC 6x6, max 2048), Background (`*BG*`/`bg*` — ASTC 8x8, max 2048) or UI (everything else, incl. ShopItems/Settings/DeskHub — mesh type left alone, ASTC 6x6, max 2048); mipmaps off, alpha-is-transparency on; batched, only re-imports what actually differs. Also `Restorium/Fix Video Import Settings` (and called from the same menu): every `VideoClip` under `Assets/videos` gets H.264/Medium-bitrate transcoding, downscaled to max 1080px wide on Android. |
| `Assets/Editor/Localization/LocaleSource.Restoration.cs` | 5 keys (§5 below), pt-BR + en. |

No changes were needed to `Assets/Scripts/UI/CleaningScreen.cs`, `LinenBackingScreen.cs`, `RestorationScreenBase.cs`, `Assets/Scripts/Restoration/RestorationPresenter.cs`, `ToolBarController.cs`, `Assets/Scripts/FX/ToolFxController.cs` or `ParticleBurstPool.cs` — I read all of them against the "screens with no poster stack root" / "stages with `requiredTool == None`" concern and they already do the right thing:
- `RestorationPresenter.ResolveStackRoot` returns null for a screen whose `PosterStackRoot` is empty (StickerRemovalScreen leaves it empty on purpose) and `ApplyScreen` then `SetActive(false)`s the shared poster instead of parenting it — matches the "poster deactivated on Journal/FinishedRepair" behaviour already documented there, and now also covers StickerRemoval.
- `ToolBarController` is only ever placed on `CleaningScreen`/`LinenBackingScreen*`; `StickerRemovalScreen` has no `ToolBarRoot` and no `ToolBarController`, so there is nothing to gate on `requiredTool == None`.
- `ToolFxController.HandleToolSelected(ToolId.None)` simply finds no pool and stays idle; the sticker screen's painter is disabled anyway so `PaintedAt` never fires during that stage.

## 2. Public API actually implemented

Matches §5 exactly.

```csharp
// RestorationController : MonoBehaviour, IRestorationRuntime  (no save I/O)
void ForceCompleteCurrentStage();   // the only way a StickerPeel stage completes
StageKind CurrentStageKind { get; } // Scrub when no stage is running

// UI/StickerRemovalScreen : RestorationScreenBase
GameScreen Screen => GameScreen.StickerRemoval;
int RemainingStickers { get; }
```

`Restoration/StickerView.cs`, `Logic/Sticker/StickerLayoutMath.cs`, `StickerPeelMotion.cs`, `StickerPeelTracker.cs` are internal supporting types, not part of the cross-agent contract surface, but their public members are documented inline (WHAT & WHY blocks) since other agents may want to reuse `StickerLayoutMath.Fit` for similar zoomed-in layouts.

No deviations from §5 found.

## 3. Scene wiring for the builder

### A) `StickerRemovalScreen` (new object under the main Canvas, sibling of `CleaningScreen`/`LinenBackingScreen*`)

```
Canvas
└── StickerRemovalScreen        RectTransform stretch/stretch, offsets 0
    │  Component: Sticker Removal Screen (RestorationScreenBase subclass)
    │  Starts DISABLED (router enables it); add to GameFlow → Screen Router → Screens.
    ├── Background               Image, stretch/stretch, offsets 0
    │                            Source = Assets/Art/LinnenAssets/LinnenBackingBG(all)
    │                            Raycast Target UNTICKED
    ├── CloseUpArea               Empty RectTransform, stretch/stretch, offsets 0
    │   │  Component: Rect Mask 2D (crops the Cover-fit overflow)
    │   ├── CloseUp               Image (rect sized at runtime by the script)
    │   │                         Source = Assets/Art/Posters/poster2/close-upForStickerRemoval
    │   │                         (Scene-view only; stage.closeUpSprite wins at runtime)
    │   │                         Raycast Target UNTICKED
    │   └── StickerRoot           Empty RectTransform (sized at runtime; must be BELOW
    │                             CloseUp in the hierarchy so stickers draw on top)
    │       └── NextStickerMarker Empty RectTransform (invisible, no Image)
    │                             Component: Tutorial Anchor, Anchor Id = sticker.next
    ├── Header                    TextMeshPro, top-left anchor, Pos X 114 / Pos Y -90,
    │                             Width 184 / Height 30, Center+Middle align, size 18.
    │                             No LocalizedText — the script writes the stage title.
    └── PauseButton (LAST child)  UI agent's PauseButton prefab/object, top-left anchor,
                                   Pos X 14 / Pos Y -38, Width 53 / Height 57,
                                   Tutorial Anchor pause.button.
```

Inspector on `StickerRemovalScreen`:
| Field | Assign |
|---|---|
| `Restoration Source` (from base) | `GameFlow` (has `RestorationController`) |
| `Poster Stack Root` (from base) | **leave EMPTY** — hides the shared poster while this screen is up |
| `Tool Bar Root` (from base) | **leave EMPTY** |
| `Header Label` (from base) | `Header` |
| `Close Up Area` | `CloseUpArea` |
| `Close Up Image` | `CloseUp` |
| `Sticker Root` | `StickerRoot` |
| `Next Sticker Marker` | `NextStickerMarker` |
| `Sticker Template` | leave EMPTY (stickers are created in code) |
| `Fit Mode` | `Cover` |
| `Complete Beat Seconds` | `0.35` |
| `Peel Motion` | leave at defaults (`StickerPeelMotion.Default`) |
| `Preview Stage` (Editor only) | `Assets/Data/Poster2/Stages/02_Stickers` (draws gizmo preview when this object is selected) |

### B) `RestorationController` / `RestorationPresenter` (existing `GameFlow` object)

No new fields beyond what CORE's handoff already documents. Confirm (already true in the checked-out `RestorationController.cs`):
- `Begin On Start` **UNTICKED** (A5) — GameFlowController drives it.
- `Tools` array has the 6 `ToolData` assets from `Assets/Data/Tools/` (built by `PosterAuthoringTool`).
- `Stage Settle Seconds` default 1s is fine; sticker completion adds its own separate 0.35s beat on top, so the total pause after the last sticker falls is roughly 0.35s (screen's own beat) + 1s (controller's settle) before the transition/flow reacts — that is intentional (same "mask snaps, then the game holds still" pacing as scrub stages) but flag it to the human if 1.35s feels long in playtesting; `stageSettleSeconds` and `completeBeatSeconds` are independent knobs on two different components.

### C) SfxLibrary row

`Assets/Audio/Data/SfxLibrary.asset` needs one entry: `Id = StickerPeel`, `Clip = Assets/Audio/(stickerPeel)freesound_community-egg-crack4-85848.mp3`, `Volume = 1`, `Pitch Min/Max = 0.95/1.05`. **This is added automatically**: `PosterAuthoringTool` calls `EnsureStickerPeelSfx()` at the end of any poster run that includes a sticker stage (both `Poster 2 Data` and `All Posters`), and only fills an empty row / adds a missing one — it never overwrites a clip the human already assigned. Manual fallback if ever needed: select `SfxLibrary.asset` → Entries → `+` → Id `StickerPeel` → drag the clip in → Volume 1 → Pitch 0.95/1.05.

## 4. Editor menu items (run in this order)

1. `Restorium/Fix Art Import Settings (Sprites)` — poster/close-up/sticker textures Full Rect + ASTC 6x6 (capped 2048 on Android), backgrounds ASTC 8x8, everything else UI settings; also transcodes `Assets/videos/*` to H.264.
2. `Restorium/Posters/Create or Update Poster 1 Data` (sets `completionCutscene = tracysc2.mp4`; keeps Poster1's existing GUIDs).
3. `Restorium/Posters/Create or Update Poster 2 Data` (creates `Assets/Data/Poster2/Poster02.asset` + `Stages/01_Dust`..`07_Mend`; adds the `SfxLibrary` StickerPeel row).
   - Or run `Restorium/Posters/Create or Update All Posters` instead of steps 2+3.
4. Both poster menus end by calling `CatalogBuilder.RebuildAll()` (SHOP agent's — rebuilds `PosterCatalog`/`ShopCatalog`/locale tables), so no separate step is needed for that.
5. Optional: `Restorium/Posters/Reset Poster 2 Sticker Positions (Figma defaults)` if the sticker positions ever get hand-dragged away from the Figma values and need resetting.
6. Optional: `Restorium/Posters/Add Sticker Peel Sound To SfxLibrary` to run the SFX-mapping step on its own.
7. After running these, select `Assets/Data/Poster2/Stages/02_Stickers` in the Project window to see/tweak the sticker preview (`StickerStageInspector`).

## 5. Localization keys added (`LocaleSource.Restoration.cs`)

| key | pt-BR | en |
|---|---|---|
| `stage.stickers.title` | Hora de descolar! | Peel them off! |
| `stage.stickers.prompt` | Parece que alguém já tentou consertar este cartaz... Toque em cada fita para descolá-la. | Looks like someone tried to fix this poster before... Tap each piece of tape to peel it off. |
| `poster.poster01.chapter` | Cap. 1 - Cartaz de turismo de Lethe Falls | Ch1 - Lethe Falls Tourism Poster |
| `poster.poster02.title` | Cine Eternal | Eternal Theatre |
| `poster.poster02.chapter` | Cap. 2 - Programação do Cine Eternal | Ch2 - Eternal Theatre Cinema Program |

`poster.poster01.title` and every `stage.<id>.title`/`.prompt` used by poster 2's Scrub stages (dust/wash/deacidify/squeegee/adhesive/mend) intentionally reuse the **existing MVP keys** the UI agent moved into `LocaleSource.Legacy.cs` — not duplicated here, per §8. **Please have a native speaker check `poster.poster02.title`/`.chapter`**: I drafted "Eternal Theatre" / a cinema programme from the poster 2 art (a theatre/cinema poster), which is a content guess, not something the contract specified — flag for human review.

## 6. Tests

`Assets/Scripts/Tests/EditMode/Logic/StickerLogicTests.cs` — 22 NUnit tests, no UnityEngine reference, three fixtures:
- `StickerLayoutTests` (10): `Fit` in Contain/Cover/degenerate cases, `NormalizedToLocal`/`LocalToNormalized` round-trip, `StickerRect.Contains`, and `DesignRectToNormalized_Poster2FigmaStickers` which pins the exact Figma-derived normalised values used by `PosterRecipes.Poster2StickerDefaults()` — a future change to that maths cannot silently move the stickers without failing this test.
- `StickerPeelTrackerTests` (6): reset, double-tap rejection, exactly-once completion flag, out-of-range indices, reset-after-peel, zero-count edge case.
- `StickerPeelMotionTests` (8): rest pose at t=0, lift end state, direction mirroring, monotonically accelerating fall, `DurationFor` round-trips through `Evaluate`/`IsFinished`, longer falls take longer, zeroed-struct never produces NaN/Infinity.

Also present and unchanged: `Assets/Scripts/Tests/EditMode/RevealMaskTests.cs` (pre-existing, scrub-stage coverage maths).

Both files are already covered by `Tools/logictests/LogicTests.csproj`'s wildcards (`Logic\**\*.cs`, `Tests\EditMode\Logic\**\*.cs`) — **no csproj edit was needed** (contract A6). Run via Unity's EditMode Test Runner, or `dotnet test Tools/logictests` — note `dotnet test` currently fails to build for the **whole shared project** because of other agents' in-flight files (`JournalPageRulesTests.cs`, `LocalePickerTests.cs`, `TutorialTriggerRulesTests.cs` reference `RestoriumEmporium.UI`/`Localization`/`Tutorial` types that don't exist yet) — this is the same pre-existing cross-agent build gap CORE's handoff already noted, not something introduced or fixable from RESTORATION's files. `sh Tools/compilecheck/check.sh` (which compiles against the real Unity DLLs, not `dotnet test`'s plain-C# subset) is clean except for the two UI-agent errors noted in §0.

## 7. Needs from others / open questions

- **ART agent**: `Docs/Batch2/FigmaLayout.md` does not exist yet in this worktree (checked at the start and again at the end of this session). The sticker positions in `PosterRecipes.Poster2StickerDefaults()` were **not guessed** — they come from actually reading the Figma file's "Poster2" section (close-up node at design coords (500, 3780), size 2304×4096; sticker1 at (1818, 4912), sticker2 at (1818, 6505), both 710×603, rotation baked into the art) and converting with `StickerLayoutMath.DesignRectToNormalized`, which is unit-tested. **Please cross-check these four numbers against `FigmaLayout.md`'s "Sticker removal" section once it exists** — if they disagree, either re-run `Restorium/Posters/Create or Update Poster 2 Data` after fixing the recipe, or just hand-drag the stickers in `StickerStageInspector` (Reset menu item reverts to the recipe values).
- **UI/TUTORIAL agent**: the `stickers` tutorial sequence (§6 of the contract) needs a `TutorialSequenceData` authored with trigger `StageKindStarted StickerPeel`, and a step gated on `GameSignals.StickerPeeled` with a `TapTarget`/hand pointing at anchor `sticker.next` (already registered/moved by `StickerRemovalScreen`). No tutorial data was authored here per contract (RESTORATION's tools must not create tutorial steps).
- **SHOP agent**: confirmed `CatalogBuilder.RebuildAll()` (namespace `RestoriumEmporium.EditorTools`, `Assets/Editor/CatalogBuilder.cs`) exists with the exact signature the poster tool calls.
- **LEAD**: none — no LEAD-owned type looked wrong or needed a flag.
- The prior agent's own note (now resolved): "Now the recipe file" in its cut-off state suggested `PosterRecipes.cs` was unfinished. On inspection it was complete and matches every field in contract §5's Poster 2 stage list (Dust/Stickers/Wash/Deacidify/Squeegee/Adhesive/Mend) and Poster 1 unchanged. I made no functional changes to it.

## 8. Optimisation ideas

- `StickerView.MeasureFallDistance()` walks up to the root canvas and does an `InverseTransformPoint` + a couple of divides on every `Peel()` call — that's at most twice per stage (2 stickers), so it is not worth pooling/caching further.
- `ArtImportSettingsTool` already batches every texture reimport inside one `StartAssetEditing`/`StopAssetEditing`; the video transcode pass (`FixVideos`) is not batched the same way since `VideoClipImporter` doesn't need it, but transcoding 3 clips is already slow (up to ~1 minute per the tool's own comment) — that's an inherent cost of H.264 transcoding, not something to optimize further here.
- Nothing else stood out; sticker rendering is 2 `Image`s with `preserveAspect` and a `RectTransform` sizeDelta/anchoredPosition/localRotation write per falling sticker per frame — well within the "no per-frame allocations" rule (no boxing, no LINQ, no `new` in `Update`).
