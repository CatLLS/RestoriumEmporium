// ============================================================
// SceneBuilderCore — shared helpers every Build*.cs file uses.
// WHAT & WHY: Batch 2 needs a lot of near-identical Editor-time work: find (or
//   create) a named child, size a RectTransform the way the Figma spec gives
//   coordinates (top-left origin, pixel rect), drop an Image/TMP text/Button on
//   it, and push a value into a MonoBehaviour's private [SerializeField] by
//   name. Every Build*.cs file in this folder is just data (which objects, which
//   art, which fields) fed through these few operations, so the actual
//   Unity-object plumbing is written and tested once, here.
// KEY DECISIONS:
//   - IDEMPOTENT BY NAME: FindOrCreateChild looks for an existing child with the
//     exact given name before creating a new one. Re-running the whole builder
//     never duplicates an object; it just finds the same name and updates it.
//     Sibling order is re-applied every run too (SetAsLastSibling as each
//     expected child is processed in front-to-back order), so the hierarchy
//     self-heals if someone dragged something out of order by hand.
//   - THE FIGMA RECT FORMULA lives in exactly one place (FigmaRect). Figma gives
//     (x, y, w, h) from a frame's top-left corner; every anchor here is pinned
//     to the PARENT's own top-left corner (anchorMin = anchorMax = (0, 1)), and
//     the requested pivot then places the object exactly where every handoff's
//     "Pos X / Pos Y" checklist entry already describes by hand:
//       anchoredPosition = (x + w*pivotX, -(y + h*(1-pivotY)))
//     Pivot (0,1) [top-left, the default] collapses that to (x, -y), matching
//     every "Pos X = x, Pos Y = -y" instruction in the codebase's own checklists
//     word for word. Pivot (0.5,0.5) matches the "centre pivot recommended"
//     notes (e.g. SHOP.md's BookButton math), also word for word.
//   - SERIALIZEDOBJECT, NEVER DIRECT FIELD ACCESS: every cross-agent component
//     wires through FindProperty(name) so a renamed/removed field is a LOUD,
//     reported problem (added to Problems and logged) instead of a silent
//     no-op or a compile error the human has to chase across ten files.
//   - Undo: new GameObjects/components go through Undo.RegisterCreatedObjectUndo
//     / Undo.AddComponent so the whole run collapses into one Ctrl+Z step (the
//     menu command wraps everything in one Undo group). SerializedObject writes
//     use ApplyModifiedProperties() (WITH undo), not WithoutUndo, for the same
//     reason.
//   - Sprites/fonts/assets are loaded through AssetDatabase.LoadAssetAtPath and
//     a missing one is reported (never a silent blank Image) — this is what
//     lets "Build Batch 2 Scene Objects" tell the human exactly which earlier
//     menu (Fix Art Import Settings, Create Starter Items, ...) still needs to
//     run instead of leaving them to guess from a grey box in the Scene view.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This file has no menu items and adds no component; it is pure
//     helper code used by the other files in Assets/Editor/SceneBuilder/. See
//     Docs/Batch2/handoff/SCENE_BUILDER.md for the human steps.
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RestoriumEmporium.EditorTools
{
    /// <summary>
    /// Shared, stateless-except-for-the-report helpers for the Batch 2 scene
    /// builder. Every Build*.cs file in this folder calls into this one.
    /// </summary>
    public static class SceneBuilderCore
    {
        public const float RefWidth = 412f;
        public const float RefHeight = 917f;

        public const string RyeFontPath = "Assets/Fonts/Rye-Regular SDF.asset";
        public const string SpecialEliteFontPath = "Assets/Fonts/SpecialElite-Regular SDF.asset";

        private static TMP_FontAsset _rye;
        private static TMP_FontAsset _specialElite;

        /// <summary>Every child object that did not exist and was created this run.</summary>
        public static readonly List<string> Created = new List<string>();

        /// <summary>Every existing object/field that was touched (updated) this run.</summary>
        public static readonly List<string> Updated = new List<string>();

        /// <summary>Everything that needs a human's attention: missing asset, missing field, etc.</summary>
        public static readonly List<string> Problems = new List<string>();

        public static void ResetReport()
        {
            Created.Clear();
            Updated.Clear();
            Problems.Clear();
        }

        public static void NoteCreated(GameObject go) => Created.Add(PathOf(go));

        public static void NoteUpdated(string what) => Updated.Add(what);

        public static void Problem(string what)
        {
            Problems.Add(what);
            Debug.LogWarning("[SceneBuilder] " + what);
        }

        public static string PathOf(GameObject go)
        {
            if (go == null)
            {
                return "(null)";
            }

            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;

            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
            }

            return sb.ToString();
        }

        // ---- Hierarchy -----------------------------------------------------------------

        /// <summary>
        /// Finds a DIRECT child of <paramref name="parent"/> named exactly
        /// <paramref name="name"/>, or creates one (with a RectTransform, since
        /// every object this builder makes lives under a Canvas). Newly created
        /// objects are registered with Undo.
        /// </summary>
        public static GameObject FindOrCreateChild(Transform parent, string name)
        {
            if (parent == null)
            {
                Problem($"FindOrCreateChild('{name}'): parent is null.");
                return null;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);

                if (c.name == name)
                {
                    return c.gameObject;
                }
            }

            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Restorium Scene Builder");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            NoteCreated(go);
            return go;
        }

        /// <summary>
        /// Finds a direct child named <paramref name="newName"/>; if none exists, looks
        /// for one of <paramref name="legacyNames"/> (an MVP-era object being carried
        /// forward into the Batch 2 redesign) and RENAMES it in place — same object,
        /// same GUID, no visual duplicate — instead of creating a second object next to
        /// an orphaned old one. Only creates a fresh object when neither is found.
        /// </summary>
        public static GameObject FindRenameOrCreateChild(Transform parent, string newName,
            params string[] legacyNames)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);

                if (c.name == newName)
                {
                    return c.gameObject;
                }
            }

            if (legacyNames != null)
            {
                foreach (var legacy in legacyNames)
                {
                    var found = parent.Find(legacy);

                    if (found != null)
                    {
                        Undo.RecordObject(found.gameObject, "Restorium Scene Builder");
                        found.gameObject.name = newName;
                        NoteUpdated($"Renamed '{PathOf(parent.gameObject)}/{legacy}' -> '{newName}' " +
                                     "(same object, carried forward from the MVP scene).");
                        return found.gameObject;
                    }
                }
            }

            return FindOrCreateChild(parent, newName);
        }

        /// <summary>
        /// Disables (never deletes) a legacy child that has no place in the Batch 2
        /// design, and reports it so the human can decide whether to delete it by hand.
        /// </summary>
        public static void RetireLegacyChild(Transform parent, string legacyName)
        {
            var found = parent.Find(legacyName);

            if (found == null || !found.gameObject.activeSelf)
            {
                return;
            }

            Undo.RecordObject(found.gameObject, "Restorium Scene Builder");
            found.gameObject.SetActive(false);
            Problem($"'{PathOf(parent.gameObject)}/{legacyName}' has no place in the Batch 2 design and was " +
                    "disabled (not deleted) rather than removed. Delete it by hand once you've confirmed " +
                    "nothing still needs it.");
        }

        /// <summary>
        /// Finds or creates a child, then moves it to <paramref name="siblingIndex"/>.
        /// Call this once per child, IN FRONT-TO-BACK ORDER, to keep draw order
        /// matching FigmaLayout.md every time the builder re-runs.
        /// </summary>
        public static GameObject FindOrCreateChildOrdered(Transform parent, string name, int siblingIndex)
        {
            var go = FindOrCreateChild(parent, name);

            if (go != null)
            {
                go.transform.SetSiblingIndex(siblingIndex);
            }

            return go;
        }

        public static T AddOrGet<T>(GameObject go) where T : Component
        {
            if (go == null)
            {
                return null;
            }

            var c = go.GetComponent<T>();

            if (c == null)
            {
                c = Undo.AddComponent<T>(go);
            }

            return c;
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        // ---- Rect --------------------------------------------------------------------

        public static RectTransform Rect(GameObject go)
        {
            var r = go.GetComponent<RectTransform>();
            return r != null ? r : go.AddComponent<RectTransform>();
        }

        /// <summary>Anchors stretch/stretch with every offset 0 (fills the parent).</summary>
        public static RectTransform Stretch(GameObject go)
        {
            var r = Rect(go);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            return r;
        }

        /// <summary>
        /// Places a rect using a Figma top-left-origin (x, y, w, h) box, anchored to
        /// the PARENT's own top-left corner. See KEY DECISIONS for the formula.
        /// Default pivot (0,1) matches every "Pos X = x, Pos Y = -y" checklist entry.
        /// </summary>
        public static RectTransform FigmaRect(GameObject go, float x, float y, float w, float h,
            float pivotX = 0f, float pivotY = 1f)
        {
            var r = Rect(go);
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(pivotX, pivotY);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x + w * pivotX, -(y + h * (1f - pivotY)));
            return r;
        }

        public static void CenterAnchor(GameObject go)
        {
            var r = Rect(go);
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // ---- Assets --------------------------------------------------------------------

        public static Sprite LoadSprite(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Problem($"Missing sprite at '{path}'. If the file exists, run " +
                        "Restorium/Fix Art Import Settings (Sprites) first (it must import as " +
                        "Sprite (2D and UI) before AssetDatabase can find it as one).");
            }

            return sprite;
        }

        public static T LoadAsset<T>(string path, string humanNameForError = null) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                Problem($"Missing {humanNameForError ?? typeof(T).Name} at '{path}'.");
            }

            return asset;
        }

        public static TMP_FontAsset Rye => _rye != null ? _rye : (_rye = LoadFont(RyeFontPath));

        public static TMP_FontAsset SpecialElite =>
            _specialElite != null ? _specialElite : (_specialElite = LoadFont(SpecialEliteFontPath));

        private static TMP_FontAsset LoadFont(string path)
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (f == null)
            {
                Problem($"Missing TMP font asset at '{path}'. Follow SETUP.md Part 2 to generate it.");
            }

            return f;
        }

        // ---- Image ----------------------------------------------------------------------

        public static Image SetImage(GameObject go, string spritePath, bool raycastTarget = false,
            Color? color = null)
        {
            var img = AddOrGet<Image>(go);

            if (!string.IsNullOrEmpty(spritePath))
            {
                var sprite = LoadSprite(spritePath);

                if (sprite != null)
                {
                    img.sprite = sprite;
                }
            }

            img.raycastTarget = raycastTarget;
            img.color = color ?? Color.white;
            return img;
        }

        /// <summary>A flat-colour Image with no sprite (Figma "shape: fill #rrggbb").</summary>
        public static Image SetColorShape(GameObject go, Color color, bool raycastTarget = false)
        {
            var img = AddOrGet<Image>(go);
            img.sprite = null;
            img.color = color;
            img.raycastTarget = raycastTarget;
            return img;
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var c))
            {
                c.a = alpha;
                return c;
            }

            Problem($"Bad hex colour '{hex}'.");
            return Color.magenta;
        }

        // ---- Text -------------------------------------------------------------------------

        public enum FontChoice
        {
            Rye,
            SpecialElite
        }

        /// <summary>
        /// Sets up a TMP label. Pass <paramref name="localizedKey"/> to add/refresh a
        /// LocalizedText component; pass <paramref name="placeholder"/> alone (no key)
        /// for script-set text (the label still gets the placeholder so it reads right
        /// in the Scene view before Play).
        /// </summary>
        public static TextMeshProUGUI SetupText(GameObject go, FontChoice font, float size, Color color,
            TextAlignmentOptions align, string localizedKey = null, string placeholder = null)
        {
            var tmp = AddOrGet<TextMeshProUGUI>(go);
            var asset = font == FontChoice.Rye ? Rye : SpecialElite;

            if (asset != null)
            {
                tmp.font = asset;
            }

            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            if (placeholder != null)
            {
                tmp.text = placeholder;
            }

            if (localizedKey != null)
            {
                var lt = AddOrGet<RestoriumEmporium.Localization.LocalizedText>(go);
                var so = new SerializedObject(lt);
                SetField(so, "key", localizedKey, PathOf(go));
                so.ApplyModifiedProperties();
            }

            return tmp;
        }

        // ---- Buttons ------------------------------------------------------------------

        public static Button SetupButton(GameObject go, bool addSfx = true,
            RestoriumEmporium.Core.SfxId sfx = RestoriumEmporium.Core.SfxId.ButtonClick)
        {
            var img = AddOrGet<Image>(go);
            var btn = AddOrGet<Button>(go);

            if (btn.targetGraphic == null)
            {
                btn.targetGraphic = img;
            }

            if (addSfx)
            {
                var bsfx = AddOrGet<RestoriumEmporium.UI.ButtonSfx>(go);
                var so = new SerializedObject(bsfx);
                SetFieldEnum(so, "sfx", sfx, PathOf(go));
                so.ApplyModifiedProperties();
            }

            return btn;
        }

        // ---- Tutorial anchors -----------------------------------------------------------

        public static RestoriumEmporium.Tutorial.TutorialAnchor SetupAnchor(GameObject go, string anchorId)
        {
            var a = AddOrGet<RestoriumEmporium.Tutorial.TutorialAnchor>(go);
            var so = new SerializedObject(a);
            SetField(so, "anchorId", anchorId, PathOf(go));
            so.ApplyModifiedProperties();
            return a;
        }

        // ---- SerializedObject field assignment (loud on a missing field) ----------------

        public static void SetField(SerializedObject so, string propName, UnityEngine.Object value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.objectReferenceValue = value;
            }
        }

        public static void SetField(SerializedObject so, string propName, string value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.stringValue = value ?? string.Empty;
            }
        }

        public static void SetField(SerializedObject so, string propName, bool value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.boolValue = value;
            }
        }

        public static void SetField(SerializedObject so, string propName, float value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.floatValue = value;
            }
        }

        public static void SetField(SerializedObject so, string propName, int value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.intValue = value;
            }
        }

        public static void SetField(SerializedObject so, string propName, Color value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.colorValue = value;
            }
        }

        public static void SetField(SerializedObject so, string propName, Vector2 value, string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.vector2Value = value;
            }
        }

        /// <summary>
        /// Sets an enum-backed field by its underlying int VALUE (not the popup
        /// index) — correct even for enums with gaps/explicit numbers, e.g. SfxId.
        /// </summary>
        public static void SetFieldEnum<TEnum>(SerializedObject so, string propName, TEnum value, string context)
            where TEnum : Enum
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p != null)
            {
                p.intValue = Convert.ToInt32(value);
            }
        }

        public static void SetFieldArray(SerializedObject so, string propName, UnityEngine.Object[] values,
            string context)
        {
            var p = FindPropertyOrProblem(so, propName, context);

            if (p == null)
            {
                return;
            }

            values ??= Array.Empty<UnityEngine.Object>();
            p.arraySize = values.Length;

            for (var i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static SerializedProperty FindPropertyOrProblem(SerializedObject so, string propName, string context)
        {
            var p = so.FindProperty(propName);

            if (p == null)
            {
                Problem($"[{context}] '{so.targetObject.GetType().Name}' has no serialized field named " +
                        $"'{propName}'. Its fields may have been renamed — the scene builder needs updating " +
                        "to match the current script.");
            }

            return p;
        }
    }
}
