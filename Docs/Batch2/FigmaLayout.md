# Figma Layout Spec — Batch 2 (Art & Layout)

Figma file `liAY3FddBN7jtIHUvEs4Uu`, section `585:133` ("new stuff / alteratios"). All frames are 412×917, top-left origin, unless noted. Coordinates below are exactly as reported by Figma metadata relative to each frame's own top-left corner (i.e. already frame-local, not section-absolute). "Back to front" = listed in Figma z-order, first line = bottom-most.

Screenshots of every frame: `Docs/Batch2/screens/<frameName>.png`.

Legend for the Asset column:
- `path/to/file.png` — new export this session
- `path/to/file.png (reuse)` — already exists in the repo, this node reuses it as-is
- `shape: ...` — flat/gradient vector, not exported; recreate in Unity (Image w/ color, or a 9-slice)
- `text` — see the Text sub-row for string/font/size/colour/alignment
- `baked-in` — pixel content already included inside a previously-exported composite background

---

## 1. GamePausedOverlay (`283:177`)

Base fill: `rgba(30,9,12,0.8)` (dark scrim, sits over a screenshot of the paused game).

| # | Purpose | Node | Asset | x | y | w | h | Rot | Op |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Decorative corner sparkle (top-left) | 391:67 › 300:331 | baked-in → `Assets/Art/GamePausedOverlay/gamePausedBG.png` | -66 | -25 | 302 | 190 | -24.94° | 100% |
| 2 | Decorative corner sparkle (bottom-right) | 391:67 › 300:339 | baked-in → same file | 175 | 483 | 282 | 822 | 96.88° | 100% |
| 3 | Sleeping Tracy portrait | 391:67 › 313:368 | baked-in → same file | 105 | 149 | 211 | 264 | 0 | 100% |
| 4 | Outer border | 391:67 › 300:327 | baked-in (shape: 1px stroke `#c7c8bc`, no fill) | 13 | 16 | 386 | 885 | 0 | 100% |
| 5 | Divider line under title | 391:67 › 300:329 | baked-in (line) | 113 | 144 | 185 | ~1 | 0.3° | 100% |
| 6 | Inner border | 391:67 › 300:317 | baked-in (shape: 1px stroke `#c7c8bc`, no fill) | 29 | 33 | 355 | 852 | 0 | 100% |
| 7 | "GAME PAUSED" | 300:313 | text | 98 | 113 | 216 | 30 | 0 | 100% |
| 8 | "Journal" (button label) | 300:303 | text | 128 | 563 | 156 | 20 | 0 | 100% |
| 9 | "Settings" (button label) | 300:305 | text | 98 | 601 | 216 | 20 | 0 | 100% |
| 10 | "Quit" (button label) | 300:307 | text | 98 | 639 | 216 | 20 | 0 | 100% |
| 11 | Footer flavour text | 300:311 | text | 50 | 743 | 311 | 47 | 0 | 100% |
| — | "Continue Restoration" button + label | 300:299 / 300:301 | **hidden in Figma**, unused | 83 | 419 | 246 | 79 | — | — |

Text detail:
| Node | String | Font | Weight | Size | Colour | Align |
|---|---|---|---|---|---|---|
| 300:313 | GAME PAUSED | Rye | Regular | 24 | `#c7c8bc` | center |
| 300:303 | Journal | Special Elite | Regular | 20 | `#c7c8bc` | center |
| 300:305 | Settings | Special Elite | Regular | 20 | `#c7c8bc` | center |
| 300:307 | Quit | Special Elite | Regular | 20 | `#c7c8bc` | center |
| 300:311 | Take your time,\nMysteries that lie behind these papers shall wait for your return. | Special Elite | Regular | 16 | `#c7c8bc` | center |

Nothing new to export here — `gamePausedBG.png` (already exported by the previous agent) already bakes in the border, both sparkle corners, the sleeping-Tracy art and the divider line.

---

## 2. SettingsScene (`300:298`)

Base fill: `#130607`.

| # | Purpose | Node | Asset | x | y | w | h | Rot | Op |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Bookshelf background | 637:128 | `Assets/Art/Settings/bookshelfBG.png` (new) | -62 | 259 | 523 | 800 | 0 | 100% |
| 2 | Tracy portrait photo | 217:83 | `Assets/Art/Settings/tracyPortrait.png` (new) | 141 | 74 | 130 | 164 | 0 | 100% |
| 3 | Outer border | 315:389 | shape: 1px stroke `#c7c8bc`, no fill | 13 | 16 | 386 | 885 | 0 | 100% |
| 4 | Inner border | 315:388 | shape: 1px stroke `#c7c8bc`, no fill | 29 | 34 | 355 | 852 | 0 | 100% |
| 5 | "SETTINGS" title | 315:391 | text | 100 | 254 | 212 | 45 | 0 | 100% |
| 6 | Back button | 315:395 | `Assets/Art/UI/backArrowIcon.png` (reuse) | 29 | 39 | 58.6 | 55 | 180° | 100% |
| 7 | Flavour text (2 lines) | 315:399 | text | 54 | 365 | 311 | 28 | 0 | 100% |
| 8 | Portrait frame border | 315:401 | shape: 1px stroke `#c7c8bc`, no fill | 136 | 69 | 140 | 174 | 0 | 100% |
| 9 | "Language" label | 315:404 | text | 107 | 440 | 71 | 15 | 0 | 100% |
| 10 | Language pill | 315:405 | shape: fill `#000000`, border 2px `#c7c8bc`, radius 6 | 200 | 434 | 112 | 26 | 0 | 100% |
| 11 | "English" value | 315:407 | text | 220 | 440 | 62 | 15 | 0 | 100% |
| 12 | Dropdown arrow | 315:408 | `Assets/Art/Settings/dropdownArrow.png` (reuse) | 302 | 444 | 15 | 8.6 | 90° | 100% |
| 13 | "SFX Audio" label | 590:135 | text | 81 | 503 | 76 | 15 | 0 | 100% |
| 14 | SFX track | 590:136 | shape: fill `#000000`, border 2px `#c7c8bc`, radius 6 | 178 | 505 | 156 | 12 | 0 | 100% |
| 15 | SFX fill | 593:22 | shape: fill `rgba(199,200,188,0.76)`, radius 8 | 180 | 506 | 75 | 10 | 0 | 100% |
| 16 | SFX handle base | 590:140 | shape: fill `rgba(199,200,188,0.74)`, thick `#c7c8bc` ring, radius 9 | 245 | 501 | 22 | 20 | 0 | 100% |
| 17 | SFX handle dot | 593:20 | shape: fill `#d9d9d9`, radius 9 | 250 | 506 | 11 | 10 | 0 | 100% |
| 18 | "Music Audio" label | 593:24 | text | 72 | 564 | 94 | 15 | 0 | 100% |
| 19 | Music track | 593:25 | shape: fill `#000000`, border 2px `#c7c8bc`, radius 6 | 178 | 566 | 156 | 12 | 0 | 100% |
| 20 | Music fill | 593:31 | shape: fill `rgba(199,200,188,0.76)`, radius 8 | 178 | 567 | 75 | 10 | 0 | 100% |
| 21 | Music handle base | 593:26 | shape: fill `rgba(199,200,188,0.74)`, thick `#c7c8bc` ring, radius 9 | 245 | 562 | 22 | 20 | 0 | 100% |
| 22 | Music handle dot | 593:27 | shape: fill `#d9d9d9`, radius 9 | 250 | 567 | 11 | 10 | 0 | 100% |

Text detail:
| Node | String | Font | Size | Colour | Align |
|---|---|---|---|---|---|
| 315:391 | SETTINGS | Rye | 36 | `#c7c8bc` | center |
| 315:399 | Oh, the restorium needs adjusting?\nMy bad, let's change it right away! | Special Elite | 16 | `#c7c8bc` | center |
| 315:404 | Language | Special Elite | 15 | `#ffffff` | left |
| 315:407 | English | Special Elite | 15 | `#ffffff` | center |
| 590:135 | SFX Audio | Special Elite | 15 | `#ffffff` | left |
| 593:24 | Music Audio | Special Elite | 15 | `#ffffff` | left |

All slider parts (10, 11, 13–22) are flat/translucent rounded rectangles — no raster needed, build as `Image` components with the colours above.

---

## 3. FinishedRepairBG (`342:497`)

| # | Purpose | Node | Asset | x | y | w | h | Rot | Op |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Radial dark-red background | 342:503 | `Assets/Art/FinishedRepairBG.png` (reuse) | 0 | 0 | 412 | 917 | 0 | 100% |
| 2 | Corner sparkle beam (top-left) | 342:509 | baked-in → same file | -232 | -110 | 528 | 339 | -25.78° | 100% |
| 3 | Corner sparkle beam (bottom-right) | 342:510 | baked-in → same file | 185 | 468 | 282 | 822 | 96.88° | 100% |
| 4 | "Restoration Complete!" | 342:515 | text | 55 | 100 | 309 | 80 | 0 | 100% |
| 5 | Frame drop shadow | 454:35 | shape: soft shadow, no export (see note) | 129 | 245 | 165 | 327 | 0 | ~30% |
| 6 | Linen/wood frame | 454:36 | `Assets/Art/LinnenAssets/linnenBacking.png` (reuse) | 108 | 230 | 202 | 344 | 0 | 100% |
| 7 | Poster art (per-poster, dynamic) | 454:37 | `Assets/Art/Posters/<poster>/posterDry.png` (reuse, dynamic) | 123 | 252 | 171 | 305 | 0 | 100% |
| 8 | Star sparkle (large) | 462:70 | `Assets/Art/UI/starGold.png` (reuse) | 76 | 564 | 36 | 50 | 0 | 100% |
| 9 | Star sparkle (small) | 462:72 | `Assets/Art/UI/starGold.png` (reuse) | 107 | 593 | 10.5 | 16.1 | 0 | 100% |
| 10 | Star sparkle (large) | 462:74 | `Assets/Art/UI/starGold.png` (reuse) | 314 | 211 | 17 | 22.9 | 0 | 100% |
| 11 | Star sparkle (small) | 462:75 | `Assets/Art/UI/starGold.png` (reuse) | 322 | 230 | 10.5 | 16.1 | 0 | 100% |
| 12 | "+100" reward text | 462:77 | text | 180 | 607 | 58 | 30 | 0 | 100% |
| 13 | Coin icon | 462:78 | `Assets/Art/UI/coinIcon.png` (reuse) | 154 | 613 | 26 | 23 | 0 | 100% |
| 14 | "Double Reward" button base | 342:506 | `Assets/Art/newGameButton.png` (reuse) + gold border `#f5ba55` 4px, radius 21, **outer glow effect not exported** (gold blur, see note) | 76 | 659 | 257 | 63 | 0 | 100% |
| 15 | "Double Reward" label | 342:507 | text | 117 | 672 | 208 | 24 | 0 | 100% |
| 16 | Play triangle icon | 463:25 | shape: filled polygon, `#f5ba55` | 123 | 677 | 26 | 28 | 90° | 100% |
| 17 | "Ch1 - Lethe Falls Turism Poster" subtitle | 463:28 | text | 81 | 196 | 245 | 15 | 0 | 100% |
| 18 | "Continue" button base | 462:82 | `Assets/Art/newGameButton.png` (reuse) + dark border `#444` 4px, radius 21 | 120 | 788 | 161 | 51 | 0 | 100% |
| 19 | "Contiune" label (sic — verbatim typo in Figma) | 462:83 | text | 126.6 | 793 | 148.8 | 41 | 0 | 100% |
| 20 | "or" | 463:29 | text | 187 | 738 | 28 | 42 | 0 | 100% |

Text detail:
| Node | String | Font | Size | Colour | Align |
|---|---|---|---|---|---|
| 342:515 | Restoration Complete! | Rye | 32 | `#c7c8bc` | center |
| 462:77 | +100 | Rye | 24 | `#ffffff` | center |
| 342:507 | Double Reward | Special Elite | 24 | `#f5ba55` | center |
| 463:28 | Ch1 - Lethe Falls Turism Poster (verbatim, incl. "Turism" typo) | Special Elite | 15 | `#a1c9c1` | center |
| 462:83 | Contiune (verbatim typo) | Special Elite | 24 | `#c7c8bc` | center |
| 463:29 | or | Special Elite | 24 | `#c7c8bc` | center |

Note: node 454:35 "shadow" is a Figma layer-blur drop shadow (`box-shadow: -10px 12px 14.1px 21px rgba(0,0,0,0.3)`), not a raster — recreate with a soft shadow sprite/Unity UI shadow if wanted, otherwise skip (barely visible under the frame). The gold glow around "Double Reward" is a Figma effect (outer glow), also not exported; either add a `UnityEngine.UI.Shadow`/glow sprite behind the button or ignore.

---

## 4. Cleaning screen (`196:52`)

| # | Purpose | Node | Asset | x | y | w | h | Rot | Op |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Outer rounded panel | 460:16 | shape: rounded rect, border 4px `#c7c8bc`, radius 21 (texture fill mostly hidden under desk layer above it — see note) | -15 | -13 | 450 | 941 | 0 | 100% |
| 2 | Desk background (wood + red vignette) | 474:109 | `Assets/Art/cleaningAssets/deskBG.png` (new) | 0 | 126 | 421 | 703 | 0 | 100% |
| 3 | Poster, dusty state | 344:525 | `Assets/Art/Posters/<poster>/posterBeforeDusting.png` (reuse, dynamic) | 67 | 195 | 278 | 497 | 0 | 100% |
| 4 | Poster, white-wet state (hidden by default) | 349:552 | `Assets/Art/Posters/<poster>/posterWhiteWet.png` (reuse, dynamic) | 67 | 195 | 278 | 498 | 0 | 0%(hidden)|
| 5 | Tools tray background | 248:33 | `Assets/Art/cleaningAssets/toolsBarBG.png` (new) | 0 | 754 | 412 | 76 | 0 | 100% |
| 6 | Water spray bottle | 258:89 | `Assets/Art/cleaningAssets/waterSpray.png` (reuse) | 161 | 722 | 86 | 118 | 0 | 100% |
| 7 | Deacidifier bottle | 265:96 | `Assets/Art/cleaningAssets/deacidifier.png` (reuse) | 287 | 733 | 58 | 115 | 0 | 100% |
| 8 | Dust remover brush | 265:98 | `Assets/Art/cleaningAssets/dustRemover.png` (reuse) | 55 | 733 | 66 | 140 | 0 | 100% |
| 9 | "Time to clean!" | 164:140 | text | 114 | 90 | 180 | 30 | 0 | 100% |
| 10 | Hamburger menu icon | 474:110 | `Assets/Art/UI/hamburgerIcon.png` (reuse) | 14 | 38 | 53 | 57 | 0 | 100% |
| 11 | Desk edge shadow vector | 460:27 | shape: soft vector shadow overlay, skip export | -86 | 82 | 499 | 659 | 0 | ~low |
| 12 | Banker's lamp fixture | 460:25 | `Assets/Art/cleaningAssets/lampFixture.png` (new) | 412 | 38 | 145 | 162 | 180°(flip Y)| 100% |
| 13 | Compass | 474:118 | `Assets/Art/cleaningAssets/compass.png` (new) | 345 | 200 | 61 | 66 | 0 | 100% |
| 14 | Pen + inkwell | 474:124 | `Assets/Art/cleaningAssets/pen.png` (new) | 283 | 129 | 78 | 59 | 0 | 100% |
| 15 | Magnifying glass | 479:130 | `Assets/Art/cleaningAssets/magnifyingGlass.png` (new) | 35 | 115 | 89 | 146 | 8.96° | 100% |

Text detail: `164:140` "Time to clean!" — Rye, 24, `#c7c8bc`, left.

Note: `deskBG.png` exports at 824×1684px (vs. the nominal 421×703 node box) because the wood texture has a soft feathered/blurred edge baked into the Figma layer bounds; place it centred on its declared box, the extra transparent margin is intentional bleed, not a bug.

---

## 5. Desk_Hub (`91:45`) — fully reused, no new exports

| # | Purpose | Node | Asset | x | y | w | h |
|---|---|---|---|---|---|---|---|
| 1 | Room background | 91:47 | `Assets/Art/DeskHub/zoomoutBG.png` (reuse) | 0 | 0 | 412 | 917 |
| 2 | Cat/evidence board | 132:2 | `Assets/Art/DeskHub/catBoard.png` (reuse) | 261 | 67 | 146 | 249 |
| 3 | Book (desk hub prop) | 114:6 | `Assets/Art/DeskHub/book.png` (reuse) | 151 | 457 | 125 | 115 |
| 4 | Tracy (still pose) | 122:11 | `Assets/Art/DeskHub/tracyStill.png` (reuse) | 0 | 256 | 236 | 661 |
| 5 | Tracy (happy, hidden variant) | 275:131 | `Assets/Art/DeskHub/tracyTalkingHappyNormal.png` (reuse) | -36 | 155 | 307 | 762 |
| 6 | Tracy (embarrassed, hidden variant) | 275:128 | `Assets/Art/DeskHub/tracyTalkingEmbarassed.png` (reuse) | -36 | 150 | 312 | 775 |
| 7 | Dialogue box (incl. "TRACY" label baked in) | 93:56 | `Assets/Art/UI/dialogueRect.png` (reuse) | 36 | 627 | 340 | 163 |
| 8 | Hamburger icon | 93:52 | `Assets/Art/UI/hamburgerIcon.png` (reuse) | 248 | 34 | 53 | 57 |
| 9 | Coin pill background | 612:171 | `Assets/Art/UI/coinShowerBG.png` (reuse) | 301 | 36 | 106 | 53 |
| 10 | Balance text "120" | 612:169 | text | 354 | 52 | 26 | 18 |
| 11 | Bag button base | 481:136 | shape: fill `#592e1e` (mostly covered by icon 12) | 30 | 45 | 37 | 38 |
| 12 | Bag icon | 136:8 | `Assets/Art/UI/bagIcon.png` / `bagButton.png` (reuse) | 11 | 25 | 79 | 84 |
| 13 | Edit-mode button base | 612:192 | `Assets/Art/UI/editModeButtonBase.png` (reuse) | 96 | 41 | 44 | 40 |
| 14 | Move icon | 612:188 | `Assets/Art/UI/moveIcon.png` (reuse) | 105 | 49 | 26 | 24 |

Text: `612:169` "120" — Rye, 14, `#000000`, left/baseline.

---

## 6. Shop (`455:42`)

| # | Purpose | Node | Asset | x | y | w | h | Rot |
|---|---|---|---|---|---|---|---|---|
| 1 | Screen fill | 458:8 | shape: flat `#1f0b07` | -9 | 0 | 422 | 785 | 0 |
| 2 | Bookshelf header art | 458:12 | `Assets/Art/Shop/shopBookshelfBG.png` (reuse) | 16 | 48 | 380 | 336 | 0 |
| 3 | Header curved panel | 458:15 | `Assets/Art/Shop/shopHeaderPanel.png` (reuse) | 15.5 | 0 | 380.5 | 157.3 | 0 |
| 4 | Leather card background | 458:38 | `Assets/Art/Shop/leatherBG.png` (reuse), border 4px `#c7c8bc`, radius 21 | -14 | 359 | 441 | 567 | 0 |
| 5 | Item grid panel base | 455:46 | `Assets/Art/Shop/itemGridPanel.png` (reuse) (fallback shape: fill `#44151c`, border 5px `#612e17`, radius 13) | 26 | 411 | 362 | 475 | 0 |
| 6 | "Emporium Shop" title | 456:7 | text | 43.6 | 94.7 | 301.9 | 34.3 | 0 |
| 7 | "Tracy's" subtitle | 458:19 | text | 59.5 | 82.7 | 73.5 | 17.1 | 0 |
| 8 | Title sparkle star | 458:35 | `Assets/Art/UI/starGold.png` (reuse) | 319 | 64 | 45 | 59 | 0 |
| 9 | Tracy (shop pose) | 458:17 | `Assets/Art/Shop/tracyShop.png` (reuse) | 208 | 140 | 208 | 253 | 0 |
| 10 | Back button | 458:28 | `Assets/Art/UI/backArrowIcon.png` (reuse) | -4 | 21 | 58.6 | 55 | 180° |
| 11 | "Go back to workbench" | 458:29 | text | 37 | 43 | 152 | 14 | 0 |
| 12 | "Misc." tab pill | 497:34 | `Assets/Art/Shop/categoryTab.png` (reuse), fill `#592e1e`, border `#612e17` | 117 | 392 | 87 | 20 | 0 |
| 13 | "Misc." tab star (inactive) | 497:35 | `Assets/Art/UI/tabStarInactive.png` / `starOrange.png` (reuse) | 122.6 | 394.8 | 16.8 | 15 | 0 |
| 14 | "Misc." label | 497:36 | text | 148.9 | 391 | 38.3 | 20 | 0 |
| 15 | Scrollbar track | 460:14 | shape: fill `#1f0b07`, radius 5 | 357 | 430 | 11 | 442 | 0 |
| 16 | Lamp card base | 585:37 | shape: fill via card texture, border 1px `#c7c8bc`, radius 11 | 151 | 436 | 82 | 107 | 0 |
| 17 | Lamp card slot | 585:38 | shape: fill `#250d0b`, radius 7 | 155 | 441 | 73 | 81 | 0 |
| 18 | Lamp card art | 603:37 (`brush 21`) | `Assets/Art/ShopItems/lamp/icon.png` (new) | 164 | 425 | 53 | 102 | 0 |
| 19 | Lamp price "100" | 585:41 | text | 180 | 520 | 29 | 19 | 0 |
| 20 | Lamp price coin icon | 585:43 | `Assets/Art/UI/coinIcon.png` (reuse) | 171 | 525 | 12.5 | 12.3 | 0 |
| 21 | Books card base | 585:81 | shape (as #16) | 45 | 436 | 82 | 107 | 0 |
| 22 | Books card slot | 585:82 | shape (as #17) | 49 | 441 | 73 | 81 | 0 |
| 23 | Books card art | 610:70 (`brush 30`) | `Assets/Art/ShopItems/books/icon.png` (new; same source as node 610:71) | 49 | 446 | 77 | 71 | 0 |
| 24 | Books price "100" | 585:85 | text | 88.6 | 520 | 29 | 19 | 0 |
| 25 | Books price coin icon | 585:87 | `Assets/Art/UI/coinIcon.png` (reuse) | 65 | 525 | 12.5 | 12.3 | 0 |
| 26 | Plant card base | 585:48 | shape (as #16) | 259 | 438 | 82 | 107 | 0 |
| 27 | Plant card slot | 585:49 | shape (as #17) | 263 | 443 | 73 | 81 | 0 |
| 28 | Plant card art | 605:53 (`brush 26`) | `Assets/Art/ShopItems/plant/icon.png` (new) | 272 | 440 | 64 | 85 | 0 |
| 29 | Plant price "50" | 585:52 | text | 288 | 522 | 29 | 19 | 0 |
| 30 | Plant price coin icon | 585:54 | `Assets/Art/UI/coinIcon.png` (reuse) | 279 | 527 | 12.5 | 12.3 | 0 |
| 31 | "Buy More Coins" pill | 489:21 | shape: fill `#44151c`, border 5px `#612e17`, radius 13 | 27 | 299 | 163 | 33 | 0 |
| 32 | "Buy More Coins" label | 497:38 | text ("Buy" in `#f5ba55`, rest white) | 33 | 307 | 155 | 20 | 0 |
| 33 | Balance pill | 470:16 | shape: fill `#44151c`, border 5px `#340203`, radius 13 | 27 | 192 | 163 | 33 | 0 |
| 34 | Balance "100" | 470:18 | text | 65 | 202 | 36 | 20 | 0 |
| 35 | Balance coin icon | 462:85 | `Assets/Art/UI/coinIcon.png` (reuse) | 32 | 197 | 26 | 23 | 0 |
| 36 | Balance "+" | 470:22 | text | 168 | 201 | 12 | 24 | 0 |
| 37 | Scrollbar thumb | 471:35 | shape: fill `#d19562`, border 4px `#612e17`, radius 7 | 356 | 441 | 14 | 49 | 0 |
| 38 | "Remove ads" pill | 489:14 | shape: fill `#44151c`, border 5px `#612e17`, radius 13 | 27 | 245 | 163 | 33 | 0 |
| 39 | "Remove ads" label | 489:16 | text | 63 | 254 | 112 | 20 | 0 |
| 40 | Play triangle icon | 489:17 | shape: polygon `#f5ba55`(ish) | 58 | 248 | 26 | 28 | 90° |
| 41 | "Décor" tab pill (active) | 458:62 | `Assets/Art/Shop/categoryTab.png` (reuse), fill `#592e1e`, border `#612e17` | 26 | 391 | 87 | 20 | 0 |
| 42 | "Décor" tab star (active) | 458:60 | `Assets/Art/UI/tabStarActive.png` / `starGold.png` (reuse) | 31.6 | 394.8 | 16.8 | 15 | 0 |
| 43 | "Décor" label | 458:59 | text | 57.9 | 391 | 38.3 | 20 | 0 |

Text detail:
| Node | String | Font | Size | Colour | Align |
|---|---|---|---|---|---|
| 456:7 | Emporium Shop | Rye | 32 | `#e4c191` | center |
| 458:19 | Tracy's | Special Elite | 20 | `#c7c8bc` | center |
| 458:29 | Go back to workbench | Special Elite | 14 | `rgba(199,200,188,0.74)` | left |
| 497:36 | Misc. | Special Elite | 13 | `#99805f` (inactive) | center |
| 585:41/85/52 | 100 / 100 / 50 | Special Elite | 13 | `#ffffff` | center |
| 497:38 | Buy More Coins | Special Elite | 20 | "Buy"=`#f5ba55`, rest `#ffffff` | left |
| 470:18 | 100 | Special Elite | 20 | `#ffffff` | left |
| 470:22 | + | Special Elite | 24 | `#f5ba55` | left |
| 489:16 | Remove ads | Special Elite | 20 | `#ffffff` | left |
| 458:59 | Décor | Special Elite | 13 | `#e4c191` (active) | center |

---

## 7. PreviewMode (`606:58`)

| # | Purpose | Node | Asset | x | y | w | h | Rot |
|---|---|---|---|---|---|---|---|---|
| 1 | Room background | 603:42 | `Assets/Art/DeskHub/zoomoutBG.png` (reuse) | 0 | -1 | 412 | 918 | 0 |
| 2 | Lamp, placed (lit) | 604:44 (`brush 24`) | `Assets/Art/ShopItems/lamp/placed.png` (reuse) | 12 | 296 | 94 | 180 | 0 |
| 3 | Plant, item being previewed (outlined) | 610:72 (`plantIcon`) | `Assets/Art/ShopItems/plant/icon.png` (reuse) | 250 | 468 | 138 | 182 | 0 |
| 4 | Books pile, placed (w/ candles, background) | 608:69 (`brush 29`) | `Assets/Art/ShopItems/books/placed.png` (reuse) | 207 | 323 | 205 | 138 | 0 |
| 5 | Bottom scrim | 610:74 | shape: fill `rgba(0,0,0,0.3)` | 0 | 632 | 412 | 302 | 0 |
| 6 | Info panel background | 610:75 | shape: fill `rgba(3,50,29,0.52)`, border 1px white | 18 | 640 | 376 | 220 | 0 |
| 7 | "Buy Item" button base | 610:77 | shape: fill `#620b1a`, border 5px `#612e17`, radius 13 | 45 | 796.7 | 121 | 36.7 | 0 |
| 8 | "Buy Item" label | 610:79 | text | 62 | 807 | 87 | 22.2 | 0 |
| 9 | "Give up" button base | 611:83 | shape: fill `#340203`, border 5px `#421f0f`, radius 13 | 245 | 796.7 | 121 | 36.7 | 0 |
| 10 | "Give up" label | 611:84 | text | 267 | 804.4 | 77 | 22.2 | 0 |
| 11 | Instruction text | 611:86 | text | 41 | 657 | 330 | 15.6 | 0 |
| 12 | Item description block | 611:87 | text (3 lines) | 41 | 685 | 347 | 93.3 | 0 |
| 13 | Header scrim | 611:89 | shape: fill `rgba(0,0,0,0.6)` | 0 | 79 | 413 | 61 | 0 |
| 14 | "Preview Mode" title | 612:90 | text | 52 | 98 | 164 | 24 | 0 |
| 15 | Coin pill background | 612:175 | `Assets/Art/UI/coinShowerBG.png` (reuse) | 272 | 83 | 106 | 53 | 0 |
| 16 | Balance text | 612:176 | text | 325 | 99 | 26 | 18 | 0 |
| 17 | Move icon | 612:93 | `Assets/Art/UI/moveIconLight.png` (reuse, light variant for dark bar) | 225 | 92 | 38 | 36 | 0 |

Text detail:
| Node | String | Font | Size | Colour | Align |
|---|---|---|---|---|---|
| 610:79 | Buy Item | Special Elite | 20 | `rgba(245,186,85,0.92)` | left |
| 611:84 | Give up | Special Elite | 20 | `#ffffff` | left |
| 611:86 | Click and drag around the item to position it | Special Elite | 14 | `#ffffff` | left |
| 611:87 | Selected: Plant Pot\nPrice: 50 coins\nDesc.: This pretty plant pot décor was found at a store near the workshop, it's really pretty! | Special Elite | 14 | `#c7c8bc` | left |
| 612:90 | Preview Mode | Special Elite | 24 | `#ffffff` | left |
| 612:176 | 120 (example balance) | Rye | 14 | `#000000` | left |

---

## 8. SelectedObjectToMove (`612:119`)

Same structural template as PreviewMode, for the **Books** item.

| # | Purpose | Node | Asset | x | y | w | h |
|---|---|---|---|---|---|---|---|
| 1 | Room background | 612:120 | `Assets/Art/DeskHub/zoomoutBG.png` (reuse) | 0 | -1 | 412 | 918 |
| 2 | Lamp, placed (lit) | 612:121 (`brush 24`) | `Assets/Art/ShopItems/lamp/placed.png` (reuse) | 12 | 296 | 94 | 180 |
| 3 | Books pile, selected/outlined sticker | 612:181 (`BookPileIcon`) | `Assets/Art/ShopItems/books/icon.png` (reuse — near-identical crop to the shop card art) | 231 | 328 | 142 | 139 |
| 4 | Bottom scrim | 612:124 | shape: fill `rgba(0,0,0,0.3)` | 0 | 632 | 412 | 302 |
| 5 | Info panel background | 612:126 | shape: fill `rgba(3,50,29,0.52)`, border 1px white | 18 | 640 | 376 | 220 |
| 6 | "Place Item" button base | 612:128 | shape: fill `#d19562`, border 5px `#612e17`, radius 13 | 45 | 796.7 | 152 | 36.7 |
| 7 | "Place Item" label | 612:129 | text | 66 | 804.4 | 109.3 | 22.2 |
| 8 | "Undo" button base | 612:131 | shape: fill `#340203`, border 5px `#421f0f`, radius 13 | 245 | 796.7 | 121 | 36.7 |
| 9 | "Undo" label | 612:132 | text | 267 | 804.4 | 77 | 22.2 |
| 10 | Instruction text | 612:133 | text | 41 | 657 | 330 | 15.6 |
| 11 | Item description block | 612:134 | text (2 lines) | 41 | 682 | 347 | 93.3 |
| 12 | Header scrim | 612:135 | shape: fill `rgba(0,0,0,0.71)` | 0 | 57 | 413 | 61 |
| 13 | "Edit Mode" title | 612:136 | text | 97 | 73 | 181 | 36 |
| 14 | Move icon | 612:137 | `Assets/Art/UI/moveIconLight.png` (reuse) | 291 | 70 | 38 | 36 |

Text detail:
| Node | String | Font | Size | Colour |
|---|---|---|---|---|
| 612:129 | Place Item | Special Elite | 20 | `#ffffff` |
| 612:132 | Undo | Special Elite | 20 | `#ffffff` |
| 612:133 | Click and drag around the item to position it | Special Elite | 14 | `#ffffff` |
| 612:134 | Selected: Book Pile\nDesc.: You've recently gotten into reading mystery books about time travel, so you got yourself a pile to keep at the workshop! | Special Elite | 14 | `#c7c8bc` |
| 612:136 | Edit Mode | Special Elite | 36 | `#ffffff` |

---

## 9. enteredEditMode (`612:98`)

| # | Purpose | Node | Asset | x | y | w | h |
|---|---|---|---|---|---|---|---|
| 1 | Room background | 612:99 | `Assets/Art/DeskHub/zoomoutBG.png` (reuse) | 0 | -1 | 412 | 918 |
| 2 | Lamp, placed (lit) | 612:100 (`brush 24`) | `Assets/Art/ShopItems/lamp/placed.png` (reuse) | 12 | 296 | 94 | 180 |
| 3 | Books pile, placed (w/ candles) | 612:102 (`brush 29`) | `Assets/Art/ShopItems/books/placed.png` (reuse) | 207 | 323 | 205 | 138 |
| 4 | Bottom scrim | 612:103 | shape: fill `rgba(0,0,0,0.3)` | 0 | 632 | 412 | 302 |
| 5 | Info panel background | 612:105 | shape: fill `rgba(3,50,29,0.52)`, border 1px white | 18 | 640 | 376 | 100 |
| 6 | Instruction text | 612:112 | text | 41 | 666 | 330 | 48 |
| 7 | Header scrim | 612:114 | shape: fill `rgba(0,0,0,0.71)` | 0 | 56 | 413 | 61 |
| 8 | "Edit Mode" title | 612:115 | text | 97 | 72 | 181 | 36 |
| 9 | Move icon | 612:116 | `Assets/Art/UI/moveIconLight.png` (reuse) | 291 | 69 | 38 | 36 |
| 10 | "Back to workshop" button base | 612:196 | `Assets/Art/newGameButton.png` (reuse), border 4px `#444`, radius 21 | 106 | 793 | 212 | 51 |
| 11 | "Back to workshop" label | 612:197 | text | 122 | 798 | 180 | 41 |
| 12 | "or" | 612:200 | text | 197 | 748 | 19 | 28 |

Text detail:
| Node | String | Font | Size | Colour |
|---|---|---|---|---|
| 612:112 | Select an item to edit its position. | Special Elite | 24 | `#ffffff` (center) |
| 612:115 | Edit Mode | Special Elite | 36 | `#ffffff` |
| 612:197 | Back to workshop | Special Elite | 20 | `#c7c8bc` (center) |
| 612:200 | or | Special Elite | 16 | `#ffffff` (center) |

---

## 10. JounalPage (`173:164`) — fully reused, no new exports

| # | Purpose | Node | Asset | x | y | w | h |
|---|---|---|---|---|---|---|---|
| 1 | Book background | 178:6 | `Assets/Art/journalAssets/bg.png` (reuse) | -2 | 0 | 414 | 917 |
| 2 | Paper page | 185:18 | `Assets/Art/journalAssets/paper 1.png` (reuse) | 0 | 175 | 412 | 541 |
| 3 | Entry heading | 190:19 | text | ~82 | 241 | 248 | 15 |
| 4 | Dashed poster placeholder frame | 190:21 | shape: dashed border `rgba(132,66,46,0.5)`, 5px | ~125 | 275 | 163 | 299 |
| 5 | Poster preview (30% opacity) | 342:517 | `Assets/Art/Posters/<poster>/posterBeforeDusting(30opacity,beforeRestoring).png` (reuse, dynamic) | 120 | 270 | 173 | 309 |
| 6 | Back button | 212:65 | `Assets/Art/UI/backArrowIcon.png` (reuse) | 14 | 60 | 58.6 | 55 |
| 7 | "Go back to workbench" | 212:69 | text | 55 | 81 | 152 | 14 |
| 8 | Next-page arrow | 246:6 | `Assets/Art/journalAssets/arrow.png` (reuse, mirrored) | 330 | 787 | 31 | 59 |
| 9 | Prev-page arrow | 246:7 | `Assets/Art/journalAssets/arrow.png` (reuse) | 82 | 846 | 31 | 59 |
| 10 | "Click to change pages" | 246:9 | text | 128 | 810 | 156 | 14 |
| 11 | "Restore" button base | 269:117 | `Assets/Art/journalAssets/buttonBase.png` (reuse), border 4px `#c7c8bc`, radius 21 | 104 | 592 | 202 | 69 |
| 12 | "Restore" label | 269:118 | text | 143 | 607 | 132 | 40 |

Text detail:
| Node | String | Font | Size | Colour |
|---|---|---|---|---|
| 190:19 | Level 1: Lethe falls tourism poster | Special Elite | 14 | `rgba(68,21,28,0.69)` |
| 212:69 | Go back to workbench | Special Elite | 14 | `#a1c9c1` |
| 246:9 | Click to change pages | Special Elite | 14 | `#a1c9c1` |
| 269:118 | Restore | Rye | 32 | `#c7c8bc` |

Journal back button (212:65) uses the same `icn arrow-left .icn-xs` component as Settings/Shop — confirmed identical to `Assets/Art/UI/backArrowIcon.png`, no separate export needed.

---

## 11. BookTransition (`168:157`)

| # | Purpose | Node | Asset | x | y | w | h |
|---|---|---|---|---|---|---|---|
| 1 | Full-bleed book illustration | 110:91 | `Assets/Art/BookTransition/bookTransitionBG.png` (new) | 0 | 0 | 417 | 917 |

No text on this frame. Note: `Assets/videos/openBookTransition.mp4` already exists and is wired as `GameFlowController.bookOpenCutscene` — this static frame is likely the reference art the video was made from, or a fallback/loading frame; exported in case a static image is still needed (e.g. as a poster/first-frame image or loading placeholder).

---

## 12. RemoveAds (`615:204`) — `Assets/Art/Monetization/`

| # | Purpose | Node | Asset | x | y | w | h |
|---|---|---|---|---|---|---|---|
| 1 | Bookshelf background | 620:212 | `Assets/Art/Monetization/bookshelfBG.png` (new) | -76 | 0 | 523 | 930 |
| 2 | Lower scrim | 622:234 | shape: fill `rgba(0,0,0,0.62)` | 0 | 321 | 412 | 596 |
| 3 | "The Quiet Workshop" title | 622:233 | text | 32 | 402 | 347 | 40 |
| 4 | "One Purchase - Forever" | 622:235 | text | 87 | 350 | 238 | 20 |
| 5 | Divider line | 622:236 | shape: 1px line | 174 | 390 | 64 | 0 |
| 6 | Divider line | 622:237 | shape: 1px line | 174 | 459 | 64 | 0 |
| 7 | Close "X" | 622:242 | text — **font `Quicksand Regular`, not in project TMP set** | 24 | 35 | 15 | 30 |
| 8 | Body copy (2 paragraphs) | 623:18 | text | 22 | 503 | 368 | 84 |
| 9 | "Get Package" button base | 623:20 | `Assets/Art/newGameButton.png` (reuse), border 4px `#f5ba55`, radius 21 | 78 | 676 | 257 | 63 |
| 10 | "Get Package | $1.99" label | 623:21 | text | 87 | 689 | 240 | 24 |
| 11 | Fine print | 623:25 | text | 42 | 771 | 329 | 28 |

Text detail:
| Node | String | Font | Size | Colour | Align |
|---|---|---|---|---|---|
| 622:233 | The Quiet Workshop | Rye | 32 | `#c7c8bc` | left |
| 622:235 | One Purchase - Forever | Special Elite | 20 | `#f5ba55` | left |
| 622:242 | X | **Quicksand** | 24 | `#ffffff` | left |
| 623:18 | No ads, anywhere in the game. \n\n x2 coins, every restoration pays double, without the button. | Special Elite | 20 | "No ads"/"x2 coins"=`#f5ba55`, rest `#c7c8bc` | center |
| 623:21 | Get Package \| $1.99 | Special Elite | 24 | `#f5ba55` | center |
| 623:25 | charged to your account. Restore purchases any time from Settings | Special Elite | 14 | `#c7c8bc` | center |

---

## 13. BuyCoins (`623:26`) — `Assets/Art/Monetization/`

| # | Purpose | Node | Asset | x | y | w | h | Rot |
|---|---|---|---|---|---|---|---|---|
| 1 | Bookshelf background | 623:27 | `Assets/Art/Monetization/bookshelfBG.png` (reuse, same asset as RemoveAds — confirmed same crop/size) | -76 | 0 | 523 | 930 | 0 |
| 2 | Close "X" | 623:33 | text — **font `Quicksand Regular`** | 24 | 35 | 15 | 30 | 0 |
| 3 | Coin pill background | 624:42 | `Assets/Art/UI/coinShowerBG.png` (reuse) | 282 | 32 | 106 | 53 | 0 |
| 4 | Balance text | 624:43 | text | 335 | 48 | 26 | 18 | 0 |
| 5 | Panel scrim | 624:46 | shape: fill `rgba(0,0,0,0.59)` | 18 | 295 | 356 | 540 | 0 |
| 6 | "300 coins" button base | 623:36 | `Assets/Art/newGameButton.png` (reuse), border 4px `#c7c8bc`, radius 21 | 55 | 409 | 283 | 61 | 0 |
| 7 | "300 coins | $0.99" label | 623:37 | text | 65 | 422 | 264 | 23 | 0 |
| 8 | Coin icon | 624:47 | `Assets/Art/UI/coinIcon.png` (reuse) | 70 | 428 | 26 | 23 | 0 |
| 9 | "900 coins" button base | 624:54 | same style as #6 | 55 | 519 | 283 | 61 | 0 |
| 10 | "900 coins | $1.99" label | 624:55 | text | 65 | 532 | 264 | 23 | 0 |
| 11 | Coin icon | 624:57 | `Assets/Art/UI/coinIcon.png` (reuse) | 70 | 538 | 26 | 23 | 0 |
| 12 | "2000 coins" button base | 624:64 | same style as #6 | 54 | 629 | 283 | 61 | 0 |
| 13 | "2000 coins | $3.00" label | 624:65 | text | 73 | 641 | 264 | 23 | 0 |
| 14 | Coin icon | 624:67 | `Assets/Art/UI/coinIcon.png` (reuse) | 69 | 648 | 26 | 23 | 0 |
| 15 | "The Golden Vault" title | 623:29 | text | 52 | 320 | 288 | 40 | 0 |
| 16 | Fine print | 623:38 | text | 32 | 716 | 329 | 28 | 0 |
| 17 | Terms / Privacy / Remove Ads links | 624:72 | text | 66 | 770 | 277 | 28 | 0 |
| 18 | Decorative plant pot | 633:119 | `Assets/Art/ShopItems/plant/placed.png` (reuse) | 328.5 | 773 | 119 | 138 | 16.5° |
| 19 | Tracy holding coins | 635:121 (`brush 32`) | `Assets/Art/Monetization/tracyCoins.png` (new) | 46 | 72 | 200 | 223 | 0 |
| 20 | Sparkle star (large) | 635:122 | `Assets/Art/UI/starGold.png` (reuse) | 211 | 85 | 45 | 59 | 0 |
| 21 | Sparkle star (small) | 635:124 | `Assets/Art/UI/starOrange.png` (reuse) | 256 | 222 | 22 | 32 | 0 |

Text detail:
| Node | String | Font | Size | Colour | Align |
|---|---|---|---|---|---|
| 624:43 | 120 (example balance) | Rye | 14 | `#000000` | left |
| 623:37 | 300 coins \| $ 0.99 | Special Elite | 24 | "300"=`#f5ba55`, rest `#c7c8bc` | center |
| 624:55 | 900 coins \| $ 1.99 | Special Elite | 24 | "900"=`#f5ba55`, rest `#c7c8bc` | center |
| 624:65 | 2000 coins \| $ 3.00 | Special Elite | 24 | "2000"=`#f5ba55`, rest `#c7c8bc` | center |
| 623:29 | The Golden Vault | Rye | 32 | `#f5ba55` | left |
| 623:38 | charged to your account. Restore purchases any time from Settings | Special Elite | 14 | `#c7c8bc` | center |
| 624:72 | Terms \| Privacy \| Remove Ads | Special Elite | 16 | `#c7c8bc` | right |
| 623:33 | X | **Quicksand** | 24 | `#ffffff` | left |

---

## Fonts used

- **Rye** (Regular) — headings/titles. TMP asset already exists in the project.
- **Special Elite** (Regular) — body copy, labels, buttons. TMP asset already exists in the project.
- **Quicksand** (Regular) — used ONLY for the "X" close-button glyph on RemoveAds (`622:242`) and BuyCoins (`623:33`), size 24, white. **No TMP asset for Quicksand exists in the project.** Recommend either (a) swapping the glyph to a plain Unicode "×" rendered in Special Elite/Rye (visually close enough, avoids adding a new font), or (b) importing a Quicksand TMP font asset if pixel-exact match to Figma is required. Flagging per instructions rather than silently substituting.

---

## Interaction notes

- **Buttons:** every node/group named `newGameButton`, `Rectangle 29`/`Rectangle 4` inside an `<a>`-equivalent wrapper in the design-context output, or any pill labelled with a verb ("Restore", "Buy Item", "Give up", "Place Item", "Undo", "Remove ads", "Get Package…", the 3 coin-pack rows, "Back to workshop", "Continue", "Double Reward", Journal/Settings/Quit rows on GamePausedOverlay) is a tappable button.
- **Tabs:** Shop's "Décor" (`458:64`, active — gold star + tan `#e4c191` text) and "Misc." (`497:33`, inactive — orange star + muted `#99805f` text) are a 2-state tab bar; swap `UI/tabStarActive.png`/`tabStarInactive.png` (or `starGold`/`starOrange`) and the text colour on selection.
- **Scroll area:** Shop's item grid scroll bounds ≈ `x:26–388, y:411–886` (panel `455:46`); the scrollbar track (`460:14`) and thumb (`471:35`) sit at `x:356–370` along the same vertical range, thumb currently near the top (short content, only 3 items visible so scrolling may not be needed yet).
- **Language dropdown** (`315:405`) in Settings is a single fixed value "English" today — no other options represented in this Figma pass; treat as a stub if you need a functioning dropdown.
- **Sliders:** SFX/Music audio sliders in Settings are horizontal drag tracks, track bounds `x:178–334`ish, handle currently sits mid-track (~50%) for both.
- **Panels per mode:** PreviewMode / enteredEditMode / SelectedObjectToMove all share the same header-bar + bottom-info-panel template (bottom scrim `rgba(0,0,0,0.3)` full width from y≈632, info panel `rgba(3,50,29,0.52)` bordered box). enteredEditMode has no item selected yet (shorter info panel, no price/desc, just an instruction). Header bar text/height differs slightly: "Preview Mode" bar height 61 @ y79 (translucent black `rgba(0,0,0,0.6)`); "Edit Mode" bars are taller-opacity `rgba(0,0,0,0.71)` @ y56/57, height 61, with the "Edit Mode" title itself using a larger 36px size vs. Preview Mode's 24px.
- **Book Pile vs Lamp vs Plant placed art:** the two "editable item" screens (enteredEditMode / PreviewMode) show the Lamp (already placed, lit, `brush 24`) and the Books pile (already placed with candles, `brush 29`) as pre-existing room decor; the item currently being bought/moved (Plant in PreviewMode, Books in SelectedObjectToMove) is shown as the bright white-outlined "sticker" variant instead, at a slightly different crop/position than its resting `brush 29`/`placed.png` art. Implementation-wise: `placed.png` = resting-in-room state, `icon.png` = both the shop-card thumbnail AND the "picked up / being dragged" sticker state.

---

## Shop item data

| Item | itemId (suggested) | Name | Price | Description | Card position (Shop grid) | Placed size/pos (room, `zoomoutBG` 412×917 space) |
|---|---|---|---|---|---|---|
| Lamp | `lamp` | (no on-screen name string found) | 100 coins | **MISSING** — no PreviewMode/SelectedObjectToMove card exists for the Lamp in this Figma pass, so no verbatim description text was found. | `x:151,y:436,w:82,h:107` (card 585:36) | `x:12,y:296,w:94,h:180` (`brush 24`, lit) |
| Books | `books` | "Book Pile" | 100 coins | "You've recently gotten into reading mystery books about time travel, so you got yourself a pile to keep at the workshop!" | `x:45,y:436,w:82,h:107` (card 585:80) | `x:207,y:323,w:205,h:138` (`brush 29`, w/ candles) |
| Plant | `plant` | "Plant Pot" | 50 coins | "This pretty plant pot décor was found at a store near the workshop, it's really pretty!" | `x:259,y:435,w:82,h:110` (card 585:47) | `x:250,y:468,w:138,h:182` (as shown in PreviewMode; resting/placed crop not separately shown for Plant in this pass — reuse `ShopItems/plant/placed.png` at the same box) |

---

## Sticker removal (poster 2)

Found on page `0:1`, section `574:37` ("Poster2"), NOT inside section `585:133`.

- `close-upForStickerRemoval` — node `574:27`, absolute canvas rect `x=500, y=3780, w=2304, h=4096`.
- `sticker1` — node `574:28`, absolute canvas rect `x=1818, y=4912, w=710, h=603`.
- `sticker2` — node `574:34`, absolute canvas rect `x=1818, y=6505, w=710, h=603`.
- Annotation text next to them: "click stickers so they fall + sticker removal sound".

Rects relative to the close-up image, top-left origin (pixels, close-up is 2304×4096):
- `sticker1`: `x=1318, y=1132, w=710, h=603`
- `sticker2`: `x=1318, y=2725, w=710, h=603`

Rects normalised 0..1, origin **bottom-left** (as requested):
- `sticker1`: `x:[0.572, 0.880]`, `y:[0.576, 0.724]`
- `sticker2`: `x:[0.572, 0.880]`, `y:[0.188, 0.335]`

(Both stickers share the same horizontal band, stacked vertically — sticker1 upper, sticker2 lower, matching the existing `Assets/Art/Posters/poster2/sticker1.png` / `sticker2.png` / `close-upForStickerRemoval.png` already in the repo.)

---

## Other frames on page (outside section `585:133`)

Top-level sections found on page `0:1`:

| Section | id | Notes |
|---|---|---|
| new stuff / alteratios | `585:133` | **This batch's target** — covered above. |
| Items | `606:55` | Shop item icon/placed source art — covered above (lampIcon/lampItem/plantIcon/plantItem/brush 28/brush 31). |
| finals, keep size pls | `82:38` | **Older**, already-implemented screens (DeskHub `zoomoutBG`/`ZOOMinBG`, `catBoard`, `tracyFull`, LinnenBacking tool group w/ `roller`/`brush 6`, etc.) — predates this batch, corresponds to already-exported `Assets/Art/DeskHub/*` and `Assets/Art/LinnenAssets/*`. Not newer versions, no action needed. |
| GameScreens | `99:74` | **Older**, already-implemented screens: `TracyHelpOverlay` (→ `Assets/Art/TracyHelpOverlay/*`, already exported), `ThanksForPlayingBG` (→ `Assets/Art/thanksForPlaying/*`, already exported), and more. Not newer versions, no action needed. |
| First Poster Assets, Phase By Phase | `319:443` | Poster 1 restoration phase-by-phase reference art — already covered by `Assets/Art/Posters/poster1/*`. |
| level2 | `571:18` | Poster 2 restoration phase-by-phase reference art — already covered by `Assets/Art/Posters/poster2/*`. |
| Poster2 | `574:37` | Contains the sticker-removal close-up (see above) plus Poster 2's other phase art; already covered. |
| References/inspo | `600:20` | Mood-board / inspiration images, not shippable game screens — ignored. |
| Space | `606:56` | No content inspected (likely spacing/organisational section, not a screen) — ignored. |
| claude.ai (English-US) by html.to.design… | `4:372` | Unrelated scraped web content accidentally on the canvas — ignored. |

No frames outside `585:133` represent *newer* versions of the screens this batch covers; the other sections are either older (already-implemented and already-exported) or non-screen reference material.
