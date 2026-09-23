# SHOP agent — Batch 2 handoff

## 1. Files created / changed

| File | What it does |
|---|---|
| `Assets/Scripts/Decor/RoomCoordinates.cs` (new) | Plain C#, no UnityEngine. Normalised (0..1, bottom-left origin) <-> room-local pixel maths, and the "an item can't leave the room" clamp. Unit tested. |
| `Assets/Scripts/Decor/DecorationView.cs` (new) | One decoration `Image` in the room: drag via `IBeginDrag/IDrag/IEndDragHandler` with the canvas camera, mode-gated (`SetInteraction`), selection outline, no per-frame allocation. |
| `Assets/Scripts/Decor/DecorationRoom.cs` (new) | Pools `DecorationView`s from `IDecorationInventory` + `ShopCatalog`, sorted by `sortingOrder` (then catalogue order); refreshes on `ItemPurchased`/`PlacementChanged`; hosts the temporary preview item and the runtime `preview.item` tutorial anchor; forwards a drag that starts on empty floor to the one draggable item. |
| `Assets/Scripts/Decor/RuntimeTutorialAnchor.cs` (new) | One-line helper: `AddComponent<TutorialAnchor>` (if missing) + `TutorialAnchor.SetAnchorId`, used by `ShopItemCard` and `DecorationRoom` for the runtime `shop.item.<id>` / `preview.item` anchors. |
| `Assets/Scripts/UI/DeskHubScreen.cs` (new) | `ScreenView` for `GameScreen.DeskHub`. Normal / Edit / Preview modes; see §2. |
| `Assets/Scripts/UI/ShopScreen.cs` (new) | `ScreenView` for `GameScreen.Shop`. Décor/Misc tabs, grid built from `ShopCatalog` by cloning one template card, coin balance, disabled ads/IAP buttons, routes into `DeskHubScreen.RequestPreview`. |
| `Assets/Scripts/UI/ShopItemCard.cs` (new) | One shop grid tile: icon, price, owned state, `Clicked` event, registers its own `shop.item.<id>` anchor. |
| `Assets/Scripts/UI/CoinBalanceLabel.cs` (new) | Reusable: binds any `TMP_Text` to `IWallet.CoinsChanged`, with an optional punch animation. Used on the desk hub, the shop and preview mode; free for other agents too. |
| `Assets/Editor/CatalogBuilder.cs` (new) | `RebuildAll()` (menu `Restorium/Rebuild Catalogs & Locale Tables`) rebuilds `PosterCatalog.asset` and `ShopCatalog.asset` from every `PosterData`/`ShopItemData` on disk, then calls `LocaleTableBuilder.RebuildAll()`. `CatalogPostprocessor` re-runs the catalogue half automatically (deferred, debounced) whenever a poster/shop-item asset is imported, moved or deleted. |
| `Assets/Editor/ShopItemWizard.cs` (new) | Menu `Restorium/Shop/New Shop Item`: a form (id, tab, price, both sprites, size, default position, sorting order, name+description in en/pt-BR) that creates/edits one `ShopItemData` asset, upserts its strings via `LocaleTableBuilder.Upsert`, and rebuilds the catalogues. Also `Restorium/Shop/Create Starter Items` (lamp/plant/books, reading `Assets/Art/ShopItems/<id>/icon.png` + `placed.png`). |
| `Assets/Editor/Localization/LocaleSource.Shop.cs` (new) | Every shop/desk-hub string, en verbatim from Figma + natural pt-BR. Lamp description is DRAFT (Figma has no copy for it) — see §7. |
| `Assets/Scripts/Tests/EditMode/Logic/RoomCoordinatesTests.cs` (new) | NUnit, no UnityEngine: round-trip, clamping (incl. oversized/zero-size items), NaN/Infinity fallback, empty-room safety. |

No other agent's files were touched. `Assets/Scripts/Decor/RuntimeTutorialAnchor.cs` was simplified during this session (see §7 "Fixed from the previous pass") to use `TutorialAnchor.SetAnchorId` directly instead of a `JsonUtility` hack, now that `TutorialAnchor` exposes that method publicly.

## 2. Public API implemented (matches contract §5 "SHOP agent provides")

```csharp
// Decor/RoomCoordinates.cs — plain C#
public readonly struct RoomRect { public RoomRect(float x, float y, float width, float height); public bool IsEmpty { get; } }
public static class RoomCoordinates
{
    public const float Centre = 0.5f;
    public static void ToLocal(in RoomRect room, float nx, float ny, out float localX, out float localY);
    public static void ToNormalized(in RoomRect room, float localX, float localY, out float nx, out float ny);
    public static void Clamp(in RoomRect room, float itemWidth, float itemHeight, ref float nx, ref float ny);
    public static void LocalToClampedNormalized(in RoomRect room, float localX, float localY, float itemWidth, float itemHeight, out float nx, out float ny);
    public static float Sanitize(float value);
}

// Decor/DecorationView.cs
public class DecorationView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public ShopItemData Item { get; } public string ItemId { get; } public RectTransform Rect { get; }
    public float NormalizedX { get; } public float NormalizedY { get; }
    public bool IsDragging { get; } public bool IsSelected { get; } public bool IsPreview { get; internal set; }
    public void SetNormalized(float nx, float ny);
}

// Decor/DecorationRoom.cs
public class DecorationRoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public const string PreviewAnchorId = "preview.item";
    public event Action<DecorationView> ViewClicked, ViewDragStarted, ViewDragEnded;
    public ShopCatalog Catalog { get; } public RoomRect RoomRect { get; } public DecorationView PreviewView { get; }
    public void Refresh();
    public DecorationView ShowPreview(ShopItemData item);
    public void ClearPreview();
    public DecorationView Find(string itemId);
    public void SetInteraction(bool selectable, DecorationView dragTarget);
    public void SetSelected(DecorationView view);
    public bool ScreenToRoomLocal(PointerEventData eventData, out float x, out float y);
}

// UI/DeskHubScreen.cs
public enum DeskHubMode { Normal = 0, Edit = 1, Preview = 2 }
public class DeskHubScreen : ScreenView
{
    public override GameScreen Screen => GameScreen.DeskHub;
    public DeskHubMode Mode { get; }
    public event Action<DeskHubMode> ModeChanged;
    public void RequestPreview(ShopItemData item);   // call BEFORE routing to DeskHub; applied in OnShown
    public void EnterEditMode();                     // Edit button
    public void ExitEditMode();                      // "Back to workshop" button
}

// UI/ShopScreen.cs
public class ShopScreen : ScreenView
{
    public override GameScreen Screen => GameScreen.Shop;
    public ShopCategory ActiveTab { get; }
    public void SelectDecor(); public void SelectMisc();
    public void ShowTab(ShopCategory category);
}

// UI/ShopItemCard.cs
public class ShopItemCard : MonoBehaviour
{
    public ShopItemData Item { get; } public bool Owned { get; }
    public event Action<ShopItemData> Clicked;
    public void Bind(ShopItemData item, bool owned);
    public void SetOwned(bool owned);
}

// UI/CoinBalanceLabel.cs
public class CoinBalanceLabel : MonoBehaviour { public int Shown { get; } public void Redraw(bool animate); }

// Editor/CatalogBuilder.cs
public static class CatalogBuilder
{
    public static void RebuildAll();      // menu "Restorium/Rebuild Catalogs & Locale Tables"
    public static void RebuildCatalogs(); // catalogues only, used by the postprocessor
    public static PosterCatalog LoadOrCreatePosterCatalog();
    public static ShopCatalog LoadOrCreateShopCatalog();
}
public class CatalogPostprocessor : AssetPostprocessor { /* re-runs RebuildCatalogs() automatically */ }

// Editor/ShopItemWizard.cs
public class ShopItemWizard : EditorWindow
{
    [MenuItem("Restorium/Shop/New Shop Item")] public static void Open();
    [MenuItem("Restorium/Shop/Create Starter Items")] public static void CreateStarterItems();
}
```

Behaviour notes the scene builder / other agents need:

- **Buy flow:** `BuyPreviewItem()` calls `IDecorationInventory.TryPurchase(itemId, price, view.NormalizedX, view.NormalizedY)`. `Success` -> `SfxId.Purchase`, mode back to Normal (the room turns the preview view into the owned view in place — no flicker, because `DecorationRoom.OnItemPurchased` reuses the same `DecorationView`). `NotEnoughCoins` -> the price label + coin badge shake (unscaled time) and turn red for ~0.6 s longer than the shake, plus a "not enough coins yet" line — no popup/dialog. `AlreadyOwned` (shouldn't happen; owned cards are disabled in the shop) clears the preview silently.
- **Edit flow:** tapping an owned item selects it (`room.ViewClicked`); dragging moves the `DecorationView` only (nothing saved). "Place Item" -> `IDecorationInventory.SetPlacement` + `SfxId.ItemPlaced`. "Undo" (or selecting a different item, or leaving the screen) reverts to the position recorded at selection time. Nothing is saved by drag alone.
- **GameSignals:** `RaisePreviewOpened(itemId)` on entering Preview (after the view/labels are ready, so `TutorialAnchor.Find("preview.item")` already resolves). `RaiseEditModeChanged(true/false)` on entering/leaving Edit mode (button and "Back to workshop"/leaving the screen while in Edit both raise `false`).
- **Leaving the screen for any reason** (Give up, or `Hide()` because another screen routed in) is handled once in `OnHidden`: unplaced edit moves are reverted, the preview item is removed from the room, `EditModeChanged(false)` fires if Edit was active. `RequestPreview` called while the screen is already visible applies immediately; called before showing it, it is applied in `OnShown` on the very next frame the screen appears.
- **ShopScreen** never spends coins or touches the router directly: `OnCardClicked` calls `deskHub.RequestPreview(item)` **then** `flow.GoToDeskHub()` (preview must be queued before the screen shows, per contract). "Remove ads" / "Buy More Coins" are forced `interactable = false` in `Awake()` regardless of what the scene has wired, so nobody can accidentally ship a dead button that looks live.
- **Grid growth:** the card template lives disabled under `Grid/Viewport/Content`; `ShopScreen` clones it once per `ShopItemData` (lazily, on first tab show) and just shows/hides clones on tab switches, so a brand-new `ShopItemData` asset appears with **zero scene edits** — this is the whole point of `ShopItemWizard`.

## 3. Scene wiring for the builder

All positions below are **from the top-left of the 412×917 reference canvas** (Figma's own coordinate space for each frame; Unity's UI y-axis is bottom-up, so `anchoredPosition.y` for a top-anchored element is `-figmaY` and for a bottom-anchored element is `917 - figmaY - height`). Figma file `liAY3FddBN7jtIHUvEs4Uu`, frames: `Desk_Hub` 91:45, `enteredEditMode` 612:98, `SelectedObjectToMove` 612:119, `Shop` 455:42, `PreviewMode` 606:58, `Items` 606:55 (I read these five frames directly with the read-only Figma metadata tool since `Docs/Batch2/FigmaLayout.md` was not yet written by the ART agent at the time I wrote this handoff — **cross-check against `FigmaLayout.md` once it exists; if it disagrees with a number below, FigmaLayout.md wins**).

### DeskHubScreen — object `DeskHubScreen` under Canvas, stretch/stretch, offsets 0, component `DeskHubScreen`

```
DeskHubScreen                              (Desk_Hub 91:45; anchor stretch/stretch, offsets 0)
├── Background          Image               Art/DeskHub/zoomoutBG.png, stretch/stretch, offsets 0
├── Room                Image (alpha 0) + DecorationRoom
│                       stretch/stretch, offsets 0 (the room IS the whole 412x917 screen — decorations
│                       are positioned in this space, same space RoomCoordinatesTests uses)
├── BookButton           Button + TutorialAnchor(desk.book)
│                       Art/DeskHub/book.png, x=151 y=457 w=125 h=115 (top-left anchor; centre pivot
│                       recommended: anchoredPosition = (151+62.5, -(457+57.5)) = (213.5, -514.5))
├── NormalUI            (empty; shown only in Normal mode)
│   └── TopBar          Image/empty, x=11 y=25 w=396 h=84.36  (Figma "topUI")
│       ├── MenuButton      Button + TutorialAnchor(desk.menu)
│       │                   "hamburguerIcon" art (hamburgerIcon.png), rel. to TopBar x=248 y=34 w=53 h=57
│       │                   -> absolute x=259 y=59
│       ├── ShopButton      Button + TutorialAnchor(desk.shop)
│       │                   Figma "Group 11" (bagButton.png / bagIcon.png "Bag_duotone"), rel. x=11 y=25
│       │                   w=79 h=84.36 -> absolute x=22 y=50
│       ├── EditButton      Button + TutorialAnchor(desk.edit)
│       │                   "editModeButton" (editModeButtonBase.png + moveIcon.png), rel. x=96 y=41
│       │                   w=44 h=40 -> absolute x=107 y=66
│       └── CoinsBadge      Image + TutorialAnchor(desk.coins)
│           │               "Coins" (coinShowerBG.png), rel. x=301 y=36 w=106 h=53 -> absolute x=312 y=61
│           └── CoinsLabel  TMP_Text + CoinBalanceLabel   inside CoinsBadge, centred
├── EditUI               (empty; shown only in Edit mode)
│   ├── EditHeader        Image, "Group 23/24" bar, x=0 y=56 w=413 h=61 — TMP_Text "Edit Mode"
│   │                     (LocalizedText key ui.edit.title) + decorative move icon (Art/UI/moveIcon.png,
│   │                     non-interactive, purely visual continuity with EditButton)
│   ├── EditIdleGroup     (shown while nothing selected — DeskHubScreen.editIdleGroup)
│   │   ├── Panel         Image, x=18 y=640 w=376 h=100 (Figma "Group 22" in enteredEditMode, SHORTER
│   │   │                 than the selected-state panel below)
│   │   │   └── IdleLabel TMP_Text + LocalizedText(ui.edit.hint), x=41 y=666 w=330 h=48
│   │   └── BackToWorkshopButton  Button + TutorialAnchor(edit.done)
│   │                     "newGameButton", x=106 y=748 w=212 h=96 (button rect itself: x=106 y=793
│   │                     w=212 h=51, "or" label above it at y=748 is decorative)
│   └── EditSelectedGroup (shown while an item is selected — DeskHubScreen.editSelectedGroup)
│       └── Panel          Image, x=18 y=640 w=376 h=220 (Figma "Group 22" in SelectedObjectToMove)
│           ├── DragHint    TMP_Text + LocalizedText(ui.edit.dragHint), x=41 y=657 w=330 h=15.6
│           ├── EditNameLabel  TMP_Text  — DeskHubScreen.editNameLabel (code sets text via
│           │                 Format("ui.desk.selected", itemName); Figma shows this stacked with Desc
│           │                 as one paragraph starting x=41 y=685 — split into 2 labels for clean code:
│           │                 name line first)
│           ├── EditDescLabel  TMP_Text  — DeskHubScreen.editDescLabel (Format("ui.desk.desc", desc);
│           │                 second line of the same Figma paragraph, w=347)
│           ├── PlaceButton    Button + TutorialAnchor(edit.place)
│           │                 "Group 20" x=45 y=796.67 w=121 h=36.67 — text "Place Item"
│           │                 (NOTE: SelectedObjectToMove's button is 152 wide to fit "Place Item";
│           │                 PreviewMode's matching button ("Buy Item") is 121 wide — use each frame's
│           │                 own width, both centred in the same panel)
│           └── UndoButton     Button + TutorialAnchor(edit.undo)
│                             "Group 21" x=245 y=796.67 w=121 h=36.67 — text "Undo"
└── PreviewUI            (empty; shown only in Preview mode)
    ├── PreviewHeader      Image, "Group 24" bar, x=0 y=79 w=413 h=61 — TMP_Text "Preview Mode"
    │                      (LocalizedText ui.preview.title) at x=52 y=98, decorative move icon at
    │                      x=225 y=92 w=38 h=36 (non-interactive)
    │   └── PreviewCoinsBadge  Image — DeskHubScreen.previewCoinsBadge (the RectTransform that shakes)
    │       │                  x=272 y=83 w=106 h=53 (coinShowerBG.png)
    │       └── PreviewCoinsLabel  TMP_Text + CoinBalanceLabel, centred in PreviewCoinsBadge
    └── PreviewPanel        Image, x=18 y=640 w=376 h=220 (Figma "Group 22" in PreviewMode)
        ├── DragHint         TMP_Text + LocalizedText(ui.preview.dragHint), x=41 y=657 w=330 h=15.6
        ├── PreviewNameLabel TMP_Text — DeskHubScreen.previewNameLabel (Format("ui.desk.selected", name))
        ├── PreviewPriceLabel TMP_Text — DeskHubScreen.previewPriceLabel (Format("ui.preview.price", price);
        │                    also the label that turns red on Not Enough Coins)
        ├── PreviewDescLabel TMP_Text — DeskHubScreen.previewDescLabel (Format("ui.desk.desc", desc))
        │                    (the three labels above replace Figma's single paragraph at x=41 y=685
        │                    w=347 h=93.3: "Selected: {name} Price: {price} coins Desc.: {desc}")
        ├── NotEnoughCoinsLabel TMP_Text — DeskHubScreen.notEnoughCoinsLabel, start INACTIVE, placed
        │                    just under the price/desc text; text set from ui.preview.notEnoughCoins
        ├── BuyButton        Button + TutorialAnchor(preview.buy)
        │                    "Group 20" x=45 y=796.67 w=121 h=36.67 — text "Buy Item"
        └── GiveUpButton     Button + TutorialAnchor(preview.cancel)
                             "Group 21" x=245 y=796.67 w=121 h=36.67 — text "Give up"
```

Inspector fields on `DeskHubScreen`:

| Field | Assign |
|---|---|
| `flow` | `GameFlow` (GameFlowController) |
| `overlays` | `Overlays` (OverlayController) |
| `room` | `DeskHubScreen/Room` |
| `normalObjects` | `[NormalUI, BookButton]` |
| `editObjects` | `[EditUI]` |
| `previewObjects` | `[PreviewUI]` |
| `menuButton` / `shopButton` / `editButton` / `bookButton` | the four buttons above |
| `editIdleGroup` / `editSelectedGroup` | `EditUI/EditIdleGroup`, `EditUI/EditSelectedGroup` |
| `editNameLabel` / `editDescLabel` | inside `EditSelectedGroup` |
| `placeButton` / `undoButton` / `backToWorkshopButton` | as above |
| `previewNameLabel` / `previewPriceLabel` / `previewDescLabel` | inside `PreviewPanel` |
| `buyButton` / `giveUpButton` | as above |
| `notEnoughCoinsLabel` | `PreviewPanel/NotEnoughCoinsLabel` |
| `previewCoinsBadge` | `PreviewUI/PreviewHeader/PreviewCoinsBadge` (RectTransform) |
| `shakeSeconds` / `shakePixels` / `shortPriceColor` | defaults are fine (0.45s, 8px, a warm red) |

`Room`'s `DecorationRoom.catalog` <- `Assets/Data/Catalogs/ShopCatalog.asset`. Leave `dragCatcher` empty (found automatically from the `Image` on the same object). Add `DeskHubScreen` to `ScreenRouter.screens` on `GameFlow`.

`CatBoard 2` (catBoard.png), the "tracys" full-body images (tracyStill/tracyTalkingHappyNormal/tracyTalkingEmbarassed) and `DialogueRect` from the `Desk_Hub` Figma frame are **not** part of this hierarchy — they belong to `HubTracyOverlay` (the tutorial's full-body Tracy view, UI/Tutorial agent's, per contract §2). I read them out of the frame only to avoid the scene builder mistaking them for DeskHubScreen children.

### ShopScreen — object `ShopScreen` under Canvas, stretch/stretch, offsets 0, component `ShopScreen`

```
ShopScreen                                 (Shop 455:42)
├── Background           Image, Art/Shop/leatherBG.png, x=-14 y=359 w=441 h=567 (bottom leather panel)
├── HeaderImage           Image, Art/Shop/shopHeaderPanel.png / tracyShop.png, x=16 y=48 w=380 h=336
│                         (the "image 47" + "tracy" Figma nodes — one background photo; use whichever
│                         single exported PNG the ART agent produced for this header)
├── TitleGroup            "tracy'sEmporiumShop", x=43.58 y=64 w=320.42 h=64.95
│   ├── TitleTop          TMP_Text + LocalizedText(ui.shop.titleTop), x=59.47 y=82.68 w=73.5 h=17.1
│   └── TitleMain         TMP_Text + LocalizedText(ui.shop.titleMain), x=43.58 y=94.68 w=302 h=34.27
├── BackButton            Button + TutorialAnchor(shop.back), "Group 15", x=-4 y=21 w=193 h=55
│                         text LocalizedText(ui.shop.back)
├── DecorTab              Button, "Group 16", x=26 y=391 w=87 h=21.3, text LocalizedText(ui.shop.tabDecor)
├── MiscTab                Button, "Group 19", x=117 y=391 w=87 h=21.3, text LocalizedText(ui.shop.tabMisc)
├── CurrentBalanceRow      Image, x=27 y=192 w=163 h=33 ("+"-looking IAP row; the "+" and "100" number
│   │                      are Figma decoration for a future buy-coins flow — this build just shows the
│   │                      live balance)
│   └── BalanceLabel       TMP_Text + CoinBalanceLabel, centred where Figma's "100" sits
├── RemoveAdsButton        Button (interactable = false, forced in code), x=27 y=245 w=163 h=33
│                         text LocalizedText(ui.shop.removeAds)
├── BuyMoreCoinsButton     Button (interactable = false, forced in code), x=27 y=299 w=163 h=33
│                         text LocalizedText(ui.shop.buyMoreCoins)
├── EmptyLabel             TMP_Text + LocalizedText(ui.shop.empty), centred in the grid area, INACTIVE
│                         by default (ShopScreen shows it only when a tab has 0 items — the Misc tab)
└── Grid                   ScrollRect, x=26 y=411 w=362 h=475 ("Rectangle 22" panel bounds)
    └── Viewport           RectMask2D + Image(alpha 0), stretch/stretch
        └── Content         VerticalLayoutGroup is wrong here — use GridLayoutGroup
                            + ContentSizeFitter(vertical = PreferredSize)
                            Cell size ~ 82x110 (card frame, from the Lamp/Books/Plant Figma nodes),
                            spacing ~ (14, 14) to reproduce the 3-per-row look (Lamp/Plant/Books sit at
                            x=45/151/259 in Figma, i.e. ~106px pitch for an 82px card = 24px gap; use
                            GridLayoutGroup's own spacing rather than hand-placing cards)
            └── CardTemplate  Button (INACTIVE) + ShopItemCard + ButtonSfx(ButtonClick)
                              82x107 frame art ("Rectangle 4" + "Rectangle 25" glass overlay)
                ├── Icon        Image (ShopItemCard.icon), centred, preserveAspect
                ├── PriceGroup  (ShopItemCard.priceGroup)
                │   ├── CoinIcon    Image, Art/UI/coinIcon.png
                │   └── PriceLabel  TMP_Text (ShopItemCard.priceLabel)
                └── OwnedBadge   TMP_Text + LocalizedText(ui.shop.owned) (ShopItemCard.ownedBadge), INACTIVE
```

Inspector fields on `ShopScreen`:

| Field | Assign |
|---|---|
| `flow` | `GameFlow` |
| `deskHub` | `DeskHubScreen` object |
| `catalog` | `Assets/Data/Catalogs/ShopCatalog.asset` |
| `gridContent` | `Grid/Viewport/Content` |
| `cardTemplate` | `Grid/Viewport/Content/CardTemplate` (leave UNTICKED / inactive) |
| `scroll` | `Grid` (the ScrollRect) |
| `emptyLabel` | `EmptyLabel` |
| `decorTab` / `miscTab` | `DecorTab`, `MiscTab` |
| `backButton` / `removeAdsButton` / `buyMoreCoinsButton` | as above |
| `inactiveTabAlpha` | default 0.55 is fine |

Add `ShopScreen` to `ScreenRouter.screens` on `GameFlow`. Card fields on `ShopItemCard` (the template): `button` <- itself, `icon`/`priceLabel`/`priceGroup`/`ownedBadge` <- the children above.

Figma's "brush 21" decoration (a small overlapping sticker/ribbon at x=164 y=425 w=53 h=102, between the Lamp and Plant cards) and "Rectangle 28"/"Rectangle 23" (thin vertical bars at x≈356-370, likely a scrollbar handle for the grid) are cosmetic — attach them as non-interactive children of `Grid` if the art agent exported them, or skip them; neither has a serialized field.

## 4. Editor menu items (run in this order)

1. **RESTORATION agent's** `Restorium/Posters/Create or Update Poster 1 Data` and `.../Poster 2 Data` (creates the `PosterData` assets `CatalogBuilder` needs; they call `CatalogBuilder.RebuildAll()` themselves at the end, but it's harmless to run again).
2. `Restorium/Shop/Create Starter Items` — reads `Assets/Art/ShopItems/lamp|plant|books/{icon,placed}.png`. **Needs that art to exist first** (see §7 — it did not exist yet when I wrote this handoff). Re-run it any time after the art lands; it updates the same three assets in place.
3. `Restorium/Rebuild Catalogs & Locale Tables` — safe to run any time; also runs automatically on asset import once any `PosterData`/`ShopItemData` exists.
4. For a **new** shop item later: `Restorium/Shop/New Shop Item` (see the tooltip text in the window; every field explains itself). That menu alone is the whole "add an item" job — see §8.

## 5. Localization keys added (`LocaleSource.Shop.cs`)

| key | pt-BR | en | note |
|---|---|---|---|
| `ui.desk.selected` | Selecionado: {0} | Selected: {0} | shared by Edit + Preview panels |
| `ui.desk.desc` | Desc.: {0} | Desc.: {0} | shared by Edit + Preview panels |
| `ui.edit.title` | Modo de Edição | Edit Mode | |
| `ui.edit.hint` | Selecione um item... | Select an item to edit its position. | |
| `ui.edit.dragHint` | Toque e arraste... | Click and drag around the item to position it | |
| `ui.edit.place` | Colocar Item | Place Item | |
| `ui.edit.undo` | Desfazer | Undo | |
| `ui.edit.or` | ou | or | |
| `ui.edit.backToWorkshop` | Voltar à oficina | Back to workshop | |
| `ui.preview.title` | Modo de Prévia | Preview Mode | |
| `ui.preview.dragHint` | Toque e arraste... | Click and drag around the item to position it | |
| `ui.preview.price` | Preço: {0} moedas | Price: {0} coins | |
| `ui.preview.buy` | Comprar Item | Buy Item | |
| `ui.preview.giveUp` | Desistir | Give up | |
| `ui.preview.notEnoughCoins` | Ainda não tenho moedas suficientes... | Not enough coins yet... | **DRAFT**, not in Figma |
| `ui.shop.back` | Voltar à bancada | Go back to workbench | |
| `ui.shop.titleTop` | Empório da | Tracy's | **DRAFT split** — see §7 |
| `ui.shop.titleMain` | Tracy | Emporium Shop | **DRAFT split** — see §7 |
| `ui.shop.removeAds` | Remover anúncios | Remove ads | |
| `ui.shop.buyMoreCoins` | Comprar Moedas | Buy More Coins | |
| `ui.shop.tabDecor` | Decoração | Décor | |
| `ui.shop.tabMisc` | Diversos | Misc. | |
| `ui.shop.owned` | Comprado | Owned | **DRAFT**, not in Figma |
| `ui.shop.empty` | Mais itens em breve! | More items coming soon! | **DRAFT**, not in Figma |
| `shop.item.lamp.name` | Luminária | Lamp | |
| `shop.item.lamp.desc` | Uma luminária quentinha... | A warm, bright lamp for the workbench. With light like this, you'll never miss that a poster is a fake again! | **DRAFT** — see §7 |
| `shop.item.plant.name` | Vaso de Planta | Plant Pot | Figma copy verbatim |
| `shop.item.plant.desc` | Este vaso de planta lindinho... | This pretty plant pot décor was found at a store near the workshop, it's really pretty! | Figma copy verbatim |
| `shop.item.books.name` | Pilha de Livros | Book Pile | |
| `shop.item.books.desc` | Você começou a ler livros de mistério... | You've recently gotten into reading mystery books about time travel, so you got yourself a pile to keep at the workshop! | Figma copy verbatim |

Figma's curly apostrophe (’) is written as a straight `'` throughout (see the file header comment) because the TMP font atlas is Latin-1 only.

## 6. Tests

`Assets/Scripts/Tests/EditMode/Logic/RoomCoordinatesTests.cs` — 10 NUnit tests, no UnityEngine reference: corner/centre conversion, round-trip, bottom-left-origin room, clamp keeps a whole item inside, clamp leaves in-range values alone, an item bigger than the room centres itself, a zero-size item still clamps to [0,1], NaN/Infinity fall back to the room centre, an empty (zero-size) room never divides by zero, negative sizes are treated as zero. Already listed in `Tools/logictests/LogicTests.csproj` (`<Compile Include="$(S)\Decor\RoomCoordinates*.cs" />`).

Run via Unity's Test Runner (EditMode -> Run All) or `dotnet test Tools/logictests` from the worktree root. I could not get a clean `dotnet test` run of the **whole** shared project right now: `JournalPageRulesTests.cs`, `LocalePickerTests.cs` and `TutorialTriggerRulesTests.cs` (UI agent's, mid-flight) reference `RestoriumEmporium.UI`/`Localization`/`Tutorial` pure-logic types that are not yet added to `LogicTests.csproj` — not my files, not a RoomCoordinates problem (CORE's handoff flagged the same shared-project state). `sh Tools/compilecheck/check.sh` (the Unity-DLL compile, which does include EditMode tests) passes clean for every file I own; RoomCoordinatesTests.cs compiles there without issue.

## 7. Needs from others / open questions

- **ART agent — blocking for a full shop:** `Assets/Art/ShopItems/lamp/{icon,placed}.png`, `.../plant/{icon,placed}.png`, `.../books/{icon,placed}.png` did not exist yet when this handoff was written. `Restorium/Shop/Create Starter Items` is ready and will slot them in the moment the files land (it also warns per-missing-file if run early). `Docs/Batch2/FigmaLayout.md` also did not exist yet — I pulled the five frames' layout directly from Figma's metadata (read-only) for §3 above; if `FigmaLayout.md` later disagrees on a pixel value, trust `FigmaLayout.md`, it's the ART agent's authoritative pass.
- **Fixed from the previous pass:** `Decor/RuntimeTutorialAnchor.cs` used to write `TutorialAnchor`'s id field through `JsonUtility.FromJsonOverwrite` plus a deactivate/reactivate dance, written (I assume) before `TutorialAnchor` grew its own public `SetAnchorId`/`SetPointAt`. I simplified it to just call `SetAnchorId` — same behaviour, no reflection-adjacent trick, no needless `GameObject.SetActive` churn. Both call sites (`ShopItemCard.Bind`, `DecorationRoom.CreatePreviewAnchor`) already hand it an inactive object, so nothing else changed.
- **LEAD / Figma review — Lamp description is a DRAFT.** The Figma `Items` section has no copy for the lamp (only Plant Pot and Book Pile have description text in the frames). I drafted: *"A warm, bright lamp for the workbench. With light like this, you'll never miss that a poster is a fake again!"* — it nods at the `deskhub_lamp` tutorial's beat (Tracy blaming the dim desk light for missing the fake poster) without repeating Tracy's dialogue verbatim (that line belongs to the UI agent's `TutorialSequenceData`, not the item's own description). Please have Tracy's voice reviewed by a human; pt-BR is a direct translation of my draft, not an independent pass.
- **DRAFT: `ui.shop.titleTop` / `ui.shop.titleMain`.** Figma's `Tracy's Emporium Shop` title is two overlapping text runs ("Tracy's" small, "Emporium Shop" large) with no natural pt-BR split of the same shape; I split it "Empório da" / "Tracy" for Portuguese, which reads fine but changes emphasis. Flag for a native-speaker pass.
- **DRAFT: `ui.shop.owned`, `ui.shop.empty`, `ui.preview.notEnoughCoins`.** None of these three lines exist in the Figma copy (owned-state badge, the empty Misc tab, and the not-enough-coins message are all Batch 2 additions with no design reference). Please review the wording.
- **UI agent:** `deskhub_lamp`'s tutorial step that waits on `ItemPurchased "lamp"` with the hand on `preview.buy`, per contract §6, needs `buyButton`'s `TutorialAnchor` id to be exactly `preview.buy` — confirmed done above. The `stickers` sequence anchor `sticker.next` and `journal_page2`'s `journal.nextPage`/`journal.restoreButton` are not mine; listed here only to confirm I did **not** create competing anchors with those ids anywhere in Decor/Shop.
- **CORE agent:** `DeskHubScreen`/`ShopScreen` both call `ServiceLocator.Get<IDecorationInventory>()` / `IWallet` / `ILocalizationService` / `IAudioService` in `OnShown`/`Start`/`Awake` per the "never Awake" rule (§4) — no deviation, just confirming I followed it.
- **Scene builder:** the `Room` RectTransform must be sized to the FULL 412×917 screen (stretch/stretch, offsets 0), not a sub-panel — every position in `ShopItemData.defaultPosition` and every `OwnedDecoration.x/y` is normalised against the whole screen, matching the Figma "brush 24" / "plantIcon" / "BookPileIcon" absolute positions used to author the starter items (see `ShopItemWizard.Starters`). If the room is ever made a sub-rect of the desk area only, every existing position (in `ShopItemWizard.Starters` and in any player's save) would need re-deriving.

## 8. Optimisation ideas

- `ShopScreen.ShowTab` calls `LayoutRebuilder.ForceRebuildLayoutImmediate` once per tab switch (not per frame), which is fine at this catalogue size (3 items); if the catalogue grows into the dozens, consider only forcing the rebuild the first time a tab is shown and relying on the `GridLayoutGroup`'s own dirty tracking after that.
- `DecorationRoom.Refresh()` is O(owned items) with a couple of small allocation-free passes; it is only called on purchase/placement events and `OnEnable`, never per frame, so it is fine even with a much larger catalogue than this build's three items.
- `ShopItemWizard`'s "one-minute job" path end-to-end: **New Shop Item -> fill the form (id, tab, price, both PNGs, size, position, both languages) -> Create Item.** That single click creates the asset, writes all four locale strings, and rebuilds both catalogues — the item is immediately buyable in Play mode with no further step, which is the beginner-friendly path the contract asked for. Editing later is the same window with "Edit Existing" dragged in.
