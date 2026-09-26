// ============================================================
// BuildCutscenePlayer — creates/updates the "CutscenePlayer" full-screen video overlay.
// WHAT & WHY: One object, always active, that CutscenePlayer.cs (Cinematics)
//   uses to play tracysc1 / tracysc2 / openBookTransition. Built per that
//   script's own checklist and CORE.md §3: a nested Canvas at sort order 1000
//   (topmost, above everything including the pause/settings overlays), a
//   CanvasGroup that starts fully transparent and non-blocking, a VideoPlayer +
//   AudioSource routed to MainMixer/Music, and three children (Backdrop,
//   VideoSurface, SkipHint).
// KEY DECISIONS:
//   - Must be the LAST child of the Canvas so it draws over every screen and
//     every overlay; BuildCutscenePlayer is deliberately the LAST Build* call in
//     SceneBuilderMenu's orchestration order for this reason (see that file).
//   - No clip is assigned here: GameFlowController owns introCutscene/
//     bookOpenCutscene, and each PosterData owns its own completionCutscene
//     (set by the poster recipe tool). See BuildGameFlow.cs.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — run Restorium/Scene/Build Batch 2 Scene Objects with the
//     Game scene open. See Docs/Batch2/handoff/SCENE_BUILDER.md.
// ---------------------------------------------------------------

using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RestoriumEmporium.EditorTools
{
    internal static class BuildCutscenePlayer
    {
        public static RestoriumEmporium.Cinematics.CutscenePlayer Build(Transform canvas)
        {
            var root = SceneBuilderCore.FindOrCreateChild(canvas, "CutscenePlayer");
            SceneBuilderCore.Stretch(root);
            // Always last: draws over every screen and every overlay.
            root.transform.SetAsLastSibling();

            var overlayCanvas = SceneBuilderCore.AddOrGet<Canvas>(root);
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 1000;
            SceneBuilderCore.AddOrGet<GraphicRaycaster>(root);

            var group = SceneBuilderCore.AddOrGet<CanvasGroup>(root);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var videoPlayer = SceneBuilderCore.AddOrGet<VideoPlayer>(root);
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;

            var audioSource = SceneBuilderCore.AddOrGet<AudioSource>(root);
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            var musicGroup = SceneBuilderMixerHelper.FindMusicGroup();

            if (musicGroup != null)
            {
                audioSource.outputAudioMixerGroup = musicGroup;
            }

            var backdrop = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "Backdrop", 0);
            SceneBuilderCore.Stretch(backdrop);
            SceneBuilderCore.SetColorShape(backdrop, Color.black, raycastTarget: true);

            var videoSurfaceGo = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "VideoSurface", 1);
            SceneBuilderCore.CenterAnchor(videoSurfaceGo);
            var rawImage = SceneBuilderCore.AddOrGet<RawImage>(videoSurfaceGo);
            rawImage.raycastTarget = true;
            var fitter = SceneBuilderCore.AddOrGet<AspectRatioFitter>(videoSurfaceGo);
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            var skipHint = SceneBuilderCore.FindOrCreateChildOrdered(root.transform, "SkipHint", 2);
            var skipRect = SceneBuilderCore.Rect(skipHint);
            skipRect.anchorMin = new Vector2(0.5f, 0f);
            skipRect.anchorMax = new Vector2(0.5f, 0f);
            skipRect.pivot = new Vector2(0.5f, 0f);
            skipRect.sizeDelta = new Vector2(240f, 30f);
            skipRect.anchoredPosition = new Vector2(0f, 48f);
            var skipText = SceneBuilderCore.SetupText(skipHint, SceneBuilderCore.FontChoice.SpecialElite, 18f,
                Color.white, TextAlignmentOptions.Center, "ui.cutscene.skipHint", "Tap to skip");
            SceneBuilderCore.SetActive(skipHint, false);

            var cutscenePlayer = SceneBuilderCore.AddOrGet<RestoriumEmporium.Cinematics.CutscenePlayer>(root);
            var so = new SerializedObject(cutscenePlayer);
            SceneBuilderCore.SetField(so, "videoSurface", rawImage, "CutscenePlayer");
            SceneBuilderCore.SetField(so, "videoPlayer", videoPlayer, "CutscenePlayer");
            SceneBuilderCore.SetField(so, "group", group, "CutscenePlayer");
            SceneBuilderCore.SetField(so, "fitter", fitter, "CutscenePlayer");
            SceneBuilderCore.SetField(so, "skipHint", skipText, "CutscenePlayer");
            SceneBuilderCore.SetField(so, "videoAudioSource", audioSource, "CutscenePlayer");
            SceneBuilderCore.SetField(so, "overlayCanvas", overlayCanvas, "CutscenePlayer");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(root, true);
            return cutscenePlayer;
        }
    }

    /// <summary>Tiny shared lookup for the MainMixer's "Music" AudioMixerGroup.</summary>
    internal static class SceneBuilderMixerHelper
    {
        public static UnityEngine.Audio.AudioMixerGroup FindMusicGroup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Audio/MainMixer.mixer");

            if (mixer == null)
            {
                SceneBuilderCore.Problem("Assets/Audio/MainMixer.mixer not found; CutscenePlayer's Audio " +
                                         "Source Output was left empty (it falls back to AudioManager's " +
                                         "Music group, or the Music volume, at runtime).");
                return null;
            }

            var groups = mixer.FindMatchingGroups("Music");
            return groups != null && groups.Length > 0 ? groups[0] : null;
        }
    }
}
