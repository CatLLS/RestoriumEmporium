// ============================================================
// DecorationView — one decoration drawn in the desk-hub room (lamp, plant...).
// WHAT & WHY: Every owned item, and the item being previewed before purchase, is
//   one of these: an Image showing ShopItemData.placedSprite at its authored
//   size. In Edit and Preview mode the player can tap it (select) and drag it;
//   the view only moves itself, it never saves — DeskHubScreen decides when a
//   position becomes permanent (Place Item / Buy Item).
// KEY DECISIONS:
//   - Created and pooled by DecorationRoom at runtime, never placed in the scene
//     by hand, so adding a shop item needs no scene edit.
//   - Position is kept as normalised 0..1 room coordinates (the save format) and
//     converted to pixels with RoomCoordinates, which also stops the item from
//     leaving the room.
//   - Drag uses IBeginDrag/IDrag/IEndDrag with the CANVAS camera (Screen Space -
//     Camera needs it for screen->local conversion). The finger's offset from
//     the item centre is remembered on BeginDrag so the item doesn't jump under
//     the finger. Nothing allocates on the drag path.
//   - Drag and click are only honoured when the room says so (DragEnabled /
//     Selectable). In Normal mode the Image doesn't even take raycasts, so the
//     decorations never block the book or the buttons underneath.
//   - Selected / previewed look = the item's shopIcon instead of its placedSprite
//     (Figma "item being previewed (outlined)" reuses icon.png). The icon art has
//     the white outline baked in. The old UI Outline effect drew four offset
//     tinted copies of the sprite, which read as a doubled item, not an outline.
//     An item with no shopIcon keeps its placedSprite when selected.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. DecorationRoom creates these objects by itself at runtime
//     (one per owned item). Do NOT add this component in the scene.
// ---------------------------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RestoriumEmporium.Decor
{
    using RestoriumEmporium.Data;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class DecorationView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private RectTransform _rect;
        private Image _image;
        private DecorationRoom _room;
        private ShopItemData _item;

        private float _nx = RoomCoordinates.Centre;
        private float _ny = RoomCoordinates.Centre;
        private float _dragOffsetX;
        private float _dragOffsetY;
        private bool _dragging;
        private bool _selected;
        private bool _dragEnabled;
        private bool _selectable;

        public ShopItemData Item => _item;
        public string ItemId => _item != null ? _item.itemId : string.Empty;
        public RectTransform Rect => _rect;
        public float NormalizedX => _nx;
        public float NormalizedY => _ny;
        public bool IsDragging => _dragging;
        public bool IsSelected => _selected;

        /// <summary>True while this view is the room's temporary preview (not owned yet).</summary>
        public bool IsPreview { get; internal set; }

        /// <summary>Called once by DecorationRoom right after AddComponent.</summary>
        internal void Init(DecorationRoom room)
        {
            _room = room;
            _rect = (RectTransform)transform;
            _image = GetComponent<Image>();
            _image.preserveAspect = true;
            _image.raycastTarget = false;

            // Centre anchors + centre pivot: localPosition is then exactly the
            // room-local point RoomCoordinates works in.
            _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Shows <paramref name="item"/> at a normalised room position.</summary>
        internal void Bind(ShopItemData item, float nx, float ny)
        {
            _item = item;
            gameObject.name = "Decoration_" + (item != null ? item.itemId : "none");
            ApplySprite();

            // "room.item.<id>" lets the tutorial point at a placed item (edit mode).
            // Views are pooled, so a re-bind simply re-ids the same anchor.
            if (item != null)
            {
                RuntimeTutorialAnchor.Attach(gameObject, "room.item." + item.itemId);
            }
            _rect.sizeDelta = item != null ? item.placedSize : new Vector2(100f, 100f);
            _rect.localScale = Vector3.one;
            SetNormalized(nx, ny);
        }

        /// <summary>Moves the item (clamped inside the room). Does not save.</summary>
        public void SetNormalized(float nx, float ny)
        {
            var room = _room != null ? _room.RoomRect : default;
            var size = _rect.sizeDelta;
            RoomCoordinates.Clamp(room, size.x, size.y, ref nx, ref ny);
            _nx = nx;
            _ny = ny;
            ApplyPosition();
        }

        /// <summary>Re-applies the stored normalised position (after the room resized).</summary>
        internal void ApplyPosition()
        {
            if (_room == null)
            {
                return;
            }

            RoomCoordinates.ToLocal(_room.RoomRect, _nx, _ny, out var lx, out var ly);
            _rect.localPosition = new Vector3(lx, ly, 0f);
        }

        /// <summary>What the player may do with this view right now.</summary>
        internal void SetInteraction(bool selectable, bool dragEnabled)
        {
            _selectable = selectable;
            _dragEnabled = dragEnabled;
            _image.raycastTarget = selectable || dragEnabled;

            if (!dragEnabled && _dragging)
            {
                _dragging = false;
            }
        }

        internal void SetSelected(bool selected)
        {
            _selected = selected;
            ApplySprite();
        }

        /// <summary>placedSprite normally; shopIcon (outlined art) while selected.</summary>
        private void ApplySprite()
        {
            Sprite sprite = null;

            if (_item != null)
            {
                sprite = _selected && _item.shopIcon != null ? _item.shopIcon : _item.placedSprite;
            }

            _image.sprite = sprite;
            _image.enabled = sprite != null;
        }

        // ---- Input ----

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_selectable && _room != null && !eventData.dragging)
            {
                _room.NotifyClicked(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData) => BeginDragFrom(eventData);

        public void OnDrag(PointerEventData eventData) => DragFrom(eventData);

        public void OnEndDrag(PointerEventData eventData) => EndDragFrom(eventData);

        /// <summary>Also called by the room when the finger starts on empty floor.</summary>
        internal void BeginDragFrom(PointerEventData eventData)
        {
            if (!_dragEnabled || _room == null)
            {
                return;
            }

            if (!_room.ScreenToRoomLocal(eventData, out var lx, out var ly))
            {
                return;
            }

            var pos = _rect.localPosition;
            _dragOffsetX = pos.x - lx;
            _dragOffsetY = pos.y - ly;
            _dragging = true;
            _room.NotifyDragStarted(this);
        }

        internal void DragFrom(PointerEventData eventData)
        {
            if (!_dragging || _room == null)
            {
                return;
            }

            if (!_room.ScreenToRoomLocal(eventData, out var lx, out var ly))
            {
                return;
            }

            var size = _rect.sizeDelta;
            RoomCoordinates.LocalToClampedNormalized(_room.RoomRect, lx + _dragOffsetX, ly + _dragOffsetY,
                size.x, size.y, out _nx, out _ny);
            ApplyPosition();
        }

        internal void EndDragFrom(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;

            if (_room != null)
            {
                _room.NotifyDragEnded(this);
            }
        }
    }
}
