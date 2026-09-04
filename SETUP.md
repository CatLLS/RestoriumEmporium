# Restorium Emporium — Unity Setup Guide (MVP)

This is the build order for the MVP. Follow it top to bottom; each part assumes the ones above it are done.

**How this guide relates to the code.** Every script in `Assets/Scripts/` carries its own tickable checklist in a comment block at the top of the file, under `---- UNITY EDITOR SETUP ----`. That block is always the authoritative, exhaustive list for that one component. This document puts those blocks in the right **order**, removes the repetition, and adds the project-level steps that no single script could own. When a step here says "see the checklist in `X.cs`", open that file and read the top — it will have more detail than is worth repeating here.

**Target flow:** Title → Journal → Cleaning → Linen Backing → Finished Repair → Thanks For Playing.

---

## Part 0 — Project Settings

Menu: `Edit > Project Settings > Player`. Scroll to **Resolution and Presentation**.

| Setting | Value | Why |
|---|---|---|
| Default Orientation | **Portrait** | The whole game is designed at 412 × 917 portrait. |
| Allowed Orientations | untick **Landscape Right**, **Landscape Left**, **Portrait Upside Down** | These are currently all ticked. If they stay on, the phone will rotate the game into a layout that does not exist. |

Then **Other Settings**:

| Setting | Value | Why |
|---|---|---|
| Scripting Backend | IL2CPP | Already set. Required for ARM64. |
| Target Architectures | ARM64 only | Already set. Google Play requires 64-bit. |
| Target API Level | **35 or higher** | Currently "Automatic". Google Play rejects uploads that do not target a recent API level, and "Automatic" is not a guarantee. |
| Minimum API Level | 26 | Already set. Fine. |

Nothing else here needs changing. Frame rate and screen-sleep are handled in code by `GameBootstrap`.

---

## Part 1 — Importing the art

### 1.1 The one setting that breaks everything if you get it wrong

Select **every poster sprite** in `Assets/Art/Posters/poster1/` plus `Assets/Art/LinnenAssets/PosterBack`, and set in the Inspector:

- Texture Type = **Sprite (2D and UI)**
- Sprite Mode = **Single**
- **Mesh Type = Full Rect** ← this one
- Generate Physics Shape = off
- Wrap Mode = Clamp

Press **Apply**.

> **Why Full Rect matters.** The default, "Tight", crops the sprite's quad down to just the opaque pixels and re-maps its UV coordinates to fit. The reveal shader paints the scrub mask in those same UV coordinates. With "Tight", the mask lands somewhere other than under the player's finger and the scrubbing appears not to work at all. This is the single most likely cause of "dragging does nothing".
>
> For the same reason: **do not put these sprites into a Sprite Atlas.** Atlasing re-maps UVs into the atlas page and breaks it identically.

Do the same for the six tool icons in `Assets/Art/cleaningAssets/` and `Assets/Art/LinnenAssets/`.

There is a shortcut for this: menu **`Restorium > Fix Art Import Settings (Sprites)`**. Run it, then spot-check a couple of sprites by hand.

### 1.2 Compression (do this before you build an APK)

| Asset group | Max Size | Format | Mipmaps |
|---|---|---|---|
| Poster states (`Art/Posters/poster1/`) | 2048 | ASTC 6×6 | Off |
| Full-screen backgrounds (412 × 917 frames) | 4096 | ASTC 8×8 | Off |
| Tools, buttons, Tracy, hand icon | 2048 | ASTC 6×6 | Off |

Set these on the **Android tab** of the import settings (the little Android icon), not the Default tab. Mipmaps are pure waste for UI that is never viewed at an angle, and they add 33% to the texture memory.

### 1.3 Re-exporting from Figma

The poster sprites currently in the project were exported at a size that will look soft on a real phone, and they do not all share the same bounds. Re-export them from the Figma frame **`exportStation`** (node `410:21`):

1. Select the **`posters` frame** inside it — export the *frame*, not the individual layers. That is what makes all six states land on identical bounds so the cross-fades do not shift.
2. Toggle one poster layer visible at a time and export at **2x**.
3. Save into `Assets/Art/Posters/poster1/`, overwriting.

`posterFinal` is layer `410:32` in that frame — this pass also gets you the sprite that is currently missing.

---

## Part 2 — Fonts

1. Create the folder `Assets/Fonts/` (right-click in Project → Create → Folder).
2. Unzip your two font files into it.
3. Menu **`Window > TextMeshPro > Font Asset Creator`**.
4. Set **Source Font File** to your font.
5. Set **Character Set** to **Custom Range** and type: `20-7E,A0-FF`
6. Click **Generate Font Atlas**, then **Save** into `Assets/Fonts/`.
7. Repeat for the second font.

> **Why the custom range.** The default "ASCII" set stops at character 127, which excludes every accented letter. Portuguese needs `ç ã á é ê í ó õ ú à`. Without the `A0-FF` range those render as empty boxes, and you will not notice until a playtester sends you a screenshot of "Restaura□ão".

---

## Part 3 — Materials

Two materials, both from the custom shaders.

1. Right-click `Assets/Art` → **Create → Material**. Name it exactly **`M_PosterReveal`**.
   At the top of its Inspector set **Shader** to `Restorium/UI/PosterReveal`.
   Leave every other field alone — this asset is only a template; `PosterLayerStack` copies it at runtime.

2. Right-click `Assets/Art` → **Create → Material**. Name it exactly **`M_UISaturation`**.
   Set **Shader** to `Restorium/UI/Saturation`. Leave **Saturation** at 1.

> Quick sanity check without pressing Play: drag `M_UISaturation`'s Saturation slider to 0. Any icon using it turns grey in the Scene view. Drag it back to 1.

---

## Part 4 — Generate the data assets

This is the step that saves you about an hour of dragging sprites into fields.

Menu: **`Restorium > Create Poster 1 Data`**

It creates and wires, automatically:

- `Assets/Data/Tools/` — the six `ToolData` assets, icons already assigned
- `Assets/Data/Poster1/Stages/` — the six `RestorationStageData` assets with their from/to sprites, coverage thresholds and transitions
- `Assets/Data/Poster1/Poster01.asset` — the `PosterData`, stages in order
- `Assets/Data/Tutorial/` — the tutorial step assets, in order
- `Assets/Data/Localization/pt-BR.asset` — the full Portuguese string table

Then open `Window > General > Console` and read the summary line.

- If it warns about a **missing sprite**, it names the exact path it wanted. `posterFinal.png` will be missing until you do the Figma export in step 1.3 — that is expected. Re-run the tool after you add it.
- If it complains that a png **"is not imported as a Sprite"**, run `Restorium > Fix Art Import Settings (Sprites)` and then run this again.

**Re-running is safe.** The tool updates existing assets rather than creating duplicates.

---

## Part 5 — Scene: Title

You already built this as `Assets/Scenes/SampleScene.unity` — the mp4 background via Render Texture, the New Game button, and an audio object.

### 5.1 Rename the scene

In the Project window, click `SampleScene`, press **F2**, type **`Title`**, press Enter. Unity updates the Build Settings reference for you.

### 5.2 Your existing audio object still works

> **Reassurance:** the old `MenuAudioManager` script was upgraded in place into `AudioManager`. Its file GUID was deliberately preserved, so the scene still finds it, and the **Audio Mixer reference and any values you already set are intact**. Do **not** delete and re-add the component — you would lose them.

What you need to add to it is in the checklist at the top of `Assets/Scripts/Audio/AudioManager.cs`. In short:

1. Rename the object that carries it to exactly **`Systems`**.
2. Add three child objects, each with an **Audio Source**: `SfxSource`, `MusicSource`, `ToolLoopSource` (the file says exactly which boxes to tick on each).
3. Drag those three children into the matching fields on `Systems`.
4. Expose the mixer volumes: open `Assets/Audio/MainMixer`, right-click the **Volume** label on the Music group → Expose to script, same for SFX, then rename the two exposed parameters to exactly **`MusicVolume`** and **`SFXVolume`**. They are case-sensitive.

### 5.3 The rest of the Systems object

With `Systems` selected, **Add Component** for each of these (see each script's own checklist for its fields):

| Component | Key fields |
|---|---|
| `GameBootstrap` | Save Manager, Localization, Audio Service Source — all three are the `Systems` object itself |
| `SaveManager` | Leave `File Name` as `save.json` |
| `LocalizationService` | `Tables` size 1 → drag in `Assets/Data/Localization/pt-BR`; `Fallback Locale Code` = `pt-BR` |
| `SceneLoader` | Leave `Fade Seconds` at 0.25 |

Then drag `Systems` from the Hierarchy into `Assets/Prefabs/` to make it a prefab.

> **Do not add a second `Systems` to the other two scenes.** It survives scene loads on its own. A duplicate destroys itself on arrival, which works but is confusing.

### 5.4 The New Game button

Add a `TitleScreenController` and wire the button's `On Click ()` to it. Add a `ButtonSfx` component to the button for the click sound.

---

## Part 6 — Scene: Game

The big one. All four gameplay screens live in this single scene.

### 6.1 Canvas

Create the scene (`File > New Scene` → Basic 2D (URP) → save as `Assets/Scenes/Game.unity`), then:

1. `GameObject > UI > Canvas`.
2. On the **Canvas** component:
   - Render Mode = **Screen Space - Camera**
   - Render Camera = the **Main Camera** in the scene
3. Select that camera and set **Projection = Perspective**.
4. On the **Canvas Scaler** component:
   - UI Scale Mode = **Scale With Screen Size**
   - Reference Resolution = **412 × 917**
   - Screen Match Mode = Match Width Or Height, **Match = 0.5**

> **Why Screen Space - Camera and not Overlay.** Two things need it: the poster's 3D card flip (in Overlay mode the rotation renders flat, with no perspective), and the tool particles (Overlay has no depth for them to sort against). Both will silently look wrong if this is set to Overlay.

5. Make sure there is exactly one **EventSystem** in the scene, and that its component is **Input System UI Input Module**. This project has legacy input disabled — if it says "Standalone Input Module", click the "Replace with InputSystemUIInputModule" button on it, or nothing will respond to touch.

### 6.2 Screen layout coordinates

All positions are in the 412 × 917 reference space, top-left origin, as `(x, y) width × height`.

**Journal screen**
| Element | Position | Size |
|---|---|---|
| bg | full-frame | 412 × 917 |
| `paper 1` | (0, 175) | 412 × 541 |
| poster thumbnail | (120, 270) | 173 × 309 |
| Restore button | (104, 592) | 202 × 69 |
| right page arrow | (330, 787) | 31 × 59 |
| left page arrow | (82, 846) | 31 × 59 |
| "change pages" caption | (128, 810) | — |

The MVP has one page, so leave both arrows in place but visibly disabled.

**Cleaning screen**
| Element | Position | Size |
|---|---|---|
| bg | full-frame | 412 × 917 |
| desk | (−43, 848) | 498 × 779 |
| header text | (120, 62) | 172 × 24 |
| `toolsBarBG` | (0, 754) | 412 × 76 |
| poster stack | (45, 127) | 322 × 577 |
| dustRemover | (55, 733) | 66 × 140 |
| waterSpray | (161, 722) | 86 × 118 |
| deacidifier | (287, 733) | 58 × 115 |

Note the tools sit **higher** than the bar and overhang it — that is intentional, not a mistake in the numbers.

**Linen Backing — front**
| Element | Position | Size |
|---|---|---|
| header text | (98, 62) | — |
| `toolsBarBG` | (0, 748) | 412 × 76 |
| poster stack | (45, 127) | 322 × 577 |
| squeegee | (19, 759) | 120 × 121 |
| roller | (147, 748) | 136 × 135 |
| pencil | (315, 722) | 38 × 135 |

**Linen Backing — back**: `PosterBack` at (46, 117) 321 × 574; header at (110, 69). Same tool bar.

**Linen Backing — final**: linen frame at (19, 61) 375 × 638, with the poster at (47, 101) 318 × 567 inside it. This is the zoomed-in framing.

**Finished Repair**
| Element | Position | Size |
|---|---|---|
| `FinishedRepairBG` | full-frame | 412 × 917 |
| title text | (89, 128) | 235 × 20 |
| flip card | (65, 174) | 283 × 506 |
| Continue button | (105, 734) | 202 × 69 |

### 6.3 Build the poster object

This is the heart of the game. Full detail is in `Assets/Scripts/Restoration/PosterLayerStack.cs`. Structure:

```
Poster                (Image, Raycast Target ON, 322 × 577)
├── BottomLayer       (Image, stretch, Raycast Target OFF, Material = None)
└── TopLayer          (Image, stretch, Raycast Target OFF, Material = M_PosterReveal)
```

`TopLayer` must be **below** `BottomLayer` in the Hierarchy — uGUI draws later siblings on top.

On `Poster`, add:
- **Poster Layer Stack** — wire Poster Rect, Bottom Layer, Top Layer, Reveal Material, Canvas
- **Reveal Mask Painter** — wire Poster Surface to the same object, then **untick the component** so it starts disabled (the controller enables it only when the right tool is held)
- **Card Flip Animator** — Face Image = `BottomLayer`, Duration 0.5
- **Poster Zoomer** — with two inactive marker objects for the wide and zoomed framings
- **Tutorial Anchor** — Anchor Id `poster.surface`

### 6.4 The GameFlow object

`GameObject > Create Empty`, name it exactly **`GameFlow`**. Add:

| Component | Wiring |
|---|---|
| `ScreenRouter` | `Screens` → drag in the six screen root objects |
| `RestorationController` | Poster → `Poster01`; Poster Stack + Painter → the `Poster` object; Tools → the six `ToolData` assets |
| `GameFlowController` | Router → `GameFlow`; Restoration Source → `GameFlow`; Poster → `Poster01` |
| `TutorialController` | Steps → the tutorial assets in order |

Then wire the two buttons:
- Journal **Restore** button → `On Click ()` → `GameFlowController.StartRestoration()`
- FinishedRepair **Continue** button → `On Click ()` → `GameFlowController.GoToThanksForPlaying()`

Once the Journal screen exists, **untick `Begin On Start`** on `RestorationController` — the Restore button is what should start the poster.

### 6.5 Tool bars and buttons

One `ToolBarController` per screen that has tools. Each tool slot is a `ToolButton` whose icon Image uses the `M_UISaturation` material, plus a `TutorialAnchor` with the ids `toolbar.dustRemover`, `toolbar.waterSpray`, `toolbar.deacidifier`, `toolbar.squeegee`, `toolbar.roller`, `toolbar.pencil`.

**Sorting — get this right or the particles vanish:**

| Layer | Order in Layer |
|---|---|
| Main Canvas (poster) | 0 |
| Tool particles | 1 |
| `ToolBarRoot` (own Canvas, Override Sorting ticked) | 2 |

### 6.6 Particles

Build six particle prefabs — full recipe, including the exact RGB per tool, is in `Assets/Scripts/FX/ParticleBurstPool.cs`. The important settings: **Simulation Space = World** (otherwise the puffs follow the finger instead of being left behind), Play On Awake off, Emission off, Material = the built-in **`Default-Particle`** (no texture file needed — it ships with Unity), Order in Layer = 1.

Assign each prefab to its `ToolData`'s `Fx Prefab` field, then add a `ToolFx` object under the Canvas with `ToolFxController`.

### 6.7 Tutorial overlay

`TracyOverlayView` (scrim + the three Tracy portraits + dialogue box), `HandPointer` using `helpingHandIcon`, and `TutorialInputGate`. See their files for the field lists.

---

## Part 7 — Scene: ThanksForPlaying

| Element | Position | Size |
|---|---|---|
| BG | full-frame | 412 × 917 |
| Tracy portrait | (141, 74) | 130 × 164 |
| "O fim?" title | (100, 267) | 212 × 45 |
| divider line | (161, 336) | 90 wide |
| body text | (51, 360) | 311 × 307 |
| Quit button | (105, 738) | 202 × 69 |

Add `ThanksForPlayingScreen`. Every text label gets a `LocalizedText` component with its key — the strings are already in the `pt-BR` table.

---

## Part 8 — Build Settings

`File > Build Profiles` → Scene List. Three scenes, in this order:

| Index | Scene |
|---|---|
| 0 | `Title` |
| 1 | `Game` |
| 2 | `ThanksForPlaying` |

All three ticked. **Index 0 must be `Title`** — it owns the `Systems` object that every other scene depends on.

---

## Part 9 — Testing it

### Run the unit tests

`Window > General > Test Runner` → **EditMode** tab → **Run All**. These cover the scrubbing mask maths and the localization fallback. They need no scene setup, so you can run them right after the scripts compile.

### A correct first playthrough

1. Title → New Game.
2. Journal: Tracy greets you, the hand points at **Restaurar**.
3. Cleaning: hand points at the dust remover. Only that tool is in colour; the other two are grey. Pick it up, drag across the poster — dust puffs appear and the grime lifts. At ~85% the stage completes on its own.
4. Water spray, then deacidifier, the same way. After the deacidifier the tool bar swaps to the linen tools.
5. Squeegee → the poster flips over.
6. Roller on the back → a wet sheen spreads → the poster flips back, mounts on the linen and zooms in.
7. Pencil: swipe to erase the damaged layer and reveal the restored art. At 60% it finishes on its own.
8. Finished Repair: the before/after flip, then **Continuar**.
9. Thanks For Playing.

Kill the app at any point and relaunch — you should land back mid-restoration.

### Troubleshooting

| Symptom | Cause |
|---|---|
| **Dragging on the poster does nothing** | Almost always `Mesh Type` is still "Tight" instead of **Full Rect**. Second most likely: `Raycast Target` is off on the `Poster` Image, or the EventSystem is still using the old Standalone Input Module. |
| Poster renders solid white in the Scene view | Expected. The reveal mask is empty outside Play mode. |
| All tools are grey, none light up | `ToolBarController.Runtime` is not wired to the `GameFlow` object, or the `ToolData` ids do not match the stages' `Required Tool`. |
| Particles invisible, or drawn over the tool bar | Sorting orders. Canvas 0, particles 1, ToolBarRoot 2 with Override Sorting ticked. Also check `Plane Distance` on `ToolFxController` matches the Canvas. |
| Text shows raw keys like `ui.journal.restore` | The `pt-BR` table is not assigned to `LocalizationService.Tables`, or that key is not in it. |
| Accented letters show as boxes | The TMP font atlas was generated without range `A0-FF`. Regenerate it (Part 2). |
| The card flip looks flat | Canvas is on Screen Space - Overlay, or the UI camera is Orthographic instead of Perspective. |
| Hand pointer in the wrong place | The `TutorialAnchor.Anchor Id` does not match the step asset's `Target Anchor Id`. |

---

## Part 10 — What is still missing

Things only you can supply:

| Item | Where it goes | Effect if absent |
|---|---|---|
| **`posterFinal.png`** | `Assets/Art/Posters/poster1/` | The pencil stage has nothing to reveal. Everything else plays. |
| **Tool SFX clips** | `Assets/Audio/` → then into `SfxLibrary` | Silent tools. Empty slots are silent by design, never an error. |
| **Font files** | `Assets/Fonts/` | Text falls back to LiberationSans. |

None of these block you from playing through the loop.
