// ============================================================
// BuildStickerRemoval — creates/updates "StickerRemovalScreen".
// WHAT & WHY: Per RESTORATION.md §3A exactly (already complete instructions,
//   nothing left ambiguous there). Close-up + sticker root fill the whole
//   screen; Poster Stack Root and Tool Bar Root are left EMPTY on purpose so
//   RestorationPresenter hides the shared Poster object while this screen is up.
// KEY DECISIONS:
//   - PauseButton is the LAST child (drawn over everything) and gets its own
//     PauseButton component pointed at Overlays, Opens = Pause — this screen is
//     new, so (unlike Cleaning/LinenBacking) there is no pre-existing gap to fix.
//   - Stickers are NOT created here: StickerRemovalScreen builds/re-binds
//     StickerView objects under StickerRoot at runtime from the active stage's
//     StickerDefinition[]. This builder only makes the container.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — run Restorium/Scene/Build Batch 2 Scene Objects with the
//     Game scene open. See Docs/Batch2/handoff/SCENE_BUILDER.md.
// ---------------------------------------------------------------

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.EditorTools
{
    internal static class BuildStickerRemoval
    {
        public static RestoriumEmporium.UI.StickerRemovalScreen Build(Transform canvas,
            RestoriumEmporium.Restoration.RestorationController restorationController,
            RestoriumEmporium.UI.OverlayController overlays)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "StickerRemovalScreen");
            SceneBuilderCore.Stretch(root);

            var background = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Background", 0);
            SceneBuilderCore.Stretch(background);
            SceneBuilderCore.SetImage(background, "Assets/Art/LinnenAssets/LinnenBackingBG(all).png");

            var closeUpArea = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "CloseUpArea", 1);
            SceneBuilderCore.Stretch(closeUpArea);
            SceneBuilderCore.AddOrGet<RectMask2D>(closeUpArea);

            var closeUp = SceneBuilderCore.FindOrCreateChild(closeUpArea.transform, "CloseUp");
            SceneBuilderCore.CenterAnchor(closeUp);
            var closeUpImage = SceneBuilderCore.SetImage(closeUp, "Assets/Art/Posters/poster2/close-upForStickerRemoval.png");

            var stickerRoot = SceneBuilderCore.FindOrCreateChild(closeUpArea.transform, "StickerRoot");
            SceneBuilderCore.CenterAnchor(stickerRoot);
            // Must stay BELOW CloseUp in the hierarchy so stickers draw on top.
            stickerRoot.transform.SetSiblingIndex(closeUp.transform.GetSiblingIndex() + 1);

            var marker = SceneBuilderCore.FindOrCreateChild(stickerRoot.transform, "NextStickerMarker");
            SceneBuilderCore.CenterAnchor(marker);
            SceneBuilderCore.SetupAnchor(marker, "sticker.next");

            var header = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Header", 2);
            SceneBuilderCore.FigmaRect(header, 114f, 90f, 184f, 30f);
            SceneBuilderCore.SetupText(header, SceneBuilderCore.FontChoice.SpecialElite, 18f, Color.white,
                TextAlignmentOptions.Center, null, "Peel them off!");

            var pauseButton = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "PauseButton", 3);
            SceneBuilderCore.FigmaRect(pauseButton, 14f, 38f, 53f, 57f);
            SceneBuilderCore.SetImage(pauseButton, "Assets/Art/UI/hamburgerIcon.png");
            SceneBuilderCore.SetupButton(pauseButton, addSfx: true);
            SceneBuilderCore.SetupAnchor(pauseButton, "pause.button");
            var pause = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.PauseButton>(pauseButton);
            var pauseSo = new SerializedObject(pause);
            SceneBuilderCore.SetField(pauseSo, "overlays", overlays, "StickerRemovalScreen/PauseButton");
            SceneBuilderCore.SetFieldEnum(pauseSo, "opens", RestoriumEmporium.UI.PauseButton.Target.Pause,
                "StickerRemovalScreen/PauseButton");
            pauseSo.ApplyModifiedProperties();

            var screen = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.StickerRemovalScreen>(root);
            var so = new SerializedObject(screen);
            SceneBuilderCore.SetField(so, "restorationSource", restorationController, "StickerRemovalScreen");
            // Left EMPTY on purpose: no field write for posterStackRoot/toolBarRoot.
            SceneBuilderCore.SetField(so, "headerLabel", header.GetComponent<TMP_Text>(), "StickerRemovalScreen");
            SceneBuilderCore.SetField(so, "closeUpArea", SceneBuilderCore.Rect(closeUpArea), "StickerRemovalScreen");
            SceneBuilderCore.SetField(so, "closeUpImage", closeUpImage, "StickerRemovalScreen");
            SceneBuilderCore.SetField(so, "stickerRoot", SceneBuilderCore.Rect(stickerRoot), "StickerRemovalScreen");
            SceneBuilderCore.SetField(so, "nextStickerMarker", SceneBuilderCore.Rect(marker), "StickerRemovalScreen");
            SceneBuilderCore.SetFieldEnum(so, "fitMode", RestoriumEmporium.Restoration.StickerFitMode.Cover,
                "StickerRemovalScreen");
            SceneBuilderCore.SetField(so, "completeBeatSeconds", 0.35f, "StickerRemovalScreen");

            var previewStagePath = "Assets/Data/Poster2/Stages/02_Stickers.asset";
            var previewStage = AssetDatabase.LoadAssetAtPath<RestoriumEmporium.Data.RestorationStageData>(
                previewStagePath);

            if (previewStage != null)
            {
                SceneBuilderCore.SetField(so, "previewStage", previewStage, "StickerRemovalScreen");
            }
            else
            {
                SceneBuilderCore.Problem($"'{previewStagePath}' not found — run Restorium/Posters/Create or " +
                                         "Update Poster 2 Data first, then re-run the scene builder to wire " +
                                         "StickerRemovalScreen's editor-only gizmo preview (optional; nothing " +
                                         "breaks at runtime without it).");
            }

            so.ApplyModifiedProperties();

            // Router enables this screen; it must start disabled like every other one.
            SceneBuilderCore.SetActive(root, false);
            return screen;
        }
    }
}
