// ============================================================
// CatalogBuilder — keeps PosterCatalog.asset and ShopCatalog.asset up to date.
// WHAT & WHY: Runtime code never searches the project for posters or shop items;
//   it reads two catalogue assets. This editor tool fills them: every PosterData
//   under Assets/Data/ (by journalOrder) and every ShopItemData under
//   Assets/Data/ShopItems/ (by displayOrder). So adding a poster or a shop item is
//   "create the asset" — nothing to register by hand, no code, no scene edits.
// KEY DECISIONS:
//   - One public entry point, RebuildAll(), also rebuilds the locale tables
//     (LocaleTableBuilder.RebuildAll) so one menu click refreshes every generated
//     asset. Other tools (PosterAuthoringTool, ShopItemWizard) call it at the end.
//   - CatalogPostprocessor re-runs ONLY the catalogue part whenever a PosterData /
//     ShopItemData is imported, moved or deleted. It is deferred with
//     EditorApplication.delayCall (never touch assets inside an import callback)
//     and debounced so importing 20 assets rebuilds once.
//   - The catalogue assets are only written when their list actually changed,
//     which also stops the postprocessor from re-triggering itself.
//   - Sorting is stable and deterministic (order, then id) so two machines
//     produce identical assets and diffs stay quiet.
//   - Duplicate ids are reported loudly: the id is the save-file identity.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach — this is an Editor script (it lives in Assets/Editor).
// [ ] Run once: menu Restorium -> Rebuild Catalogs & Locale Tables. It creates
//     Assets/Data/Catalogs/PosterCatalog.asset and ShopCatalog.asset if missing.
// [ ] After that it runs by itself whenever you add, move or delete a poster or
//     shop item asset. Run the menu again any time something looks out of date.
// ---------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Data;

    public static class CatalogBuilder
    {
        public const string CatalogFolder = "Assets/Data/Catalogs";
        public const string PosterCatalogPath = CatalogFolder + "/PosterCatalog.asset";
        public const string ShopCatalogPath = CatalogFolder + "/ShopCatalog.asset";
        public const string PosterSearchFolder = "Assets/Data";
        public const string ShopItemsFolder = "Assets/Data/ShopItems";

        [MenuItem("Restorium/Rebuild Catalogs & Locale Tables", priority = 0)]
        public static void RebuildAll()
        {
            RebuildCatalogs();
            LocaleTableBuilder.RebuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[CatalogBuilder] Catalogs and locale tables rebuilt.");
        }

        /// <summary>Only the two catalogues (no locale tables). Used by the postprocessor.</summary>
        public static void RebuildCatalogs()
        {
            EnsureFolder(CatalogFolder);
            EnsureFolder(ShopItemsFolder);

            var posterChanged = RebuildPosterCatalog();
            var shopChanged = RebuildShopCatalog();

            if (posterChanged || shopChanged)
            {
                AssetDatabase.SaveAssets();
            }
        }

        public static PosterCatalog LoadOrCreatePosterCatalog() => LoadOrCreate<PosterCatalog>(PosterCatalogPath);

        public static ShopCatalog LoadOrCreateShopCatalog() => LoadOrCreate<ShopCatalog>(ShopCatalogPath);

        private static bool RebuildPosterCatalog()
        {
            var found = FindAll<PosterData>(PosterSearchFolder);
            found.Sort((a, b) =>
            {
                var c = a.journalOrder.CompareTo(b.journalOrder);
                return c != 0 ? c : string.CompareOrdinal(a.posterId, b.posterId);
            });

            WarnDuplicates(found, p => p.posterId, "posterId");

            var catalog = LoadOrCreatePosterCatalog();

            if (SameList(catalog.posters, found))
            {
                return false;
            }

            catalog.posters = found;
            catalog.Invalidate();
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[CatalogBuilder] PosterCatalog: {found.Count} poster(s).");
            return true;
        }

        private static bool RebuildShopCatalog()
        {
            var found = FindAll<ShopItemData>(ShopItemsFolder);
            found.Sort((a, b) =>
            {
                var c = a.displayOrder.CompareTo(b.displayOrder);
                return c != 0 ? c : string.CompareOrdinal(a.itemId, b.itemId);
            });

            foreach (var item in found)
            {
                if (string.IsNullOrEmpty(item.itemId))
                {
                    Debug.LogWarning($"[CatalogBuilder] Shop item '{AssetDatabase.GetAssetPath(item)}' has no " +
                                     "Item Id. It will not appear correctly; fill Item Id (e.g. \"lamp\").", item);
                }
            }

            WarnDuplicates(found, i => i.itemId, "itemId");

            var catalog = LoadOrCreateShopCatalog();

            if (SameList(catalog.items, found))
            {
                return false;
            }

            catalog.items = found;
            catalog.Invalidate();
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[CatalogBuilder] ShopCatalog: {found.Count} item(s).");
            return true;
        }

        private static List<T> FindAll<T>(string folder) where T : ScriptableObject
        {
            var result = new List<T>();

            if (!AssetDatabase.IsValidFolder(folder))
            {
                return result;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

                if (asset != null && !result.Contains(asset))
                {
                    result.Add(asset);
                }
            }

            return result;
        }

        private static void WarnDuplicates<T>(List<T> list, System.Func<T, string> id, string label) where T : Object
        {
            var seen = new Dictionary<string, T>();

            foreach (var entry in list)
            {
                var key = id(entry) ?? string.Empty;

                if (key.Length == 0)
                {
                    continue;
                }

                if (seen.TryGetValue(key, out var first))
                {
                    Debug.LogError($"[CatalogBuilder] Duplicate {label} '{key}' in '{AssetDatabase.GetAssetPath(first)}' " +
                                   $"and '{AssetDatabase.GetAssetPath(entry)}'. Give one of them a new id.", entry);
                }
                else
                {
                    seen.Add(key, entry);
                }
            }
        }

        private static bool SameList<T>(List<T> a, List<T> b) where T : Object
        {
            if (a == null || a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            Debug.Log($"[CatalogBuilder] Created {path}.");
            return asset;
        }

        /// <summary>Creates every missing folder of an "Assets/..." path.</summary>
        public static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }

    /// <summary>
    /// Re-runs the catalogue rebuild when poster or shop-item assets are added,
    /// moved or deleted. Deferred and debounced; never runs during play mode.
    /// </summary>
    public class CatalogPostprocessor : AssetPostprocessor
    {
        private static bool _queued;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved,
            string[] movedFrom)
        {
            if (_queued)
            {
                return;
            }

            if (!Touches(imported, true) && !Touches(moved, true) && !Touches(deleted, false) &&
                !Touches(movedFrom, false))
            {
                return;
            }

            _queued = true;
            EditorApplication.delayCall += RunQueued;
        }

        private static void RunQueued()
        {
            _queued = false;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                // Try again once the editor settles.
                _queued = true;
                EditorApplication.delayCall += RunQueued;
                return;
            }

            CatalogBuilder.RebuildCatalogs();
        }

        private static bool Touches(string[] paths, bool exists)
        {
            if (paths == null)
            {
                return false;
            }

            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset") ||
                    !path.StartsWith(CatalogBuilder.PosterSearchFolder + "/") ||
                    path.StartsWith(CatalogBuilder.CatalogFolder + "/"))
                {
                    continue;
                }

                if (!exists)
                {
                    // Deleted / moved-away: we can't load it, so any data asset counts.
                    return true;
                }

                var type = AssetDatabase.GetMainAssetTypeAtPath(path);

                if (type != null && (typeof(PosterData).IsAssignableFrom(type) ||
                                     typeof(ShopItemData).IsAssignableFrom(type)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
