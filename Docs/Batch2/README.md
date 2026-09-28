# Batch 2 — what to do in Unity (start here)

Everything in this batch compiles, and 120 logic unit tests pass (saves/migration, coins, unlocks, shop purchases, room coordinates, stickers, journal rules, tutorial triggers, language picking). Unity itself could not be run on the build machine, so the steps below are the first time it runs inside the editor.

## Before merging
Your main checkout has **untracked copies** of `Assets/Art/Posters/poster2/`, `Assets/videos/` and `Assets/Audio/(stickerPeel)...mp3`. This branch commits the same files, so git will refuse to merge until you move or delete those three untracked copies. Close Unity first.

## Run once, in this order (Game scene open)
1. `Restorium > Fix Art Import Settings (Sprites)`: fixes sprites, ASTC compression and video transcoding.
2. `Restorium > Posters > Create or Update All Posters`: creates Poster 1 (now with the tracysc2 cutscene) and Poster 2 (with the sticker stage), and adds the sticker-peel sound to the SfxLibrary.
3. `Restorium > Shop > Create Starter Items`: creates Lamp (100), Plant Pot (50) and Book Pile (100).
4. `Restorium > Tutorial > Rebuild Tutorial Sequences`
5. `Restorium > Rebuild Catalogs & Locale Tables`: creates the English table next to pt-BR.
6. `Restorium > Scene > Build Batch 2 Scene Objects`: builds and wires every new screen and overlay. It is safe to re-run. Save when it asks.
7. `Restorium > Scene > Validate Game Scene`: should report nothing missing.
8. Check that `GameFlow > RestorationController > Begin On Start` is **unticked**.
9. Delete `save.json` (see SaveManager's checklist) and press Play from the **Title** scene. The expected sequence is: tracysc1 → Journal → poster 1 → +100 coins → tracysc2 → Finished Repair → Desk Hub (lamp tutorial) → Shop → buy lamp → book video → Journal page 2 → poster 2 → stickers → Finished Repair → Desk Hub.

Do a quick visual pass of each new screen against `Docs/Batch2/screens/*.png`. The layout numbers come from Figma, but nobody has seen them rendered yet. The DeskHub edit/preview panels are the likeliest to be a few pixels off.

## Adding content later
- **New shop item:** `Restorium > Shop > New Shop Item`. Fill in the id, price, icon, placed art, size, and name/description in both languages, then press Create. It appears in the shop automatically.
- **New poster:** add a recipe next to Poster 2's in `Assets/Editor/Posters/PosterRecipes.cs` and run it. The journal page and unlock order follow `journalOrder`.

## Where things are documented
`CONTRACT.md` is the architecture and flow. `CONSISTENCY.md` is the cross-agent audit. `handoff/*.md` has per-area detail and the strings marked DRAFT that need your review. `FigmaLayout.md` holds the layout.
