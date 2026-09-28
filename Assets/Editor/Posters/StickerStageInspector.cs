// ============================================================
// StickerStageInspector — drag-to-position preview for sticker peel stages.
// WHAT & WHY: Sticker positions are stored as 0..1 numbers on the stage asset
//   (StickerDefinition.normalizedCenter / normalizedSize), which nobody can
//   picture from the numbers alone. This replaces the Inspector of a
//   RestorationStageData whose Kind is StickerPeel with: the normal fields, then
//   a live preview of the close-up with every sticker drawn where it will appear
//   in the game. Click a sticker and drag it to move it; the scroll wheel over a
//   selected sticker resizes it (keeping its shape). No Play mode needed.
// KEY DECISIONS:
//   - Edits go through Undo.RecordObject + SetDirty, so Ctrl+Z works and the
//     change is saved with the asset like any Inspector edit.
//   - The preview uses the same maths as the game (StickerLayoutMath), with the
//     preview box sized to the close-up's aspect ratio, so what you see is what
//     the phone shows (minus the Cover crop at the screen edges).
//   - Sticker rotation is drawn with GUIUtility.RotateAroundPivot and restored
//     with the saved GUI.matrix, so one rotated sticker cannot tilt the rest of
//     the Inspector.
//   - Scrub stages get the default Inspector untouched.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing to attach. Select Assets/Data/Poster2/Stages/02_Stickers in the
//     Project window; the preview appears under the normal fields.
// [ ] Drag a sticker in the preview to move it. Scroll over it to resize it.
//     "Reset to Figma defaults" is the menu Restorium -> Posters -> Reset Poster 2
//     Sticker Positions (Figma defaults).
// ---------------------------------------------------------------

using UnityEditor;
using UnityEngine;

namespace RestoriumEmporium.EditorTools
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Restoration;

    [CustomEditor(typeof(RestorationStageData))]
    public class StickerStageInspector : Editor
    {
        private const float MaxPreviewWidth = 300f;
        private int _selected = -1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var stage = (RestorationStageData)target;
            if (stage == null || stage.kind != StageKind.StickerPeel)
            {
                return;
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Sticker preview (drag to move, scroll to resize)", EditorStyles.boldLabel);

            if (stage.closeUpSprite == null)
            {
                EditorGUILayout.HelpBox("Assign Close Up Sprite to see the preview.", MessageType.Info);
                return;
            }

            if (stage.screen != GameScreen.StickerRemoval || stage.requiredTool != ToolId.None)
            {
                EditorGUILayout.HelpBox(
                    "A Sticker Peel stage should use Screen = Sticker Removal and Required Tool = None.",
                    MessageType.Warning);
            }

            Rect spriteRect = stage.closeUpSprite.rect;
            float width = Mathf.Min(MaxPreviewWidth, EditorGUIUtility.currentViewWidth - 40f);
            float height = width * (spriteRect.height / Mathf.Max(1f, spriteRect.width));

            Rect box = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));

            DrawSprite(box, stage.closeUpSprite);

            for (int i = 0; i < stage.StickerCount; i++)
            {
                StickerDefinition def = stage.stickers[i];
                if (def == null)
                {
                    continue;
                }

                Rect r = StickerGuiRect(box, def);
                Matrix4x4 saved = GUI.matrix;

                // GUI space has y down, so a counter-clockwise game rotation is negative here.
                GUIUtility.RotateAroundPivot(-def.rotation, r.center);
                if (def.sprite != null)
                {
                    DrawSprite(r, def.sprite);
                }

                DrawOutline(r, i == _selected ? Color.yellow : new Color(1f, 0.3f, 0.6f, 1f));
                GUI.matrix = saved;

                GUI.Label(new Rect(r.x + 2f, r.y + 2f, 30f, 16f), (i + 1).ToString(), EditorStyles.whiteBoldLabel);
            }

            HandleInput(stage, box);

            if (_selected >= 0 && _selected < stage.StickerCount && stage.stickers[_selected] != null)
            {
                StickerDefinition sel = stage.stickers[_selected];
                EditorGUILayout.HelpBox(
                    $"Sticker {_selected + 1}: centre ({sel.normalizedCenter.x:0.000}, {sel.normalizedCenter.y:0.000}), " +
                    $"size ({sel.normalizedSize.x:0.000}, {sel.normalizedSize.y:0.000}), rotation {sel.rotation:0.#} deg",
                    MessageType.None);
            }
        }

        private static Rect StickerGuiRect(Rect box, StickerDefinition def)
        {
            StickerRect local = StickerLayoutMath.NormalizedToLocal(
                def.normalizedCenter.x, def.normalizedCenter.y,
                def.normalizedSize.x, def.normalizedSize.y, box.width, box.height);

            // Local is y-up from the box centre; GUI is y-down from the top-left.
            float cx = box.center.x + local.CenterX;
            float cy = box.center.y - local.CenterY;
            return new Rect(cx - (local.Width * 0.5f), cy - (local.Height * 0.5f), local.Width, local.Height);
        }

        private void HandleInput(RestorationStageData stage, Rect box)
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && box.Contains(e.mousePosition):
                    _selected = HitTest(stage, box, e.mousePosition);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDrag when _selected >= 0 && _selected < stage.StickerCount:
                {
                    StickerDefinition def = stage.stickers[_selected];
                    if (def == null)
                    {
                        break;
                    }

                    Undo.RecordObject(stage, "Move sticker");
                    def.normalizedCenter += new Vector2(e.delta.x / box.width, -e.delta.y / box.height);
                    def.normalizedCenter = new Vector2(
                        Mathf.Clamp01(def.normalizedCenter.x), Mathf.Clamp01(def.normalizedCenter.y));
                    EditorUtility.SetDirty(stage);
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.ScrollWheel when _selected >= 0 && _selected < stage.StickerCount &&
                                                box.Contains(e.mousePosition):
                {
                    StickerDefinition def = stage.stickers[_selected];
                    if (def == null)
                    {
                        break;
                    }

                    Undo.RecordObject(stage, "Resize sticker");
                    float factor = e.delta.y > 0f ? 0.97f : 1.03f;
                    def.normalizedSize = new Vector2(
                        Mathf.Clamp(def.normalizedSize.x * factor, 0.01f, 1f),
                        Mathf.Clamp(def.normalizedSize.y * factor, 0.01f, 1f));
                    EditorUtility.SetDirty(stage);
                    e.Use();
                    Repaint();
                    break;
                }
            }
        }

        private static int HitTest(RestorationStageData stage, Rect box, Vector2 mouse)
        {
            // Last drawn is on top, so test from the end.
            for (int i = stage.StickerCount - 1; i >= 0; i--)
            {
                StickerDefinition def = stage.stickers[i];
                if (def != null && StickerGuiRect(box, def).Contains(mouse))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            Texture2D tex = sprite.texture;
            if (tex == null)
            {
                return;
            }

            Rect tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }

        private static void DrawOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.yMin, 1f, r.height), color);
        }
    }
}
