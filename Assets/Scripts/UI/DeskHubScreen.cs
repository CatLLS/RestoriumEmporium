// ============================================================
// DeskHubScreen — Tracy's workshop desk, with Normal / Edit / Preview modes.
// WHAT & WHY: The hub the player returns to between restorations
//   (GameScreen.DeskHub). It shows the decorated room and the top bar (shop, edit
//   mode, settings, coins) and the journal book. The same room is also used to
//   MOVE owned items (Edit mode) and to try a shop item before buying it
//   (Preview mode), so those are modes of this screen, not separate screens.
// KEY DECISIONS:
//   - Modes are just "which groups of objects are visible + what the room lets
//     the player touch". Each mode lists its objects in the Inspector
//     (Normal Objects / Edit Objects / Preview Objects), so the builder can put
//     world art (the book) and UI bars in different layers of the hierarchy.
//   - Nothing is saved while dragging. Edit mode saves once on "Place Item"
//     (IDecorationInventory.SetPlacement); Preview saves once on "Buy Item"
//     (IDecorationInventory.TryPurchase, which spends coins and records the
//     position atomically). "Undo" / "Give up" simply don't save.
//   - RequestPreview(item) is called by the shop BEFORE routing here, and applied
//     in OnShown, so the preview is set up on the very frame the screen appears
//     (the tutorial listens for GameSignals.PreviewOpened right then).
//   - Not enough coins = clear feedback without a popup: the price line and the
//     coin badge shake (unscaled time, no allocation per frame), the price turns
//     red briefly, and a line of text explains how to earn more.
//   - Leaving the screen for any reason (Give up, a routed screen change) cleans
//     up: unplaced edit moves are reverted, the preview item is removed, and
//     EditModeChanged(false) is raised if edit mode was on.
//   - Routing goes through GameFlowController; the settings overlay through
//     OverlayController. This screen never touches the router or the save.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// Full hierarchy (names, art files, Figma elements): Docs/Batch2/handoff/SHOP.md.
// [ ] Under Canvas create an empty object named exactly DeskHubScreen, anchor
//     stretch/stretch, offsets 0. Add Component -> Desk Hub Screen.
// [ ] Build the children listed in SHOP.md: Background, BookButton, Room (with
//     Decoration Room), NormalUI, EditUI, PreviewUI.
// [ ] Drag into the Inspector:
//       Flow      <- GameFlow object (GameFlowController)
//       Overlays  <- the Overlays object (OverlayController)
//       Room      <- DeskHubScreen/Room
//       Normal Objects  <- NormalUI and BookButton
//       Edit Objects    <- EditUI
//       Preview Objects <- PreviewUI
//       every Button / label field (names match the SHOP.md hierarchy).
// [ ] Add DeskHubScreen to the ScreenRouter "Screens" list on GameFlow.
// [ ] Tutorial Anchors (Add Component -> Tutorial Anchor, type the id):
//       ShopButton desk.shop, BookButton desk.book, EditButton desk.edit,
//       MenuButton desk.menu, CoinsBadge desk.coins, BuyButton preview.buy,
//       GiveUpButton preview.cancel, PlaceButton edit.place, UndoButton edit.undo,
//       BackToWorkshopButton edit.done. (preview.item is added at runtime.)
// ---------------------------------------------------------------

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Decor;
    using RestoriumEmporium.Economy;
    using RestoriumEmporium.Localization;

    /// <summary>What the desk hub is doing right now.</summary>
    public enum DeskHubMode
    {
        Normal = 0,
        Edit = 1,
        Preview = 2
    }

    [DisallowMultipleComponent]
    public class DeskHubScreen : ScreenView
    {
        // Localisation keys used from code (all defined in LocaleSource.Shop.cs).
        private const string KeySelected = "ui.desk.selected";         // "Selected: {0}"
        private const string KeyDesc = "ui.desk.desc";                 // "Desc.: {0}"
        private const string KeyPrice = "ui.preview.price";            // "Price: {0} coins"
        private const string KeyNotEnough = "ui.preview.notEnoughCoins";

        [Header("Flow")]
        [Tooltip("GameFlowController on the GameFlow object.")]
        [SerializeField] private GameFlowController flow;

        [Tooltip("OverlayController on the Overlays object (settings / pause).")]
        [SerializeField] private OverlayController overlays;

        [Tooltip("DeskHubScreen/Room — draws and moves the decorations.")]
        [SerializeField] private DecorationRoom room;

        [Header("Mode groups (objects shown only in that mode)")]
        [SerializeField] private GameObject[] normalObjects = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] editObjects = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] previewObjects = Array.Empty<GameObject>();

        [Header("Normal mode")]
        [SerializeField] private Button menuButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button editButton;
        [SerializeField] private Button bookButton;

        [Header("Edit mode")]
        [Tooltip("Shown while nothing is selected: 'Select an item...' + Back to workshop.")]
        [SerializeField] private GameObject editIdleGroup;

        [Tooltip("Shown while an item is selected: details + Place Item / Undo.")]
        [SerializeField] private GameObject editSelectedGroup;

        [SerializeField] private TMP_Text editNameLabel;
        [SerializeField] private TMP_Text editDescLabel;
        [SerializeField] private Button placeButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button backToWorkshopButton;

        [Header("Preview mode")]
        [SerializeField] private TMP_Text previewNameLabel;
        [SerializeField] private TMP_Text previewPriceLabel;
        [SerializeField] private TMP_Text previewDescLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button giveUpButton;

        [Tooltip("Line shown when the player can't afford the item.")]
        [SerializeField] private TMP_Text notEnoughCoinsLabel;

        [Tooltip("The coin badge in the Preview bar; shakes when coins are short.")]
        [SerializeField] private RectTransform previewCoinsBadge;

        [Header("Not-enough-coins feedback")]
        [SerializeField] private float shakeSeconds = 0.45f;
        [SerializeField] private float shakePixels = 8f;
        [SerializeField] private Color shortPriceColor = new Color(0.86f, 0.25f, 0.2f, 1f);

        private IDecorationInventory _inventory;
        private IAudioService _audio;
        private ILocalizationService _loc;
        private bool _locSubscribed;

        private DeskHubMode _mode = DeskHubMode.Normal;
        private ShopItemData _pendingPreview;
        private ShopItemData _previewItem;
        private DecorationView _previewView;
        private bool _leaving;

        private DecorationView _editSelected;
        private float _undoX;
        private float _undoY;

        private Coroutine _shake;
        private readonly RectTransform[] _shakeTargets = new RectTransform[2];
        private readonly Vector2[] _shakeOrigins = new Vector2[2];
        private Color _priceColor = Color.white;
        private bool _priceColorCached;

        public override GameScreen Screen => GameScreen.DeskHub;

        public DeskHubMode Mode => _mode;

        /// <summary>Raised after every mode change.</summary>
        public event Action<DeskHubMode> ModeChanged;

        // ---- Lifecycle ----

        private void Awake()
        {
            AddClick(menuButton, OpenSettings);
            AddClick(shopButton, GoToShop);
            AddClick(editButton, EnterEditMode);
            AddClick(bookButton, OpenJournal);
            AddClick(placeButton, PlaceSelected);
            AddClick(undoButton, UndoSelected);
            AddClick(backToWorkshopButton, ExitEditMode);
            AddClick(buyButton, BuyPreviewItem);
            AddClick(giveUpButton, GiveUpPreview);

            if (room != null)
            {
                room.ViewClicked += OnRoomViewClicked;
            }
        }

        private void OnDestroy()
        {
            RemoveClick(menuButton, OpenSettings);
            RemoveClick(shopButton, GoToShop);
            RemoveClick(editButton, EnterEditMode);
            RemoveClick(bookButton, OpenJournal);
            RemoveClick(placeButton, PlaceSelected);
            RemoveClick(undoButton, UndoSelected);
            RemoveClick(backToWorkshopButton, ExitEditMode);
            RemoveClick(buyButton, BuyPreviewItem);
            RemoveClick(giveUpButton, GiveUpPreview);

            if (room != null)
            {
                room.ViewClicked -= OnRoomViewClicked;
            }

            UnsubscribeLocale();
        }

        protected override void OnShown()
        {
            ResolveServices();
            SubscribeLocale();
            _leaving = false;

            var pending = _pendingPreview;
            _pendingPreview = null;

            if (pending != null)
            {
                EnterPreview(pending);
            }
            else
            {
                SetMode(DeskHubMode.Normal);
            }
        }

        protected override void OnHidden()
        {
            UnsubscribeLocale();
            StopShake();

            if (_mode == DeskHubMode.Edit)
            {
                RevertEditSelection();
                GameSignals.RaiseEditModeChanged(false);
            }

            if (room != null)
            {
                room.ClearPreview();
                room.SetSelected(null);
                room.SetInteraction(false, null);
            }

            _previewItem = null;
            _previewView = null;
            _editSelected = null;
            _mode = DeskHubMode.Normal;
            _leaving = false;
        }

        // ---- Public API ----

        /// <summary>
        /// Opens Preview mode for <paramref name="item"/>. Call BEFORE routing to the
        /// desk hub; it is applied when the screen is shown (immediately if it
        /// already is).
        /// </summary>
        public void RequestPreview(ShopItemData item)
        {
            if (IsVisible)
            {
                _pendingPreview = null;
                EnterPreview(item);
            }
            else
            {
                _pendingPreview = item;
            }
        }

        /// <summary>Edit button.</summary>
        public void EnterEditMode()
        {
            if (_mode == DeskHubMode.Edit)
            {
                return;
            }

            SetMode(DeskHubMode.Edit);
            GameSignals.RaiseEditModeChanged(true);
        }

        /// <summary>"Back to workshop" button.</summary>
        public void ExitEditMode()
        {
            if (_mode != DeskHubMode.Edit)
            {
                return;
            }

            RevertEditSelection();
            SetMode(DeskHubMode.Normal);
            GameSignals.RaiseEditModeChanged(false);
        }

        // ---- Mode switching ----

        private void SetMode(DeskHubMode mode)
        {
            _mode = mode;
            StopShake();

            SetActive(normalObjects, mode == DeskHubMode.Normal);
            SetActive(editObjects, mode == DeskHubMode.Edit);
            SetActive(previewObjects, mode == DeskHubMode.Preview);

            if (notEnoughCoinsLabel != null)
            {
                notEnoughCoinsLabel.gameObject.SetActive(false);
            }

            if (room != null)
            {
                switch (mode)
                {
                    case DeskHubMode.Edit:
                        room.SetSelected(null);
                        room.SetInteraction(true, null);
                        break;
                    case DeskHubMode.Preview:
                        room.SetSelected(_previewView);
                        room.SetInteraction(false, _previewView);
                        break;
                    default:
                        room.SetSelected(null);
                        room.SetInteraction(false, null);
                        break;
                }
            }

            if (mode == DeskHubMode.Edit)
            {
                _editSelected = null;
                ShowEditSelection(false);
            }

            SetPreviewButtonsInteractable(true);
            ModeChanged?.Invoke(mode);
        }

        // ---- Normal mode buttons ----

        private void OpenSettings()
        {
            if (overlays != null)
            {
                overlays.OpenSettings();
            }
            else
            {
                Debug.LogWarning("[DeskHubScreen] Overlays is not assigned.", this);
            }
        }

        private void GoToShop()
        {
            if (flow != null)
            {
                flow.GoToShop();
            }
            else
            {
                Debug.LogWarning("[DeskHubScreen] Flow is not assigned.", this);
            }
        }

        private void OpenJournal()
        {
            if (flow != null)
            {
                flow.OpenJournalFromDesk();
            }
            else
            {
                Debug.LogWarning("[DeskHubScreen] Flow is not assigned.", this);
            }
        }

        // ---- Edit mode ----

        private void OnRoomViewClicked(DecorationView view)
        {
            if (_mode != DeskHubMode.Edit || view == null || view.IsPreview || view == _editSelected)
            {
                return;
            }

            // Tapping a different item abandons the unplaced move of the old one.
            RevertEditSelection();

            _editSelected = view;
            _undoX = view.NormalizedX;
            _undoY = view.NormalizedY;

            if (room != null)
            {
                room.SetSelected(view);
                room.SetInteraction(true, view);
            }

            ShowEditSelection(true);
        }

        private void PlaceSelected()
        {
            if (_mode != DeskHubMode.Edit || _editSelected == null)
            {
                return;
            }

            if (_inventory != null)
            {
                _inventory.SetPlacement(_editSelected.ItemId, _editSelected.NormalizedX, _editSelected.NormalizedY);
            }
            else
            {
                Debug.LogWarning("[DeskHubScreen] No IDecorationInventory; the new position is not saved.", this);
            }

            _audio?.PlaySfx(SfxId.ItemPlaced);
            ClearEditSelection();
        }

        private void UndoSelected()
        {
            if (_mode != DeskHubMode.Edit)
            {
                return;
            }

            RevertEditSelection();
        }

        /// <summary>Puts the selected item back where it was and deselects it.</summary>
        private void RevertEditSelection()
        {
            if (_editSelected != null)
            {
                _editSelected.SetNormalized(_undoX, _undoY);
            }

            ClearEditSelection();
        }

        private void ClearEditSelection()
        {
            _editSelected = null;

            if (room != null && _mode == DeskHubMode.Edit)
            {
                room.SetSelected(null);
                room.SetInteraction(true, null);
            }

            ShowEditSelection(false);
        }

        private void ShowEditSelection(bool selected)
        {
            if (editIdleGroup != null)
            {
                editIdleGroup.SetActive(!selected);
            }

            if (editSelectedGroup != null)
            {
                editSelectedGroup.SetActive(selected);
            }

            if (selected)
            {
                DrawEditLabels();
            }
        }

        private void DrawEditLabels()
        {
            var item = _editSelected != null ? _editSelected.Item : null;

            if (item == null)
            {
                return;
            }

            SetText(editNameLabel, Format(KeySelected, L(item.NameKeyOrDefault)));
            SetText(editDescLabel, Format(KeyDesc, L(item.DescriptionKeyOrDefault)));
        }

        // ---- Preview mode ----

        private void EnterPreview(ShopItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId))
            {
                Debug.LogWarning("[DeskHubScreen] RequestPreview got no item; showing the desk.", this);
                SetMode(DeskHubMode.Normal);
                return;
            }

            if (_mode == DeskHubMode.Edit)
            {
                RevertEditSelection();
                GameSignals.RaiseEditModeChanged(false);
            }

            if (_inventory != null && _inventory.Owns(item.itemId))
            {
                // Already bought (shouldn't happen: owned cards are disabled).
                SetMode(DeskHubMode.Normal);
                return;
            }

            if (room == null)
            {
                Debug.LogWarning("[DeskHubScreen] Room is not assigned; cannot preview.", this);
                SetMode(DeskHubMode.Normal);
                return;
            }

            _previewItem = item;
            _previewView = room.ShowPreview(item);
            SetMode(DeskHubMode.Preview);
            DrawPreviewLabels();
            GameSignals.RaisePreviewOpened(item.itemId);
        }

        private void DrawPreviewLabels()
        {
            if (_previewItem == null)
            {
                return;
            }

            SetText(previewNameLabel, Format(KeySelected, L(_previewItem.NameKeyOrDefault)));
            SetText(previewPriceLabel, Format(KeyPrice, _previewItem.price.ToString()));
            SetText(previewDescLabel, Format(KeyDesc, L(_previewItem.DescriptionKeyOrDefault)));

            if (notEnoughCoinsLabel != null && notEnoughCoinsLabel.gameObject.activeSelf)
            {
                notEnoughCoinsLabel.text = L(KeyNotEnough);
            }
        }

        private void BuyPreviewItem()
        {
            if (_mode != DeskHubMode.Preview || _leaving || _previewItem == null || _previewView == null)
            {
                return;
            }

            if (_inventory == null)
            {
                Debug.LogWarning("[DeskHubScreen] No IDecorationInventory registered; cannot buy.", this);
                return;
            }

            var item = _previewItem;
            var result = _inventory.TryPurchase(item.itemId, item.price,
                _previewView.NormalizedX, _previewView.NormalizedY);

            switch (result)
            {
                case PurchaseResult.Success:
                    // DecorationRoom turns the preview view into the owned view
                    // (it listens to ItemPurchased), so nothing moves or flickers.
                    _audio?.PlaySfx(SfxId.Purchase);
                    _previewItem = null;
                    _previewView = null;
                    SetMode(DeskHubMode.Normal);
                    break;

                case PurchaseResult.NotEnoughCoins:
                    ShowNotEnoughCoins();
                    break;

                case PurchaseResult.AlreadyOwned:
                    if (room != null) room.ClearPreview();
                    _previewItem = null;
                    _previewView = null;
                    SetMode(DeskHubMode.Normal);
                    break;

                default:
                    Debug.LogWarning($"[DeskHubScreen] Purchase of '{item.itemId}' failed: {result}.", this);
                    GiveUpPreview();
                    break;
            }
        }

        private void GiveUpPreview()
        {
            if (_mode != DeskHubMode.Preview || _leaving)
            {
                return;
            }

            // Keep the preview on screen during the router's short hold; OnHidden
            // removes it. Buttons are locked so a double tap can't buy on the way out.
            _leaving = true;
            SetPreviewButtonsInteractable(false);

            if (flow != null)
            {
                flow.GoToShop();
            }
            else
            {
                Debug.LogWarning("[DeskHubScreen] Flow is not assigned; staying on the desk.", this);
                if (room != null) room.ClearPreview();
                _previewItem = null;
                _previewView = null;
                _leaving = false;
                SetMode(DeskHubMode.Normal);
            }
        }

        private void SetPreviewButtonsInteractable(bool on)
        {
            if (buyButton != null)
            {
                buyButton.interactable = on;
            }

            if (giveUpButton != null)
            {
                giveUpButton.interactable = on;
            }
        }

        // ---- Not enough coins ----

        private void ShowNotEnoughCoins()
        {
            if (notEnoughCoinsLabel != null)
            {
                notEnoughCoinsLabel.text = L(KeyNotEnough);
                notEnoughCoinsLabel.gameObject.SetActive(true);
            }

            StopShake();
            _shakeTargets[0] = previewPriceLabel != null ? previewPriceLabel.rectTransform : null;
            _shakeTargets[1] = previewCoinsBadge;

            for (var i = 0; i < _shakeTargets.Length; i++)
            {
                _shakeOrigins[i] = _shakeTargets[i] != null ? _shakeTargets[i].anchoredPosition : Vector2.zero;
            }

            if (previewPriceLabel != null)
            {
                if (!_priceColorCached)
                {
                    _priceColor = previewPriceLabel.color;
                    _priceColorCached = true;
                }

                previewPriceLabel.color = shortPriceColor;
            }

            _shake = StartCoroutine(Shake());
        }

        private IEnumerator Shake()
        {
            var t = 0f;
            var duration = Mathf.Max(0.05f, shakeSeconds);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var k = 1f - Mathf.Clamp01(t / duration);
                var offset = Mathf.Sin(t * 60f) * shakePixels * k;

                for (var i = 0; i < _shakeTargets.Length; i++)
                {
                    if (_shakeTargets[i] != null)
                    {
                        _shakeTargets[i].anchoredPosition = _shakeOrigins[i] + new Vector2(offset, 0f);
                    }
                }

                yield return null;
            }

            // Leave the price red a moment longer than the shake itself.
            var hold = 0f;

            while (hold < 0.6f)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            _shake = null;
            RestoreShakeTargets();
        }

        private void StopShake()
        {
            if (_shake != null)
            {
                StopCoroutine(_shake);
                _shake = null;
                RestoreShakeTargets();
            }
        }

        private void RestoreShakeTargets()
        {
            for (var i = 0; i < _shakeTargets.Length; i++)
            {
                if (_shakeTargets[i] != null)
                {
                    _shakeTargets[i].anchoredPosition = _shakeOrigins[i];
                }

                _shakeTargets[i] = null;
            }

            if (previewPriceLabel != null && _priceColorCached)
            {
                previewPriceLabel.color = _priceColor;
            }
        }

        // ---- Services & localisation ----

        private void ResolveServices()
        {
            if (_inventory == null)
            {
                _inventory = ServiceLocator.Get<IDecorationInventory>();

                if (_inventory == null)
                {
                    Debug.LogWarning("[DeskHubScreen] No IDecorationInventory registered (scene opened " +
                                     "without the Title scene?). Buying and placing won't save.", this);
                }
            }

            if (_audio == null)
            {
                _audio = ServiceLocator.Get<IAudioService>();
            }

            if (_loc == null)
            {
                _loc = ServiceLocator.Get<ILocalizationService>();
            }
        }

        private void SubscribeLocale()
        {
            if (_locSubscribed || _loc == null)
            {
                return;
            }

            _loc.LocaleChanged += OnLocaleChanged;
            _locSubscribed = true;
        }

        private void UnsubscribeLocale()
        {
            if (_locSubscribed && _loc != null)
            {
                _loc.LocaleChanged -= OnLocaleChanged;
            }

            _locSubscribed = false;
        }

        private void OnLocaleChanged()
        {
            if (_mode == DeskHubMode.Preview)
            {
                DrawPreviewLabels();
            }
            else if (_mode == DeskHubMode.Edit && _editSelected != null)
            {
                DrawEditLabels();
            }
        }

        private string L(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return _loc != null ? _loc.Get(key) : key;
        }

        private string Format(string key, string arg)
        {
            if (_loc != null)
            {
                return _loc.Format(key, arg);
            }

            return key + " " + arg;
        }

        // ---- Small helpers ----

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }

        private static void SetActive(GameObject[] objects, bool on)
        {
            if (objects == null)
            {
                return;
            }

            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].activeSelf != on)
                {
                    objects[i].SetActive(on);
                }
            }
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
