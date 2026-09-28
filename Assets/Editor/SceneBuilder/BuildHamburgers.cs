// ============================================================
// BuildHamburgers — adds the pause hamburger to Cleaning/LinenBacking*.
// WHAT & WHY: CONSISTENCY.md's fix #2: CleaningScreen and the three
//   LinenBackingScreen variants are pre-existing MVP objects that RESTORATION
//   correctly left code-unchanged, but none of them had a hamburger in the
//   actual scene (verified against Game.unity directly). StickerRemovalScreen
//   (new, built by BuildStickerRemoval.cs) already gets its own — this file
//   covers the four screens that were missing theirs.
// KEY DECISIONS:
//   - Same rect/art/components on every screen (Pos X 14 / Y -38, 53x57,
//     hamburgerIcon.png, PauseButton + ButtonSfx + TutorialAnchor pause.button),
//     per each script's own checklist (CleaningScreen.cs §E, LinenBackingScreen.cs).
//   - Last child on each screen so nothing (a poster mid-flip, a tool bar) can
//     ever be drawn over it.
//   - No field on the screen itself references this object — PauseButton is
//     self-contained (see PauseButton.cs's own KEY DECISIONS: it registers
//     itself in a static "who's visible" list, not through a screen reference).
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing by hand — run Restorium/Scene/Build Batch 2 Scene Objects with the
//     Game scene open. See Docs/Batch2/handoff/SCENE_BUILDER.md.
// ---------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    internal static class BuildHamburgers
    {
        private static readonly string[] ScreenNames =
        {
            "CleaningScreen", "LinenBackingFrontScreen", "LinenBackingBackScreen", "LinenBackingFinalScreen"
        };

        public static void Build(Transform canvas, RestoriumEmporium.UI.OverlayController overlays)
        {
            foreach (var name in ScreenNames)
            {
                var screen = canvas.Find(name);

                if (screen == null)
                {
                    SceneBuilderCore.Problem($"'{name}' was not found under Canvas — cannot add its hamburger. " +
                                              "This screen is expected to already exist from the MVP build.");
                    continue;
                }

                AddHamburger(screen, overlays);
            }
        }

        private static void AddHamburger(Transform screen, RestoriumEmporium.UI.OverlayController overlays)
        {
            var go = SceneBuilderCore.FindOrCreateChild(screen, "PauseButtonObject");
            go.transform.SetAsLastSibling();
            SceneBuilderCore.FigmaRect(go, 14f, 38f, 53f, 57f);
            SceneBuilderCore.SetImage(go, "Assets/Art/UI/hamburgerIcon.png");
            SceneBuilderCore.SetupButton(go, addSfx: true);
            SceneBuilderCore.SetupAnchor(go, "pause.button");

            var pause = SceneBuilderCore.AddOrGet<RestoriumEmporium.UI.PauseButton>(go);
            var so = new SerializedObject(pause);
            SceneBuilderCore.SetField(so, "overlays", overlays, $"{screen.name}/PauseButtonObject");
            SceneBuilderCore.SetFieldEnum(so, "opens", RestoriumEmporium.UI.PauseButton.Target.Pause,
                $"{screen.name}/PauseButtonObject");
            so.ApplyModifiedProperties();

            SceneBuilderCore.SetActive(go, true);
        }
    }
}
