// ============================================================
// ArtImportSettingsTool — one click that puts every art file on the right import settings.
// WHAT & WHY: Three import mistakes break or bloat this game and are exactly the
//   ones a Unity beginner makes: a poster png left as "Default" texture (it cannot
//   be dragged into an Image), a poster sprite with Mesh Type "Tight" (the reveal
//   shader's UVs no longer match and scrubbing appears not to work), and 4K
//   textures shipped uncompressed to phones. This menu fixes all of them, plus the
//   cutscene videos, in one pass:
//     Restorium -> Fix Art Import Settings (Sprites)
// KEY DECISIONS:
//   - Rules by folder, not per file: Art/Posters/** and the linen poster art are
//     "poster" sprites (Full Rect, Clamp, ASTC 6x6, max 2048 on Android — the
//     2304 x 4096 sticker close-up is therefore capped at 2048 on phones, which
//     is still sharper than any phone shows it); files named *BG* / bg* are
//     full-screen backgrounds (ASTC 8x8, max 2048); everything else under
//     Assets/Art (UI, Shop, ShopItems, Settings, DeskHub, ...) is UI (ASTC 6x6,
//     max 2048, mesh type left alone — Tight is fine for UI).
//   - Only files whose settings actually differ are re-imported, inside one
//     Start/StopAssetEditing batch, so a second run is instant and quiet.
//   - Sprite Mode is only set to Single when the file was not a sprite at all. A
//     texture already imported as Multiple (e.g. the journal thumbnail and
//     linnenBacking) keeps its mode, because switching it changes the sprite's id
//     and silently breaks every scene and asset reference to it.
//   - A Multiple texture re-exported at a new size gets its lone sprite rect
//     resized to the image (id kept); sprite sheets with out-of-bounds rects are
//     reported, not guessed.
//   - Art/Particles is skipped: those are particle textures, not UI sprites.
//   - Mipmaps off everywhere: UI is never seen at an angle or a distance, and
//     mips add a third to the texture memory.
//   - Videos (Assets/videos): transcoded to H.264 (every Android device decodes
//     it in hardware), medium bitrate, and anything wider than 1080 px is scaled
//     to 1080 wide on Android — the videos are full-screen on a phone and the
//     extra pixels only cost download size and decode time.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach. Editor-only.
// [ ] Run "Restorium" -> "Fix Art Import Settings (Sprites)" after importing or
//     re-exporting any art, and once before building an APK. Read the Console
//     line: it says how many files were changed.
// [ ] Videos only: "Restorium" -> "Fix Video Import Settings" (also run by the
//     menu above). Transcoding the three cutscenes can take a minute.
// [ ] Spot-check one poster png afterwards: Texture Type = Sprite (2D and UI),
//     Mesh Type = Full Rect, and on the Android tab "Override" ticked with
//     ASTC 6x6 / Max Size 2048.
// ---------------------------------------------------------------

using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    public static class ArtImportSettingsTool
    {
        private const string ArtRoot = "Assets/Art";
        private const string PostersRoot = "Assets/Art/Posters/";
        private const string LinenRoot = "Assets/Art/LinnenAssets/";
        private const string ParticlesRoot = "Assets/Art/Particles/";
        private const string VideosRoot = "Assets/videos";
        private const string Android = "Android";
        private const int AndroidMaxVideoWidth = 1080;

        private enum ArtGroup
        {
            Poster,
            Background,
            Ui
        }

        [MenuItem("Restorium/Fix Art Import Settings (Sprites)", false, 200)]
        public static void FixArtImportSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
            int changed = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (path.StartsWith(ParticlesRoot))
                    {
                        continue;
                    }

                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    {
                        continue;
                    }

                    if (Apply(importer, Classify(path)))
                    {
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int videos = FixVideos();

            AssetDatabase.Refresh();
            Debug.Log(
                $"[ArtImportSettingsTool] Checked {guids.Length} texture(s) under {ArtRoot}: re-imported {changed}. " +
                $"Videos updated: {videos}. Posters = Full Rect + ASTC 6x6 (2048), backgrounds = ASTC 8x8 (2048), " +
                "UI = ASTC 6x6 (2048), all without mipmaps.");
        }

        [MenuItem("Restorium/Fix Video Import Settings", false, 201)]
        public static void FixVideoImportSettingsMenu()
        {
            int videos = FixVideos();
            Debug.Log($"[ArtImportSettingsTool] Video import settings updated on {videos} clip(s) in {VideosRoot}.");
        }

        private static ArtGroup Classify(string path)
        {
            string file = Path.GetFileNameWithoutExtension(path);

            if (path.StartsWith(PostersRoot))
            {
                return ArtGroup.Poster;
            }

            if (path.StartsWith(LinenRoot) && (file.StartsWith("PosterBack") || file == "linnenBacking"))
            {
                return ArtGroup.Poster;
            }

            if (file.Contains("BG") || file.StartsWith("bg"))
            {
                return ArtGroup.Background;
            }

            return ArtGroup.Ui;
        }

        /// <summary>
        /// A re-export at a different size leaves a Multiple sprite's rect pointing past
        /// the image, and Unity silently drops that sprite along with every reference to
        /// it. A lone sprite is resized to the full image through the sprite data
        /// provider, which keeps its id so references survive. Real sprite sheets are
        /// only reported: guessing their new rects would be worse than asking.
        /// </summary>
        private static bool FitSpriteRectsToImage(TextureImporter importer)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);

            if (provider == null)
            {
                return false;
            }

            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var bounds = new Rect(0f, 0f, width, height);

            var outOfBounds = 0;

            foreach (var sprite in rects)
            {
                var r = sprite.rect;

                if (r.xMin < 0f || r.yMin < 0f || r.xMax > bounds.xMax || r.yMax > bounds.yMax)
                {
                    outOfBounds++;
                }
            }

            if (outOfBounds == 0)
            {
                return false;
            }

            if (rects.Length != 1)
            {
                Debug.LogWarning($"[ArtImportSettingsTool] '{importer.assetPath}' has {outOfBounds} sprite(s) " +
                                 $"outside its {width}x{height} image. Fix them in the Sprite Editor.", importer);
                return false;
            }

            rects[0].rect = bounds;
            provider.SetSpriteRects(rects);
            provider.Apply();
            Debug.Log($"[ArtImportSettingsTool] '{importer.assetPath}': sprite rect resized to the new " +
                      $"{width}x{height} image (sprite id kept, references intact).", importer);
            return true;
        }

        /// <summary>Applies the group's rules. Returns true when anything changed.</summary>
        private static bool Apply(TextureImporter importer, ArtGroup group)
        {
            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            else if (importer.spriteImportMode == SpriteImportMode.None)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }
            else if (importer.spriteImportMode == SpriteImportMode.Multiple && FitSpriteRectsToImage(importer))
            {
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }

            if (group == ArtGroup.Poster)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                // Full Rect is REQUIRED for anything the reveal shader draws: a tight
                // mesh remaps the UVs the mask is painted in.
                if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.spriteGenerateFallbackPhysicsShape)
                {
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings);
                    dirty = true;
                }

                if (importer.wrapMode != TextureWrapMode.Clamp)
                {
                    importer.wrapMode = TextureWrapMode.Clamp;
                    dirty = true;
                }
            }

            TextureImporterFormat format = group == ArtGroup.Background
                ? TextureImporterFormat.ASTC_8x8
                : TextureImporterFormat.ASTC_6x6;

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings(Android);
            if (!android.overridden || android.maxTextureSize != 2048 || android.format != format)
            {
                android.name = Android;
                android.overridden = true;
                android.maxTextureSize = 2048;
                android.format = format;
                android.compressionQuality = 50;
                importer.SetPlatformTextureSettings(android);
                dirty = true;
            }

            return dirty;
        }

        /// <summary>Transcodes every VideoClip under Assets/videos for mobile. Returns how many changed.</summary>
        private static int FixVideos()
        {
            if (!AssetDatabase.IsValidFolder(VideosRoot))
            {
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:VideoClip", new[] { VideosRoot });
            int changed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!(AssetImporter.GetAtPath(path) is VideoClipImporter importer))
                {
                    continue;
                }

                bool dirty = false;

                VideoImporterTargetSettings all = importer.defaultTargetSettings;
                if (!all.enableTranscoding || all.codec != VideoCodec.H264 || all.bitrateMode != VideoBitrateMode.Medium)
                {
                    all.enableTranscoding = true;
                    all.codec = VideoCodec.H264;
                    all.bitrateMode = VideoBitrateMode.Medium;
                    all.spatialQuality = VideoSpatialQuality.MediumSpatialQuality;
                    all.resizeMode = VideoResizeMode.OriginalSize;
                    importer.defaultTargetSettings = all;
                    dirty = true;
                }

                // Android: same, but never wider than 1080 px.
                VideoImporterTargetSettings android = importer.GetTargetSettings(Android) ?? new VideoImporterTargetSettings();
                int sourceWidth = importer.GetResizeWidth(VideoResizeMode.OriginalSize);
                int sourceHeight = importer.GetResizeHeight(VideoResizeMode.OriginalSize);
                bool downscale = sourceWidth > AndroidMaxVideoWidth && sourceHeight > 0;
                int width = downscale ? AndroidMaxVideoWidth : sourceWidth;
                int height = downscale ? EvenRound(sourceHeight * (AndroidMaxVideoWidth / (float)sourceWidth)) : sourceHeight;
                VideoResizeMode mode = downscale ? VideoResizeMode.CustomSize : VideoResizeMode.OriginalSize;

                if (!android.enableTranscoding || android.codec != VideoCodec.H264 ||
                    android.bitrateMode != VideoBitrateMode.Medium || android.resizeMode != mode ||
                    (downscale && (android.customWidth != width || android.customHeight != height)))
                {
                    android.enableTranscoding = true;
                    android.codec = VideoCodec.H264;
                    android.bitrateMode = VideoBitrateMode.Medium;
                    android.spatialQuality = VideoSpatialQuality.MediumSpatialQuality;
                    android.resizeMode = mode;
                    if (downscale)
                    {
                        android.customWidth = width;
                        android.customHeight = height;
                    }

                    importer.SetTargetSettings(Android, android);
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            return changed;
        }

        private static int EvenRound(float value)
        {
            int v = Mathf.RoundToInt(value);
            return (v & 1) == 0 ? v : v + 1;
        }
    }
}
