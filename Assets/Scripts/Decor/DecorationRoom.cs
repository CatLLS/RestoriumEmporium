// ============================================================
// DecorationRoom — draws every owned decoration inside the desk-hub room.
// WHAT & WHY: The room is the RectTransform the player decorates. This component
//   reads WHAT the player owns and WHERE (IDecorationInventory) and WHAT each
//   item looks like (ShopCatalog), and keeps exactly one DecorationView per owned
//   item on screen. It also hosts the temporary preview item shown before a
//   purchase, and forwards drags that start on empty floor to the item being
//   moved, so a small item is still easy to drag with a thumb.
// KEY DECISIONS:
//   - Views are created from code and pooled (a hidden view is reused for the
//     next item), so the shop can grow with no scene edits and no prefab.
//   - Refreshes on IDecorationInventory.ItemPurchased and PlacementChanged, and
//     on enable. PlacementChanged only moves that one view (unless it is being
//     dragged right now).
//   - Draw order = ShopItemData.sortingOrder (then catalogue order), applied as
//     sibling order after every refresh.
//   - The room does not decide modes; DeskHubScreen tells it who may be selected
//     and which single view may be dragged (SetInteraction). Only one item can be
//     dragged at a time, by design.
//   - The room's own Image is fully transparent and only takes raycasts while a
//     drag is allowed, so in Normal mode taps fall through to the book.
//   - The "preview.item" tutorial anchor is ONE runtime child object that is
//     re-parented onto whatever item is being previewed, so exactly one anchor
//     with that id ever exists.
//   - Services are resolved in Start (never Awake) and a missing service is a
//     warning, not a crash (Game scene opened directly in the Editor).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Under DeskHubScreen, right-click -> UI -> Image. Name it exactly: Room
//     Rect Transform: anchor preset stretch/stretch (Alt+Shift, bottom-right box),
//     Left/Right/Top/Bottom = 0 (the room is the whole 412x917 screen in Figma).
// [ ] On its Image: leave Source Image empty, set Color alpha (A) to 0 so it is
//     invisible. (It must stay an Image: it catches drags on empty floor.)
// [ ] Add Component -> Decoration Room.
// [ ] Catalog <- Assets/Data/Catalogs/ShopCatalog.asset
// [ ] Leave Highlight Color / Highlight Distance at their defaults unless the
//     selection outline looks wrong on the art.
// [ ] Do NOT put any children under Room: decorations are created at runtime.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoriumEmporium.Decor
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Economy;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class DecorationRoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        public const string PreviewAnchorId = "preview.item";

        [Header("Data")]
        [Tooltip("Assets/Data/Catalogs/ShopCatalog.asset — what each owned item looks like.")]
        [SerializeField] private ShopCatalog catalog;

        [Header("Selection look")]
        [Tooltip("Outline colour of the selected / previewed item.")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.92f, 0.55f, 0.95f);

        [Tooltip("Outline thickness in reference pixels.")]
        [SerializeField] private Vector2 highlightDistance = new Vector2(3f, -3f);

        [Header("Optional")]
        [Tooltip("Transparent Image on this object that catches drags on empty floor. " +
                 "Found automatically when left empty.")]
        [SerializeField] private Image dragCatcher;

        private readonly Dictionary<string, DecorationView> _views = new Dictionary<string, DecorationView>();
        private readonly List<DecorationView> _pool = new List<DecorationView>();
        private readonly List<DecorationView> _sortBuffer = new List<DecorationView>();
        private readonly List<string> _releaseBuffer = new List<string>();

        private RectTransform _rect;
        private Camera _canvasCamera;
        private IDecorationInventory _inventory;
        private bool _subscribed;

        private bool _selectable;
        private DecorationView _dragTarget;
        private DecorationView _selected;
        private DecorationView _preview;
        private RectTransform _previewAnchor;
        private bool _forwardingDrag;

        /// <summary>A view was tapped while selection is allowed.</summary>
        public event Action<DecorationView> ViewClicked;

        /// <summary>A drag on a view finished (the view has already moved).</summary>
        public event Action<DecorationView> ViewDragEnded;

        /// <summary>A drag on a view started.</summary>
        public event Action<DecorationView> ViewDragStarted;

        public ShopCatalog Catalog => catalog;

        /// <summary>The room rect in its own local space, as RoomCoordinates wants it.</summary>
        public RoomRect RoomRect
        {
            get
            {
                if (_rect == null)
                {
                    _rect = (RectTransform)transform;
                }

                var r = _rect.rect;
                return new RoomRect(r.x, r.y, r.width, r.height);
            }
        }

        /// <summary>The temporary preview view, or null.</summary>
        public DecorationView PreviewView => _preview;

        private void Awake()
        {
            _rect = (RectTransform)transform;

            if (dragCatcher == null)
            {
                dragCatcher = GetComponent<Image>();
            }

            if (dragCatcher != null)
            {
                dragCatcher.raycastTarget = false;
            }

            var canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                _canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }

            CreatePreviewAnchor();
        }

        private void OnEnable()
        {
            Subscribe();

            if (_inventory != null)
            {
                Refresh();
            }
        }

        private void Start()
        {
            if (_inventory == null)
            {
                _inventory = ServiceLocator.Get<IDecorationInventory>();

                if (_inventory == null)
                {
                    Debug.LogWarning("[DecorationRoom] No IDecorationInventory registered (scene opened " +
                                     "without the Title scene's Systems object?). The room stays empty.", this);
                }
            }

            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnRectTransformDimensionsChange()
        {
            // The room resized (first layout, resolution change): pixels change,
            // normalised positions don't.
            foreach (var pair in _views)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetNormalized(pair.Value.NormalizedX, pair.Value.NormalizedY);
                }
            }
        }

        private void Subscribe()
        {
            if (_subscribed || _inventory == null)
            {
                return;
            }

            _inventory.ItemPurchased += OnItemPurchased;
            _inventory.PlacementChanged += OnPlacementChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _inventory == null)
            {
                return;
            }

            _inventory.ItemPurchased -= OnItemPurchased;
            _inventory.PlacementChanged -= OnPlacementChanged;
            _subscribed = false;
        }

        private void OnItemPurchased(string itemId)
        {
            // The preview view (if it is this item) becomes the owned view in place.
            if (_preview != null && _preview.ItemId == itemId)
            {
                _preview.IsPreview = false;
                _preview = null;
                HidePreviewAnchor();
            }

            Refresh();
        }

        private void OnPlacementChanged(string itemId)
        {
            if (_inventory == null || !_views.TryGetValue(itemId, out var view) || view == null ||
                view.IsDragging || view.IsPreview)
            {
                return;
            }

            var owned = _inventory.Get(itemId);

            if (owned != null)
            {
                view.SetNormalized(owned.x, owned.y);
            }
        }

        // ---- Public API (used by DeskHubScreen) ----

        /// <summary>Rebuilds the views from the inventory. Allocation-light; not per frame.</summary>
        public void Refresh()
        {
            if (catalog == null)
            {
                Debug.LogWarning("[DecorationRoom] No ShopCatalog assigned. Drag " +
                                 "Assets/Data/Catalogs/ShopCatalog.asset into 'Catalog'.", this);
                return;
            }

            var owned = _inventory != null ? _inventory.Owned : null;

            // 1) Release views whose item is no longer owned (keeps the preview).
            _releaseBuffer.Clear();

            foreach (var pair in _views)
            {
                if (pair.Value == null || (!pair.Value.IsPreview && (_inventory == null || !_inventory.Owns(pair.Key))))
                {
                    _releaseBuffer.Add(pair.Key);
                }
            }

            foreach (var id in _releaseBuffer)
            {
                Release(id);
            }

            // 2) One view per owned item that exists in the catalogue.
            if (owned != null)
            {
                for (var i = 0; i < owned.Count; i++)
                {
                    var record = owned[i];

                    if (record == null)
                    {
                        continue;
                    }

                    var item = catalog.Find(record.itemId);

                    if (item == null)
                    {
                        // Owned in the save but removed from the catalogue: keep the
                        // save untouched, just don't draw it.
                        continue;
                    }

                    if (_views.TryGetValue(record.itemId, out var view) && view != null)
                    {
                        if (!view.IsDragging && !view.IsPreview && view != _selected)
                        {
                            view.SetNormalized(record.x, record.y);
                        }

                        continue;
                    }

                    view = Acquire();
                    view.Bind(item, record.x, record.y);
                    _views[record.itemId] = view;
                }
            }

            ApplyInteractionToAll();
            SortViews();
        }

        /// <summary>
        /// Shows <paramref name="item"/> as a temporary, draggable preview at its
        /// default position. Returns the view (null if the item is invalid).
        /// </summary>
        public DecorationView ShowPreview(ShopItemData item)
        {
            ClearPreview();

            if (item == null || string.IsNullOrEmpty(item.itemId))
            {
                return null;
            }

            if (_views.TryGetValue(item.itemId, out var existing) && existing != null)
            {
                // Already owned: nothing to preview; hand back the real view.
                return existing;
            }

            var view = Acquire();
            view.IsPreview = true;
            view.Bind(item, item.defaultPosition.x, item.defaultPosition.y);
            _views[item.itemId] = view;
            _preview = view;
            ShowPreviewAnchor(view);
            ApplyInteractionToAll();
            SortViews();
            return view;
        }

        /// <summary>Removes the preview item (if not bought).</summary>
        public void ClearPreview()
        {
            if (_preview == null)
            {
                return;
            }

            var id = _preview.ItemId;
            _preview = null;
            HidePreviewAnchor();
            Release(id);
        }

        /// <summary>The view for an owned (or previewed) item, or null.</summary>
        public DecorationView Find(string itemId) =>
            !string.IsNullOrEmpty(itemId) && _views.TryGetValue(itemId, out var v) ? v : null;

        /// <summary>
        /// Who may be tapped (<paramref name="selectable"/>) and which single view
        /// may be dragged (<paramref name="dragTarget"/>, null = none).
        /// </summary>
        public void SetInteraction(bool selectable, DecorationView dragTarget)
        {
            _selectable = selectable;
            _dragTarget = dragTarget;
            ApplyInteractionToAll();
        }

        /// <summary>Highlights one view (null clears).</summary>
        public void SetSelected(DecorationView view)
        {
            if (_selected != null && _selected != view)
            {
                _selected.SetSelected(false);
            }

            _selected = view;

            if (_selected != null)
            {
                _selected.SetSelected(true);
            }
        }

        // ---- Coordinate helper for the views ----

        /// <summary>Screen point of the event -> room-local pixels, using the canvas camera.</summary>
        public bool ScreenToRoomLocal(PointerEventData eventData, out float x, out float y)
        {
            var cam = _canvasCamera != null ? _canvasCamera : eventData.pressEventCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, cam, out var local))
            {
                x = local.x;
                y = local.y;
                return true;
            }

            x = y = 0f;
            return false;
        }

        // ---- Callbacks from views ----

        internal void NotifyClicked(DecorationView view) => ViewClicked?.Invoke(view);

        internal void NotifyDragStarted(DecorationView view) => ViewDragStarted?.Invoke(view);

        internal void NotifyDragEnded(DecorationView view) => ViewDragEnded?.Invoke(view);

        // ---- Drags / taps that start on empty floor ----

        public void OnBeginDrag(PointerEventData eventData)
        {
            _forwardingDrag = _dragTarget != null && _dragTarget.isActiveAndEnabled;

            if (_forwardingDrag)
            {
                _dragTarget.BeginDragFrom(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_forwardingDrag && _dragTarget != null)
            {
                _dragTarget.DragFrom(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_forwardingDrag && _dragTarget != null)
            {
                _dragTarget.EndDragFrom(eventData);
            }

            _forwardingDrag = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Needed so the floor is a valid click target (a tap on empty floor
            // must not fall through to whatever is behind the room). Nothing to do.
        }

        // ---- Internals ----

        private void ApplyInteractionToAll()
        {
            foreach (var pair in _views)
            {
                var view = pair.Value;

                if (view == null)
                {
                    continue;
                }

                // The preview item is never "selectable"; it is dragged directly.
                view.SetInteraction(_selectable && !view.IsPreview, view == _dragTarget);
            }

            if (dragCatcher != null)
            {
                dragCatcher.raycastTarget = _dragTarget != null;
            }
        }

        private void SortViews()
        {
            _sortBuffer.Clear();

            foreach (var pair in _views)
            {
                if (pair.Value != null)
                {
                    _sortBuffer.Add(pair.Value);
                }
            }

            _sortBuffer.Sort(CompareViews);

            for (var i = 0; i < _sortBuffer.Count; i++)
            {
                _sortBuffer[i].transform.SetSiblingIndex(i);
            }

            _sortBuffer.Clear();
        }

        private int CompareViews(DecorationView a, DecorationView b)
        {
            var sa = a.Item != null ? a.Item.sortingOrder : 0;
            var sb = b.Item != null ? b.Item.sortingOrder : 0;

            if (sa != sb)
            {
                return sa.CompareTo(sb);
            }

            var ia = IndexInCatalog(a.Item);
            var ib = IndexInCatalog(b.Item);
            return ia.CompareTo(ib);
        }

        private int IndexInCatalog(ShopItemData item)
        {
            if (catalog == null || item == null)
            {
                return int.MaxValue;
            }

            var items = catalog.Items;

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] == item)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private DecorationView Acquire()
        {
            DecorationView view;

            if (_pool.Count > 0)
            {
                view = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                var go = new GameObject("Decoration", typeof(RectTransform), typeof(Image));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                view = go.AddComponent<DecorationView>();
                view.Init(this, highlightColor, highlightDistance);
            }

            view.IsPreview = false;
            view.SetSelected(false);
            view.gameObject.SetActive(true);
            return view;
        }

        private void Release(string itemId)
        {
            if (!_views.TryGetValue(itemId, out var view))
            {
                return;
            }

            _views.Remove(itemId);

            if (view == null)
            {
                return;
            }

            if (view == _selected)
            {
                _selected = null;
            }

            if (view == _dragTarget)
            {
                _dragTarget = null;
            }

            view.IsPreview = false;
            view.SetSelected(false);
            view.SetInteraction(false, false);
            view.gameObject.SetActive(false);
            _pool.Add(view);
        }

        private void CreatePreviewAnchor()
        {
            var go = new GameObject("PreviewItemAnchor", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.SetActive(false);
            _previewAnchor = (RectTransform)go.transform;
            _previewAnchor.SetParent(transform, false);
            RuntimeTutorialAnchor.Attach(go, PreviewAnchorId);
        }

        private void ShowPreviewAnchor(DecorationView view)
        {
            if (_previewAnchor == null)
            {
                return;
            }

            // Stretch over the previewed item so the tutorial hand points at it
            // and follows it while it is dragged.
            _previewAnchor.SetParent(view.Rect, false);
            _previewAnchor.anchorMin = Vector2.zero;
            _previewAnchor.anchorMax = Vector2.one;
            _previewAnchor.offsetMin = Vector2.zero;
            _previewAnchor.offsetMax = Vector2.zero;
            _previewAnchor.gameObject.SetActive(true);
        }

        private void HidePreviewAnchor()
        {
            if (_previewAnchor == null)
            {
                return;
            }

            _previewAnchor.gameObject.SetActive(false);
            _previewAnchor.SetParent(transform, false);
        }
    }
}
