# SCENE BUILDER agent — Batch 2 handoff

## 0. What this is

`Assets/Editor/SceneBuilder/**` is an Editor-only tool, not a runtime feature. It does not ship in the
game; it exists so a human does not have to hand-build ~10 new/redesigned screens and rewire ~15
cross-agent components by dragging objects in the Inspector. It reads the four agents' handoffs and
`Docs/Batch2/FigmaLayout.md`, and turns them into two menu items.

Unity could not be run in this worktree, so none of this has been executed against the real scene. It is
written to be **correct by construction**: every field is set through `SerializedObject.FindProperty` (a
missing/renamed field is a loud, reported problem, never a silent no-op), every asset is loaded through
`AssetDatabase` (a missing sprite/font/catalog is reported by exact path), and every object is found by
exact name before being created (re-running never duplicates anything). It still needs a human to actually
run it once in Unity and fix whatever it reports.

## 1. Files created / changed

| File | What it does |
|---|---|
| `Assets/Editor/SceneBuilder/SceneBuilderCore.cs` (new) | Shared helpers every other file calls: find-or-create/rename-or-create a named child, the Figma-rect → RectTransform formula, Image/TMP text/Button/TutorialAnchor setup, and the `SerializedObject` field-assignment helpers (loud on a missing field). |
| `Assets/Editor/SceneBuilder/BuildCutscenePlayer.cs` (new) | Builds/updates the `CutscenePlayer` object (Canvas order 1000, topmost) per `CutscenePlayer.cs`'s own checklist and CORE.md §3. Returns the component so `BuildGameFlow` can wire `GameFlowController.cutsceneSource` in the same run. |
| `Assets/Editor/SceneBuilder/BuildOverlays.cs` (new) | Builds/updates `Overlays` (Canvas order 30) with its two children `PauseMenuOverlay` and `SettingsOverlay`, per `OverlayController.cs`/`PauseMenuOverlay.cs`/`SettingsOverlay.cs`'s checklists and FigmaLayout.md §1/§2. |
| `Assets/Editor/SceneBuilder/BuildHubTracyOverlay.cs` (new) | Builds/updates `HubTracyOverlay` (the full-body Tracy dialogue view, Canvas order 10) per `TracyOverlayView.cs`'s second checklist block. `catBoard.png` and the three full-body Tracy poses live here, not on `DeskHubScreen` (CONSISTENCY.md dispute a). |
| `Assets/Editor/SceneBuilder/BuildDeskHub.cs` (new) | Builds/updates `DeskHubScreen` (Normal/Edit/Preview mode groups, top bar, book button, room) per SHOP.md §3 and FigmaLayout.md §5/§7/§8/§9. |
| `Assets/Editor/SceneBuilder/BuildShop.cs` (new) | Builds/updates `ShopScreen` (header, tabs, balance row, scrollable card grid, card template) per SHOP.md §3 and FigmaLayout.md §6. |
| `Assets/Editor/SceneBuilder/BuildStickerRemoval.cs` (new) | Builds/updates `StickerRemovalScreen` per RESTORATION.md §3A (already complete instructions there). |
| `Assets/Editor/SceneBuilder/BuildJournalAndFinishedRepair.cs` (new) | Rewires the **existing** MVP `JournalScreen`/`FinishedRepairScreen` objects: renames old children onto their Batch 2 role (`PosterThumbnail`→`PosterImage`, `PosterTitle`→`TitleLabel`, `RestoreButton`→`ActionButton`, `PreviousPageButton`→`PrevPageButton`), retires `PageHint` (disabled, not deleted — no longer part of the design), adds every new child (`BackButton`, `LockedHint`, `ChapterLabel`, `CoinsLabel`, `DoubleButton`, `OrLabel`), and wires `FinishedRepairScreen.onShown (+) → CardFlipAnimator.Play()` via reflection + `UnityEventTools` (the one cross-file wiring step CONSISTENCY.md flagged as easy to miss — dispute c). |
| `Assets/Editor/SceneBuilder/BuildHamburgers.cs` (new) | Adds the pause hamburger (`PauseButtonObject`, last child) to `CleaningScreen`/`LinenBackingFrontScreen`/`LinenBackingBackScreen`/`LinenBackingFinalScreen` — the gap CONSISTENCY.md's fix #2 found and fixed in those screens' own checklists; this is where that checklist gets executed. |
| `Assets/Editor/SceneBuilder/BuildGameFlow.cs` (new) | Wires `GameFlow`'s components: forces `RestorationController.beginOnStart` to false (dispute b), sets `ScreenRouter.screens` to all 9 screens, wires `GameFlowController` (router/restoration/cutscene/posters/videos), builds the 13-entry `TutorialInputGate.gatedRoots` list (adding a `CanvasGroup` to every interactive area a currently-authored gated tutorial step can target), and wires `TutorialController` (4 sequences, both Tracy overlays, hand pointer, input gate, overlays, restoration/router sources). |
| `Assets/Editor/SceneBuilder/SceneBuilderMenu.cs` (new) | `Restorium/Scene/Build Batch 2 Scene Objects`. Validates the Game scene is open and not in Play mode, runs every `Build*` file above in one `Undo` group, then shows a summary dialog (created/updated/problem counts + the first 20 problems) and asks before saving. |
| `Assets/Editor/SceneBuilder/SceneBuilderValidate.cs` (new) | `Restorium/Scene/Validate Game Scene`. Read-only: walks every `RestoriumEmporium.*` `MonoBehaviour` in the open scene for null object-reference fields (against a documented allow-list of intentionally-optional fields), cross-checks every `TutorialStepData.targetAnchorId` against `TutorialAnchor` components actually in the scene (minus the runtime-spawned ids: `shop.item.*`, `preview.item`, `sticker.next`), and checks `ScreenRouter` covers every `GameScreen` a `RestorationStageData` routes to plus Journal/FinishedRepair/DeskHub/Shop. |
| `Assets/Editor/SceneBuilder/SceneBuilderManifest.cs` (new) | Hand-maintained mirror of every sprite path / `LocalizedText` key literal used across the `Build*.cs` files, so `SceneValidationTests.cs` can check them without Unity. See its own comment for the maintenance trade-off (add a new literal to a `Build*.cs` file → add it here too). |
| `Assets/Scripts/Tests/EditMode/SceneValidationTests.cs` (new) | NUnit/EditMode: every `SceneBuilderManifest` sprite path resolves via `AssetDatabase`, every key resolves in `LocaleSource.All()` with a non-empty pt-BR and en string, no duplicates in either list. Needs `UnityEditor`/`RestoriumEmporium.Editor` — see below. |
| `Assets/Scripts/Tests/EditMode/RestoriumEmporium.EditTests.asmdef` (changed) | Added `"RestoriumEmporium.Editor"` to `references` — `SceneValidationTests.cs` needs `LocaleSource`/`SceneBuilderManifest`, which live in the Editor assembly. Previously the EditMode test assembly only referenced Runtime. |

### A note on `Tools/compilecheck/EditTests.csproj`

That file is `git`-ignored (generated by Unity from the `.asmdef`s) and was already present in this
worktree. Adding the asmdef reference above means the *next* time a human opens the project in Unity it
will regenerate this file with a matching `ProjectReference` to `Editor.csproj` automatically. Since Unity
cannot run here, this file was hand-patched to add that same `ProjectReference` so `sh
Tools/compilecheck/check.sh` reflects the new dependency **right now**, without waiting for that
regeneration. This is a one-time, local-only convenience edit (the file isn't tracked by git) — nothing to
undo, but don't be surprised if Unity's own regeneration rewrites it (harmlessly, to the same effect) the
first time the project is opened there.

`sh Tools/compilecheck/check.sh` → **`COMPILE OK`**, no warnings. `dotnet test Tools/logictests` →
**`120/120`** passed (unaffected by this batch — no logic files were touched).

## 2. Human steps, in order

1. Open `Assets/Scenes/Game.unity` in Unity.
2. Run the **human menu run order** from `Docs/Batch2/CONSISTENCY.md` §"Human menu run order", steps
   1–5 (art import settings, poster data, starter shop items, locale/tutorial tables, final catalog
   rebuild). Every `Build*.cs` file above loads an asset those menus create — if one is skipped, the
   builder will still run, but will report every asset it couldn't find by its exact path instead of
   silently leaving a field empty.
3. `Restorium/Scene/Build Batch 2 Scene Objects`.
4. Read the summary dialog. For every "Problem" line: it names the exact object/field/asset path that
   needs attention. Most will be "asset not found" (an earlier menu wasn't run yet) — re-run that menu,
   then re-run step 3 (idempotent: nothing is duplicated, only updated).
5. `Restorium/Scene/Validate Game Scene`. This is read-only and safe to run any time; it does a second,
   independent pass (null-field scan across every component, tutorial-anchor coverage, screen coverage).
6. **Manually verify `RestorationController.beginOnStart` is unticked** on `GameFlow` (the builder forces
   this every run, but it's the one checkbox CONSISTENCY.md calls out as having "no compile-time signal if
   forgotten" — worth eyeballing once).
7. First-launch smoke test (from CONSISTENCY.md §8): delete `save.json`, press Play — expect `tracysc1` →
   Journal → poster 1 → reward → `tracysc2` → Finished Repair → Desk Hub (`deskhub_lamp` tutorial) → Shop
   → buy lamp → book → `openBookTransition` → Journal page 2 → poster 2 → stickers → Finished Repair →
   Desk Hub.

## 3. What this tool cannot do — must be done by hand

- **Nothing was executed.** Every wiring decision above is based on reading the actual component source
  code's `[SerializeField]` names and each handoff's own checklist, but it has never been run against the
  real `Game.unity` in a real Unity Editor. Expect to run it, read the Console for `[SceneBuilder]` lines,
  fix whatever's reported (usually a missing upstream asset), and re-run — that loop is the intended
  workflow, not a failure sign.
- **Purely decorative Figma nodes with no serialized field were skipped**, per the handoffs' own explicit
  permission to do so (sparkle stars, the scrollbar track/thumb on the Shop grid, the "Buy More
  Coins"/"Remove ads" play-triangle icons, the `enteredEditMode`/`PreviewMode`/`SelectedObjectToMove`
  "or" labels above certain buttons). None of these have a field anywhere that references them; add them
  by hand later if wanted, purely cosmetic.
- **`TutorialInputGate.gatedRoots`** covers every anchor a *currently-authored* gated tutorial step can
  target (13 `CanvasGroup`s — see `BuildGameFlow.cs`'s own comment for the exact list and reasoning). If a
  future tutorial step is authored with `gateInputToTarget = true` pointing at an anchor outside all 13,
  the gate fails open (per `TutorialInputGate`'s own documented design) rather than locking the player out
  — but it also means that new step won't actually narrow input the way it's supposed to until a
  `CanvasGroup` is added over its target and `gatedRoots` is grown to include it.
- **Journal's `PageHint`** ("Click to change pages") was disabled, not deleted, since it has no place in
  the Batch 2 design (arrows are simply enabled/disabled per `JournalPageRules` now). Delete it by hand
  once confirmed unneeded.
- **Font/video import settings, art compression, and the poster/tutorial/locale/catalog data assets** are
  all built by *other* agents' Editor menus (see step 2) — this tool only *wires the scene* to what those
  menus produce; it does not run them for you (the summary dialog tells you which one to run if something
  is missing).
- **The Title/ThanksForPlaying scenes** are untouched — Batch 2's scope is the Game scene only.

## 4. Known risks / things to double-check by hand

- **Coordinate math has not been visually verified.** Every position uses one formula
  (`SceneBuilderCore.FigmaRect`, documented in its own file) applied consistently against the handoffs'
  numbers, but a transcription slip in any one of the ~150 `FigmaRect`/`SetImage`/`SetupText` calls across
  the `Build*.cs` files would only show up as a slightly-off element in the Scene view, not a compile or
  runtime error. Worth a visual pass against `Docs/Batch2/screens/*.png` per screen after the first build.
- **`DeskHubScreen`/`ShopScreen`'s nested-panel coordinate math** (`EditHeader`/`PreviewHeader`/the two
  info panels) required subtracting each panel's own Figma origin from its children's absolute Figma
  coordinates by hand (documented inline in `BuildDeskHub.cs`) — this is the one place in the whole tool
  where a copy-paste-without-subtracting mistake was most likely; double-check those four panels first.
- **`FinishedRepairScreen.onShown`'s persistent listener** is added via reflection (the field is private,
  per that script's own deliberate design — see CONSISTENCY.md dispute c). If a human has already wired a
  *different* listener there by hand before running this tool, the tool leaves it alone and only *notes*
  that it did so (not a Problem) — check the Updated list if the reveal card doesn't flip.
- **Purely cosmetic colour/shape approximations**: every "shape: fill #rrggbb" node from FigmaLayout.md
  was built as a flat-colour `Image` with no border/rounded-corner treatment (Unity's default `Image` has
  neither); a few (`Panel`s in Edit/Preview mode, the pill-shaped buttons in Shop) would benefit from a
  human swapping in a proper 9-sliced sprite once the ART agent exports one, but nothing is broken without
  it — everything is opaque enough to read and tap correctly.
- **`Tools/compilecheck/EditTests.csproj`** was hand-patched (see §1 above) — this is expected to be
  superseded harmlessly the first time a human opens the project in Unity 6000.5.3f1.
- **The scene builder never opened Unity or exercised any code path.** Every claim above about field names
  and behaviour is based on reading the actual `.cs` source for every component it wires (listed in §1 of
  each other agent's own handoff, cross-referenced directly here) — but "read the source correctly" is not
  the same guarantee as "ran and it worked." Treat the first real run as a first real run.
