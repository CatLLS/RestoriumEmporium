// ============================================================
// RevenueCatSetupMenu — "Restorium/Store/Add RevenueCat to Systems prefab".
// WHAT & WHY: The RevenueCat SDK needs its "Purchases" component on a persistent
//   object, with "Use Runtime Setup" ticked so RevenueCatCoinStore (not the
//   component's own Inspector fields) supplies the key. The coin shop also needs
//   the catalog on GameBootstrap. Doing that by hand is five easy-to-miss clicks,
//   so this menu does it.
// KEY DECISIONS:
//   - Edits ONLY Assets/Prefabs/Systems.prefab (through LoadPrefabContents /
//     SaveAsPrefabAsset), never an open scene. It adds components that are
//     missing and sets exactly these fields: Purchases.useRuntimeSetup,
//     RevenueCatCoinStore.config and GameBootstrap.coinPackCatalog. Anything
//     else on the prefab is left untouched.
//   - Idempotent: running it again only re-asserts those three fields.
//   - Creates RevenueCatConfig.asset / CoinPackCatalog.asset with the Figma
//     defaults if they are missing (they ship in the repo, so normally it won't).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Run the menu once after pulling the coin-shop change.
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Economy;

    public static class RevenueCatSetupMenu
    {
        private const string SystemsPrefabPath = "Assets/Prefabs/Systems.prefab";
        public const string ConfigPath = "Assets/Data/Config/RevenueCatConfig.asset";
        public const string CatalogPath = "Assets/Data/Catalogs/CoinPackCatalog.asset";

        [MenuItem("Restorium/Store/Add RevenueCat to Systems prefab")]
        public static void AddToSystemsPrefab()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("RevenueCat setup", "Exit Play mode first.", "OK");
                return;
            }

            var config = LoadOrCreateConfig();
            var catalog = LoadOrCreateCatalog();
            var root = PrefabUtility.LoadPrefabContents(SystemsPrefabPath);

            if (root == null)
            {
                EditorUtility.DisplayDialog("RevenueCat setup", $"Could not open '{SystemsPrefabPath}'.", "OK");
                return;
            }

            var log = new List<string>();

            try
            {
                var purchases = GetOrAdd<Purchases>(root, log);
                var purchasesSo = new SerializedObject(purchases);
                purchasesSo.FindProperty("useRuntimeSetup").boolValue = true;
                purchasesSo.ApplyModifiedPropertiesWithoutUndo();

                var store = GetOrAdd<RevenueCatCoinStore>(root, log);
                var storeSo = new SerializedObject(store);
                storeSo.FindProperty("config").objectReferenceValue = config;
                storeSo.ApplyModifiedPropertiesWithoutUndo();

                var bootstrap = root.GetComponent<GameBootstrap>();

                if (bootstrap == null)
                {
                    log.Add("WARNING: no GameBootstrap on Systems; 'Coin Pack Catalog' not set.");
                }
                else
                {
                    var bootSo = new SerializedObject(bootstrap);
                    bootSo.FindProperty("coinPackCatalog").objectReferenceValue = catalog;
                    bootSo.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            log.Add("Use Runtime Setup = on, Config and Coin Pack Catalog assigned.");
            var summary = string.Join("\n", log);
            Debug.Log("[RevenueCatSetup] Systems prefab updated.\n" + summary);

            EditorUtility.DisplayDialog("RevenueCat setup — done",
                summary + "\n\nNext: paste the Test Store PUBLIC key (test_...) into\n" + ConfigPath +
                "\n(never an sk_ secret key). See Docs/RevenueCat.md.", "OK");

            Selection.activeObject = config;
        }

        private static T GetOrAdd<T>(GameObject go, List<string> log) where T : Component
        {
            var c = go.GetComponent<T>();

            if (c != null)
            {
                return c;
            }

            log.Add($"Added {typeof(T).Name}.");
            return go.AddComponent<T>();
        }

        private static RevenueCatConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<RevenueCatConfig>(ConfigPath);

            if (config != null)
            {
                return config;
            }

            EnsureFolder("Assets/Data/Config");
            config = ScriptableObject.CreateInstance<RevenueCatConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static CoinPackCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CoinPackCatalog>(CatalogPath);

            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolder("Assets/Data/Catalogs");
            catalog = ScriptableObject.CreateInstance<CoinPackCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);

            // Figma BuyCoins (623:26) packs.
            var so = new SerializedObject(catalog);
            var packs = so.FindProperty("packs");
            AddPack(packs, "coins_300", 300, "$ 0.99");
            AddPack(packs, "coins_900", 900, "$ 1.99");
            AddPack(packs, "coins_2000", 2000, "$ 3.00");
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void AddPack(SerializedProperty list, string id, int coins, string price)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            var e = list.GetArrayElementAtIndex(list.arraySize - 1);
            e.FindPropertyRelative("productId").stringValue = id;
            e.FindPropertyRelative("coins").intValue = coins;
            e.FindPropertyRelative("fallbackPriceLabel").stringValue = price;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
