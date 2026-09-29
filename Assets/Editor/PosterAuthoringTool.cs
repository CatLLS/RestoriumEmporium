// ============================================================
// PosterAuthoringTool — one menu click per poster that authors its data assets.
// WHAT & WHY: A poster is about a dozen ScriptableObjects (the six ToolData
//   assets it shares with every poster, one RestorationStageData per stage and one
//   PosterData) with dozens of sprite references between them. Hand-creating that
//   through Create menus is miserable and error-prone for someone new to Unity.
//   This tool builds or updates all of it from the recipes in
//   Editor/Posters/PosterRecipes.cs:
//     Restorium -> Posters -> Create or Update Poster 1 Data
//     Restorium -> Posters -> Create or Update Poster 2 Data
//   and then refreshes the poster/shop catalogues and the locale tables through
//   CatalogBuilder.RebuildAll() (SHOP agent's tool).
// KEY DECISIONS:
//   - Recipes, not per-poster code. Both menus run the same ApplyRecipe(); a new
//     poster is a new recipe block plus one menu line.
//   - Idempotent: every asset is loaded first and only created when absent, then
//     its fields are overwritten from the recipe. Re-running never produces
//     "Poster01 1.asset" and keeps every GUID, so scene references survive.
//   - A sprite that cannot be found NEVER blanks a reference that is already
//     filled in. It is reported with its exact expected path (and whether the png
//     is missing or merely not imported as a Sprite), and the old value is kept.
//     A half-exported art folder must not undo the human's hand wiring.
//   - Sticker POSITIONS are only written when the stage has none yet (or the
//     number of stickers changed). They are meant to be tweaked by hand in the
//     stage's Inspector preview; a re-run must not snap them back. Use
//     "Reset Poster 2 Sticker Positions" to go back to the Figma values.
//   - Batch 2: this tool NO LONGER creates locale tables or tutorial steps. Those
//     moved to LocaleTableBuilder / TutorialAuthoringTool (UI agent). Strings for
//     the stages it authors live in Editor/Localization/LocaleSource.*.cs.
//   - Folders are created before any asset, because CreateAsset into a missing
//     folder fails quietly. No Start/StopAssetEditing batching: with ~15 assets
//     it buys nothing and makes freshly created assets unloadable mid-run.
//   - The import-settings fixer lives in Editor/Posters/ArtImportSettingsTool.cs
//     (same menu path as before: Restorium -> Fix Art Import Settings (Sprites)).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach. This is an Editor-only tool.
// [ ] FIRST run "Restorium" -> "Fix Art Import Settings (Sprites)" once, so every
//     poster png is a Sprite with Mesh Type = Full Rect.
// [ ] Then, in the Unity menu bar: "Restorium" -> "Posters" ->
//     "Create or Update Poster 1 Data", then "... Poster 2 Data"
//     (or "Create or Update All Posters" to do both).
// [ ] Read the Console summary. Missing sprites are listed with the exact file
//     path the tool expected; export/fix them and simply run the menu again.
// [ ] Check Assets/Data/: Tools/, Poster1/ (+Stages/), Poster2/ (+Stages/) and
//     Catalogs/PosterCatalog.asset listing Poster01 then Poster02.
// [ ] Sticker positions: select Assets/Data/Poster2/Stages/02_Stickers. The
//     Inspector shows the close-up with the stickers on it; drag a sticker to
//     move it. See Editor/Posters/StickerStageInspector.cs.
// [ ] The peel sound: this menu also adds Id = StickerPeel to
//     Assets/Audio/Data/SfxLibrary if it is missing (or run "Restorium ->
//     Posters -> Add Sticker Peel Sound To SfxLibrary" on its own).
// ---------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Audio;
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;

    public static class PosterAuthoringTool
    {
        private const string DataRoot = "Assets/Data";
        private const string ToolsFolder = DataRoot + "/Tools";
        private const string CleaningArt = "Assets/Art/cleaningAssets";
        private const string SfxLibraryPath = "Assets/Audio/Data/SfxLibrary.asset";
        private const string StickerPeelClipPath = "Assets/Audio/(stickerPeel)freesound_community-egg-crack4-85848.mp3";

        private static readonly List<string> MissingReport = new List<string>();

        // =====================================================================
        //  MENU ENTRY POINTS
        // =====================================================================

        [MenuItem("Restorium/Posters/Create or Update Poster 1 Data", false, 100)]
        public static void CreatePoster1Data()
        {
            Run(PosterRecipes.Poster1);
        }

        [MenuItem("Restorium/Posters/Create or Update Poster 2 Data", false, 101)]
        public static void CreatePoster2Data()
        {
            Run(PosterRecipes.Poster2);
        }

        [MenuItem("Restorium/Posters/Create or Update All Posters", false, 102)]
        public static void CreateAllPosters()
        {
            Run(PosterRecipes.Poster1, PosterRecipes.Poster2);
        }

        [MenuItem("Restorium/Posters/Reset Poster 2 Sticker Positions (Figma defaults)", false, 120)]
        public static void ResetPoster2StickerPositions()
        {
            PosterRecipe recipe = PosterRecipes.Poster2;
            int reset = 0;

            for (int i = 0; i < recipe.stages.Length; i++)
            {
                StageRecipe stage = recipe.stages[i];
                if (stage.stickers == null)
                {
                    continue;
                }

                string path = $"{recipe.StagesFolder}/{stage.assetName}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<RestorationStageData>(path);
                if (asset == null)
                {
                    Debug.LogWarning($"[PosterAuthoringTool] {path} does not exist yet. Run " +
                                     "Restorium -> Posters -> Create or Update Poster 2 Data first.");
                    continue;
                }

                Undo.RecordObject(asset, "Reset sticker positions");
                MissingReport.Clear();
                WriteStickers(asset, stage.stickers, true);
                EditorUtility.SetDirty(asset);
                reset++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PosterAuthoringTool] Reset sticker positions on {reset} stage(s) to the Figma values.");
        }

        [MenuItem("Restorium/Posters/Add Sticker Peel Sound To SfxLibrary", false, 121)]
        public static void AddStickerPeelSoundMenu()
        {
            MissingReport.Clear();
            EnsureStickerPeelSfx(true);
            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        //  RUN
        // =====================================================================

        private static void Run(params PosterRecipe[] recipes)
        {
            MissingReport.Clear();

            EnsureFolder(DataRoot);
            EnsureFolder(ToolsFolder);
            for (int i = 0; i < recipes.Length; i++)
            {
                EnsureFolder(recipes[i].folder);
                EnsureFolder(recipes[i].StagesFolder);
            }

            CreateTools();

            bool anyStickers = false;
            for (int i = 0; i < recipes.Length; i++)
            {
                ApplyRecipe(recipes[i]);
                anyStickers |= HasStickerStage(recipes[i]);
            }

            if (anyStickers)
            {
                EnsureStickerPeelSfx(false);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Posters appear in the journal through the catalogue; rebuild it (and
            // the locale tables) so a freshly created poster shows up straight away.
            CatalogBuilder.RebuildAll();

            string names = string.Join(", ", System.Array.ConvertAll(recipes, r => r.displayName));

            if (MissingReport.Count == 0)
            {
                Debug.Log($"[PosterAuthoringTool] Done: {names}. Every asset was created or updated, " +
                          "every reference filled in, and the catalogues / locale tables rebuilt.");
                return;
            }

            Debug.LogWarning(
                $"[PosterAuthoringTool] Done: {names}, but {MissingReport.Count} reference(s) could not be " +
                "filled. Existing values were KEPT for those slots; everything else is saved.\n" +
                "Missing:\n  " + string.Join("\n  ", MissingReport) + "\n" +
                "Fix the files above and run the same menu again; it updates the assets in place.");
        }

        private static bool HasStickerStage(PosterRecipe recipe)
        {
            for (int i = 0; i < recipe.stages.Length; i++)
            {
                if (recipe.stages[i].kind == StageKind.StickerPeel)
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================================
        //  TOOLS (shared by every poster)
        // =====================================================================

        private struct ToolDef
        {
            public string assetName;
            public ToolId id;
            public string nameKey;
            public string iconPath;
            public SfxId loopSfx;
            public float brushRadiusUv;
        }

        private static void CreateTools()
        {
            var defs = new[]
            {
                new ToolDef { assetName = "01_DustRemover", id = ToolId.DustRemover, nameKey = "tool.dustRemover",
                    iconPath = CleaningArt + "/dustRemover.png", loopSfx = SfxId.ToolDustRemover, brushRadiusUv = 0.12f },
                new ToolDef { assetName = "02_WaterSpray", id = ToolId.WaterSpray, nameKey = "tool.waterSpray",
                    iconPath = CleaningArt + "/waterSpray.png", loopSfx = SfxId.ToolWaterSpray, brushRadiusUv = 0.14f },
                new ToolDef { assetName = "03_Deacidifier", id = ToolId.Deacidifier, nameKey = "tool.deacidifier",
                    iconPath = CleaningArt + "/deacidifier.png", loopSfx = SfxId.ToolDeacidifier, brushRadiusUv = 0.14f },
                new ToolDef { assetName = "04_Squeegee", id = ToolId.Squeegee, nameKey = "tool.squeegee",
                    iconPath = PosterRecipes.LinenArt + "/squeegee.png", loopSfx = SfxId.ToolSqueegee, brushRadiusUv = 0.16f },
                new ToolDef { assetName = "05_Roller", id = ToolId.Roller, nameKey = "tool.roller",
                    iconPath = PosterRecipes.LinenArt + "/roller.png", loopSfx = SfxId.ToolRoller, brushRadiusUv = 0.18f },
                new ToolDef { assetName = "06_Pencil", id = ToolId.Pencil, nameKey = "tool.pencil",
                    iconPath = PosterRecipes.LinenArt + "/pencil.png", loopSfx = SfxId.ToolPencil, brushRadiusUv = 0.06f }
            };

            for (int i = 0; i < defs.Length; i++)
            {
                ToolDef def = defs[i];
                var asset = LoadOrCreate<ToolData>($"{ToolsFolder}/{def.assetName}.asset");

                asset.id = def.id;
                asset.nameKey = def.nameKey;
                asset.icon = SpriteOrKeep(asset.icon, def.iconPath, $"{def.assetName} icon");
                asset.loopSfx = def.loopSfx;
                asset.brushRadiusUv = def.brushRadiusUv;
                // fxPrefab is wired by hand (ToolFxController setup) and never touched here.

                EditorUtility.SetDirty(asset);
            }
        }

        // =====================================================================
        //  POSTER + STAGES
        // =====================================================================

        private static PosterData ApplyRecipe(PosterRecipe recipe)
        {
            var stages = new RestorationStageData[recipe.stages.Length];
            for (int i = 0; i < recipe.stages.Length; i++)
            {
                stages[i] = ApplyStage(recipe, recipe.stages[i]);
            }

            var poster = LoadOrCreate<PosterData>(recipe.AssetPath);

            poster.posterId = recipe.posterId;
            poster.journalOrder = recipe.journalOrder;
            poster.titleKey = recipe.titleKey;
            poster.chapterKey = recipe.chapterKey ?? string.Empty;
            poster.coinReward = recipe.coinReward;

            poster.journalThumbnail = string.IsNullOrEmpty(recipe.journalThumbnailPath)
                ? poster.journalThumbnail
                : SpriteOrKeep(poster.journalThumbnail, recipe.journalThumbnailPath, $"{recipe.assetName} Journal Thumbnail");
            poster.beforeSprite = SpriteOrKeep(poster.beforeSprite, recipe.beforePath, $"{recipe.assetName} Before Sprite");
            poster.finalSprite = SpriteOrKeep(poster.finalSprite, recipe.finalPath, $"{recipe.assetName} Final Sprite");
            poster.backSprite = SpriteOrKeep(poster.backSprite, recipe.backPath, $"{recipe.assetName} Back Sprite");
            poster.linenBackingSprite = SpriteOrKeep(poster.linenBackingSprite, recipe.linenPath,
                $"{recipe.assetName} Linen Backing Sprite");

            if (string.IsNullOrEmpty(recipe.completionCutscenePath))
            {
                poster.completionCutscene = null;
            }
            else
            {
                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(recipe.completionCutscenePath);
                if (clip != null)
                {
                    poster.completionCutscene = clip;
                }
                else
                {
                    MissingReport.Add($"{recipe.assetName} Completion Cutscene -> \"{recipe.completionCutscenePath}\" " +
                                      "was not found as a VideoClip (is the .mp4 in the project and imported?).");
                }
            }

            poster.stages = stages;
            EditorUtility.SetDirty(poster);
            return poster;
        }

        private static RestorationStageData ApplyStage(PosterRecipe poster, StageRecipe def)
        {
            var asset = LoadOrCreate<RestorationStageData>($"{poster.StagesFolder}/{def.assetName}.asset");
            string label = $"{poster.assetName} stage '{def.stageId}'";

            asset.stageId = def.stageId;
            asset.screen = def.screen;
            asset.requiredTool = def.tool;
            asset.kind = def.kind;
            asset.fromSprite = SpriteOrKeep(asset.fromSprite, def.fromPath, $"{label} From Sprite");
            asset.toSprite = SpriteOrKeep(asset.toSprite, def.toPath, $"{label} To Sprite");
            asset.revealTint = def.tint;
            asset.invertMask = def.invert;
            asset.requiredCoverage = def.coverage;
            asset.brushRadiusOverride = 0f;
            asset.onComplete = def.onComplete;
            asset.titleKey = def.titleKey;
            asset.tutorialKey = def.tutorialKey ?? string.Empty;
            // The last stage of a poster with a completion video stays silent: its
            // chime would land on the video's first frames.
            bool leadsIntoVideo = def.onComplete == StageTransition.GoToFinishedRepair
                                  && !string.IsNullOrEmpty(poster.completionCutscenePath);
            asset.completeSfx = leadsIntoVideo ? SfxId.None : SfxId.StageComplete;

            if (def.kind == StageKind.StickerPeel)
            {
                asset.closeUpSprite = SpriteOrKeep(asset.closeUpSprite, def.closeUpPath, $"{label} Close Up Sprite");
                WriteStickers(asset, def.stickers, false);
            }
            else
            {
                asset.closeUpSprite = null;
                asset.stickers = new StickerDefinition[0];
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Fills the stage's stickers. Positions/sizes/rotation are only written when
        /// <paramref name="forcePositions"/> is set or the sticker count changed, so
        /// hand tweaks survive a normal re-run. Sprites are always refreshed.
        /// </summary>
        private static void WriteStickers(RestorationStageData asset, StickerRecipe[] recipes, bool forcePositions)
        {
            recipes = recipes ?? new StickerRecipe[0];

            bool rebuild = forcePositions || asset.stickers == null || asset.stickers.Length != recipes.Length;
            if (rebuild && !forcePositions && asset.stickers != null && asset.stickers.Length > 0)
            {
                Debug.LogWarning($"[PosterAuthoringTool] '{asset.name}' had {asset.stickers.Length} sticker(s), the " +
                                 $"recipe has {recipes.Length}; positions were reset to the recipe values.");
            }

            if (rebuild)
            {
                var fresh = new StickerDefinition[recipes.Length];
                for (int i = 0; i < recipes.Length; i++)
                {
                    StickerRecipe r = recipes[i];
                    fresh[i] = new StickerDefinition
                    {
                        sprite = asset.stickers != null && i < asset.stickers.Length && asset.stickers[i] != null
                            ? asset.stickers[i].sprite
                            : null,
                        normalizedCenter = new Vector2(r.centerX, r.centerY),
                        normalizedSize = new Vector2(r.width, r.height),
                        rotation = r.rotation
                    };
                }

                asset.stickers = fresh;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                StickerDefinition sticker = asset.stickers[i] ?? (asset.stickers[i] = new StickerDefinition());
                sticker.sprite = SpriteOrKeep(sticker.sprite, recipes[i].spritePath, $"'{asset.name}' sticker {i + 1}");
            }
        }

        // =====================================================================
        //  SFX LIBRARY ROW FOR THE PEEL
        // =====================================================================

        /// <summary>
        /// Makes sure SfxLibrary maps SfxId.StickerPeel to the peel clip. Only adds or
        /// fills an EMPTY row; a row the human already assigned a clip to is left alone.
        /// </summary>
        private static void EnsureStickerPeelSfx(bool verbose)
        {
            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(SfxLibraryPath);
            if (library == null)
            {
                MissingReport.Add($"SfxLibrary -> \"{SfxLibraryPath}\" not found, so SfxId.StickerPeel could not be " +
                                  "mapped. Create it (see Audio/SfxLibrary.cs) and run Restorium -> Posters -> " +
                                  "Add Sticker Peel Sound To SfxLibrary.");
                return;
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(StickerPeelClipPath);
            if (clip == null)
            {
                MissingReport.Add($"Sticker peel sound -> \"{StickerPeelClipPath}\" was not found as an AudioClip.");
                return;
            }

            if (library.entries == null)
            {
                library.entries = new List<SfxLibrary.Entry>();
            }

            for (int i = 0; i < library.entries.Count; i++)
            {
                SfxLibrary.Entry entry = library.entries[i];
                if (entry.id != SfxId.StickerPeel)
                {
                    continue;
                }

                if (entry.clip != null)
                {
                    if (verbose)
                    {
                        Debug.Log($"[PosterAuthoringTool] SfxLibrary already maps StickerPeel to '{entry.clip.name}'. " +
                                  "Left unchanged.");
                    }

                    return;
                }

                entry.clip = clip;
                library.entries[i] = entry;
                Commit(library, verbose);
                return;
            }

            library.entries.Add(new SfxLibrary.Entry
            {
                id = SfxId.StickerPeel,
                clip = clip,
                volume = 1f,
                pitchMin = 0.95f,
                pitchMax = 1.05f
            });
            Commit(library, verbose);
        }

        private static void Commit(SfxLibrary library, bool verbose)
        {
            library.Invalidate();
            EditorUtility.SetDirty(library);

            if (verbose)
            {
                Debug.Log("[PosterAuthoringTool] SfxLibrary: StickerPeel -> (stickerPeel)freesound_community-egg-crack4.");
            }
        }

        // =====================================================================
        //  SHARED HELPERS
        // =====================================================================

        private static T LoadOrCreate<T>(string assetPath) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset != null)
            {
                return asset; // update in place: this is what makes re-running safe
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        /// <summary>
        /// Loads the sprite at <paramref name="assetPath"/>. When it cannot be found,
        /// reports why and returns <paramref name="current"/> so an existing reference
        /// is never blanked.
        /// </summary>
        private static Sprite SpriteOrKeep(Sprite current, string assetPath, string usedFor)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return current;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                return sprite;
            }

            string kept = current != null ? $" (kept the current '{current.name}')" : string.Empty;

            if (File.Exists(assetPath))
            {
                MissingReport.Add(
                    $"{usedFor} -> \"{assetPath}\" exists on disk but is NOT imported as a Sprite{kept}. " +
                    "Run Restorium -> Fix Art Import Settings (Sprites), or set Texture Type = " +
                    "\"Sprite (2D and UI)\" and Mesh Type = \"Full Rect\" by hand, then re-run.");
            }
            else
            {
                MissingReport.Add(
                    $"{usedFor} -> \"{assetPath}\" is not on disk{kept}. Export it to exactly that path " +
                    "(the file name is case-sensitive) and re-run this menu item.");
            }

            return current;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || folderPath == "Assets" ||
                AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folderPath);
            if (string.IsNullOrEmpty(parent))
            {
                return;
            }

            parent = parent.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
        }
    }
}
