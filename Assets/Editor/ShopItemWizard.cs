// ============================================================
// ShopItemWizard — one window to add (or edit) a decoration for the shop.
// WHAT & WHY: Adding a shop item must be a one-minute job for someone who does
//   not know Unity: fill a form, press Create. The window creates the
//   ShopItemData asset in Assets/Data/ShopItems/, writes its name and description
//   into BOTH locale tables (pt-BR and en), and rebuilds the catalogues, so the
//   item appears in the shop grid and can be bought and placed immediately — no
//   code, no scene edits.
//   Also hosts "Restorium > Shop > Create Starter Items" (lamp, plant, books).
// KEY DECISIONS:
//   - Strings go through LocaleTableBuilder.Upsert (owned by the UI tools), which
//     never deletes keys, so a later "Rebuild Catalogs & Locale Tables" keeps them.
//     The window also prints a ready-to-paste LocaleSource line to the Console so
//     the text can be made permanent in LocaleSource.Shop.cs if wanted.
//   - Editing an existing item updates the asset IN PLACE (same GUID), so scenes
//     and saves that reference it keep working. The item id is locked once the
//     asset exists: it is the save-file identity.
//   - The id is validated (lowercase letters, digits, '_' / '-') because it is
//     used in file names, locale keys and tutorial anchor ids ("shop.item.<id>").
//   - Sprites picked here get their importer switched to Sprite (2D and UI) if
//     needed, a common beginner stumbling block.
//   - Starter items read art from Assets/Art/ShopItems/<id>/icon.png and
//     placed.png. Their strings live in LocaleSource.Shop.cs (permanent), and the
//     positions/sizes are taken from the Figma PreviewMode / edit-mode frames.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach — Editor script.
// [ ] First time: menu Restorium -> Shop -> Create Starter Items (needs the art in
//     Assets/Art/ShopItems/lamp|plant|books/icon.png + placed.png).
// [ ] New item: menu Restorium -> Shop -> New Shop Item. Fill the form (tooltips
//     explain each field) and press "Create Item". Done — press Play and look in
//     the shop.
// [ ] Edit an item: open the same window, drag the item asset into "Edit Existing",
//     change fields, press "Save Changes".
// ---------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    public class ShopItemWizard : EditorWindow
    {
        private const string ArtRoot = "Assets/Art/ShopItems";
        private static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_-]{0,39}$");

        // ---- Form state ----
        private ShopItemData _editing;
        private string _itemId = string.Empty;
        private ShopCategory _category = ShopCategory.Decor;
        private int _price = 50;
        private int _displayOrder = -1;
        private Sprite _shopIcon;
        private Sprite _placedSprite;
        private Vector2 _placedSize = new Vector2(120f, 120f);
        private Vector2 _defaultPosition = new Vector2(0.5f, 0.5f);
        private int _sortingOrder;
        private string _nameEn = string.Empty;
        private string _namePt = string.Empty;
        private string _descEn = string.Empty;
        private string _descPt = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Restorium/Shop/New Shop Item", priority = 20)]
        public static void Open()
        {
            var window = GetWindow<ShopItemWizard>(true, "New Shop Item");
            window.minSize = new Vector2(420f, 560f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Fill the form and press Create. The item appears in the shop automatically.\n" +
                "Sizes and positions: copy them from Figma (412x917 frame).", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            var picked = (ShopItemData)EditorGUILayout.ObjectField(
                new GUIContent("Edit Existing (optional)", "Drag an existing shop item here to change it."),
                _editing, typeof(ShopItemData), false);

            if (EditorGUI.EndChangeCheck())
            {
                LoadFrom(picked);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_editing != null))
            {
                _itemId = EditorGUILayout.TextField(
                    new GUIContent("Item Id", "Lowercase, no spaces, e.g. \"rug\". Saved in the player's save " +
                                              "file: never change it after release."), _itemId).Trim();
            }

            _category = (ShopCategory)EditorGUILayout.EnumPopup(new GUIContent("Shop Tab"), _category);
            _price = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("Price (coins)"), _price));
            _displayOrder = EditorGUILayout.IntField(
                new GUIContent("Display Order", "Position in the grid, lower first. -1 = put it last."), _displayOrder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Art", EditorStyles.boldLabel);
            _shopIcon = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Shop Icon", "Picture on the shop card (e.g. Assets/Art/ShopItems/<id>/icon.png)."),
                _shopIcon, typeof(Sprite), false);
            _placedSprite = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Placed Sprite", "What sits in the room (e.g. Assets/Art/ShopItems/<id>/placed.png)."),
                _placedSprite, typeof(Sprite), false);

            if (_shopIcon == null || _placedSprite == null)
            {
                EditorGUILayout.HelpBox("Can't drop a PNG here? Select it in the Project window, set Texture Type " +
                                        "to 'Sprite (2D and UI)' and press Apply. (Or use the Texture pickers " +
                                        "below.)", MessageType.None);
                PickTexture("Shop Icon from PNG", ref _shopIcon);
                PickTexture("Placed Sprite from PNG", ref _placedSprite);
            }

            _placedSize = EditorGUILayout.Vector2Field(
                new GUIContent("Placed Size (px)", "Width/height in the room, in 412x917 pixels (Figma W/H)."),
                _placedSize);

            if (_placedSprite != null && GUILayout.Button("Keep width, match the placed sprite's shape"))
            {
                var r = _placedSprite.rect;

                if (r.width > 0f)
                {
                    _placedSize.y = Mathf.Round(_placedSize.x * r.height / r.width);
                }
            }

            _defaultPosition = EditorGUILayout.Vector2Field(
                new GUIContent("Default Position (0..1)", "Where preview mode first puts the item's CENTRE. " +
                                                          "0,0 = bottom-left, 1,1 = top-right."), _defaultPosition);
            _defaultPosition.x = Mathf.Clamp01(_defaultPosition.x);
            _defaultPosition.y = Mathf.Clamp01(_defaultPosition.y);
            _sortingOrder = EditorGUILayout.IntField(
                new GUIContent("Sorting Order", "Higher draws in front of other decorations."), _sortingOrder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Text", EditorStyles.boldLabel);
            _nameEn = EditorGUILayout.TextField("Name (English)", _nameEn);
            _namePt = EditorGUILayout.TextField("Name (Português)", _namePt);
            EditorGUILayout.LabelField("Description (English)");
            _descEn = EditorGUILayout.TextArea(_descEn, GUILayout.MinHeight(48f));
            EditorGUILayout.LabelField("Description (Português)");
            _descPt = EditorGUILayout.TextArea(_descPt, GUILayout.MinHeight(48f));

            EditorGUILayout.Space();
            var error = Validate();

            if (error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button(_editing != null ? "Save Changes" : "Create Item", GUILayout.Height(32f)))
                {
                    var asset = Save();

                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                        _editing = asset;
                    }
                }
            }

            if (GUILayout.Button("Clear form"))
            {
                LoadFrom(null);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void PickTexture(string label, ref Sprite target)
        {
            var tex = (Texture2D)EditorGUILayout.ObjectField(label, null, typeof(Texture2D), false);

            if (tex != null)
            {
                var sprite = EnsureSprite(AssetDatabase.GetAssetPath(tex));

                if (sprite != null)
                {
                    target = sprite;
                }
            }
        }

        private void LoadFrom(ShopItemData item)
        {
            _editing = item;

            if (item == null)
            {
                _itemId = string.Empty;
                _category = ShopCategory.Decor;
                _price = 50;
                _displayOrder = -1;
                _shopIcon = null;
                _placedSprite = null;
                _placedSize = new Vector2(120f, 120f);
                _defaultPosition = new Vector2(0.5f, 0.5f);
                _sortingOrder = 0;
                _nameEn = _namePt = _descEn = _descPt = string.Empty;
                return;
            }

            _itemId = item.itemId;
            _category = item.category;
            _price = item.price;
            _displayOrder = item.displayOrder;
            _shopIcon = item.shopIcon;
            _placedSprite = item.placedSprite;
            _placedSize = item.placedSize;
            _defaultPosition = item.defaultPosition;
            _sortingOrder = item.sortingOrder;
            _nameEn = LookUp(LocaleSource.En, item.NameKeyOrDefault);
            _namePt = LookUp(LocaleSource.PtBR, item.NameKeyOrDefault);
            _descEn = LookUp(LocaleSource.En, item.DescriptionKeyOrDefault);
            _descPt = LookUp(LocaleSource.PtBR, item.DescriptionKeyOrDefault);
        }

        /// <summary>Pre-fills text from LocaleSource so editing a starter item shows its strings.</summary>
        private static string LookUp(string locale, string key)
        {
            foreach (var entry in LocaleSource.All())
            {
                if (entry.Key == key)
                {
                    return locale == LocaleSource.En ? entry.En : entry.PtBR;
                }
            }

            return string.Empty;
        }

        private string Validate()
        {
            if (!IdPattern.IsMatch(_itemId ?? string.Empty))
            {
                return "Item Id: start with a lowercase letter; only a-z, 0-9, '_' or '-' (e.g. \"rug\").";
            }

            if (_editing == null && File.Exists(AssetPathFor(_itemId)))
            {
                return $"An item asset already exists at {AssetPathFor(_itemId)}. Drag it into " +
                       "'Edit Existing' to change it.";
            }

            if (_editing == null && FindById(_itemId) != null)
            {
                return $"Another shop item already uses the id '{_itemId}'.";
            }

            if (_shopIcon == null || _placedSprite == null)
            {
                return "Pick both a Shop Icon and a Placed Sprite.";
            }

            if (_placedSize.x <= 0f || _placedSize.y <= 0f)
            {
                return "Placed Size must be bigger than 0.";
            }

            if (string.IsNullOrWhiteSpace(_nameEn) || string.IsNullOrWhiteSpace(_namePt))
            {
                return "Type the name in both languages.";
            }

            if (string.IsNullOrWhiteSpace(_descEn) || string.IsNullOrWhiteSpace(_descPt))
            {
                return "Type the description in both languages.";
            }

            return null;
        }

        private ShopItemData Save()
        {
            var item = _editing != null
                ? _editing
                : CreateOrLoad(_itemId);

            item.itemId = _itemId;
            item.category = _category;
            item.price = _price;
            item.displayOrder = _displayOrder >= 0 ? _displayOrder : NextDisplayOrder(item);
            item.shopIcon = _shopIcon;
            item.placedSprite = _placedSprite;
            item.placedSize = _placedSize;
            item.defaultPosition = _defaultPosition;
            item.sortingOrder = _sortingOrder;
            item.nameKey = "shop.item." + _itemId + ".name";
            item.descriptionKey = "shop.item." + _itemId + ".desc";
            _displayOrder = item.displayOrder;
            EditorUtility.SetDirty(item);

            LocaleTableBuilder.Upsert(LocaleSource.En, item.nameKey, _nameEn.Trim());
            LocaleTableBuilder.Upsert(LocaleSource.PtBR, item.nameKey, _namePt.Trim());
            LocaleTableBuilder.Upsert(LocaleSource.En, item.descriptionKey, _descEn.Trim());
            LocaleTableBuilder.Upsert(LocaleSource.PtBR, item.descriptionKey, _descPt.Trim());

            AssetDatabase.SaveAssets();
            CatalogBuilder.RebuildAll();

            Debug.Log($"[ShopItemWizard] Saved '{_itemId}' at {AssetDatabase.GetAssetPath(item)}. " +
                      "To make its text permanent, paste into LocaleSource.Shop.cs:\n" +
                      $"            E(\"{item.nameKey}\", \"{Escape(_namePt)}\", \"{Escape(_nameEn)}\"),\n" +
                      $"            E(\"{item.descriptionKey}\", \"{Escape(_descPt)}\", \"{Escape(_descEn)}\"),",
                      item);
            return item;
        }

        // ---- Shared helpers (also used by the starter items) ----

        internal static string AssetPathFor(string itemId) =>
            CatalogBuilder.ShopItemsFolder + "/" + ToPascal(itemId) + ".asset";

        internal static ShopItemData CreateOrLoad(string itemId)
        {
            CatalogBuilder.EnsureFolder(CatalogBuilder.ShopItemsFolder);
            var path = AssetPathFor(itemId);
            var item = AssetDatabase.LoadAssetAtPath<ShopItemData>(path);

            if (item != null)
            {
                return item;
            }

            item = CreateInstance<ShopItemData>();
            item.itemId = itemId;
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        internal static ShopItemData FindById(string itemId)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ShopItemData"))
            {
                var item = AssetDatabase.LoadAssetAtPath<ShopItemData>(AssetDatabase.GUIDToAssetPath(guid));

                if (item != null && item.itemId == itemId)
                {
                    return item;
                }
            }

            return null;
        }

        private static int NextDisplayOrder(ShopItemData self)
        {
            var max = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:ShopItemData", new[] { CatalogBuilder.ShopItemsFolder }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ShopItemData>(AssetDatabase.GUIDToAssetPath(guid));

                if (item != null && item != self && item.displayOrder > max)
                {
                    max = item.displayOrder;
                }
            }

            return max + 10;
        }

        /// <summary>Loads a PNG as a Sprite, switching its importer to Sprite if needed.</summary>
        internal static Sprite EnsureSprite(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
                (importer.textureType != TextureImporterType.Sprite ||
                 importer.spriteImportMode == SpriteImportMode.None))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static string ToPascal(string id)
        {
            var parts = id.Split('_', '-');
            var result = string.Empty;

            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    result += char.ToUpperInvariant(part[0]) + part.Substring(1);
                }
            }

            return result.Length > 0 ? result : id;
        }

        private static string Escape(string s) => (s ?? string.Empty).Trim().Replace("\\", "\\\\").Replace("\"", "\\\"");

        // ---- Starter items ----

        private readonly struct Starter
        {
            public readonly string Id;
            public readonly int Price;
            public readonly int Order;
            public readonly Vector2 Size;
            public readonly Vector2 Position;
            public readonly int Sorting;

            public Starter(string id, int price, int order, Vector2 size, Vector2 position, int sorting)
            {
                Id = id;
                Price = price;
                Order = order;
                Size = size;
                Position = position;
                Sorting = sorting;
            }
        }

        // Sizes/positions from Figma (412x917): lamp "brush 24" 94x180 @ (12,296);
        // plant "plantIcon" 138x182 @ (250,468) in PreviewMode; book pile
        // "BookPileIcon" 142x139 @ (231,328) in SelectedObjectToMove. Position =
        // item centre, normalised, y flipped (Unity's 0 is the bottom).
        // Grid order follows the Figma shop: Books, Lamp, Plant.
        private static readonly Starter[] Starters =
        {
            new Starter("books", 100, 10, new Vector2(142f, 139f), new Vector2(302f / 412f, 1f - 397.5f / 917f), 1),
            new Starter("lamp", 100, 20, new Vector2(94f, 180f), new Vector2(59f / 412f, 1f - 386f / 917f), 0),
            new Starter("plant", 50, 30, new Vector2(138f, 182f), new Vector2(319f / 412f, 1f - 559f / 917f), 2),
        };

        [MenuItem("Restorium/Shop/Create Starter Items", priority = 21)]
        public static void CreateStarterItems()
        {
            var missing = string.Empty;

            foreach (var s in Starters)
            {
                var item = CreateOrLoad(s.Id); // updates in place when it already exists
                var iconPath = $"{ArtRoot}/{s.Id}/icon.png";
                var placedPath = $"{ArtRoot}/{s.Id}/placed.png";

                item.itemId = s.Id;
                item.category = ShopCategory.Decor;
                item.price = s.Price;
                item.displayOrder = s.Order;
                item.placedSize = s.Size;
                item.defaultPosition = s.Position;
                item.sortingOrder = s.Sorting;
                item.nameKey = "shop.item." + s.Id + ".name";
                item.descriptionKey = "shop.item." + s.Id + ".desc";
                item.shopIcon = EnsureSprite(iconPath);
                item.placedSprite = EnsureSprite(placedPath);

                if (item.shopIcon == null)
                {
                    missing += "\n  " + iconPath;
                }

                if (item.placedSprite == null)
                {
                    missing += "\n  " + placedPath;
                }

                EditorUtility.SetDirty(item);
            }

            AssetDatabase.SaveAssets();
            CatalogBuilder.RebuildAll();

            if (missing.Length > 0)
            {
                Debug.LogWarning("[ShopItemWizard] Starter items created, but this art is missing (the items " +
                                 "will show blank until it exists; then run this menu again):" + missing);
            }
            else
            {
                Debug.Log("[ShopItemWizard] Starter items lamp, plant and books are ready in " +
                          CatalogBuilder.ShopItemsFolder + ".");
            }
        }
    }
}
