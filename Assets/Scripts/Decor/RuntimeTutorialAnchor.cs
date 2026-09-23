// ============================================================
// RuntimeTutorialAnchor — adds a TutorialAnchor with a given id from code.
// WHAT & WHY: Shop cards and the preview decoration are created at runtime, one
//   per catalogue item, so their tutorial anchors ("shop.item.lamp",
//   "preview.item") cannot be typed into the Inspector. TutorialAnchor keeps its
//   id in a private serialized field with no setter (by design, so ids are not
//   changed by accident), so this helper writes it the same way the Inspector
//   does: through Unity's serializer.
// KEY DECISIONS:
//   - JsonUtility.FromJsonOverwrite is Unity's public API for writing serialized
//     fields of a MonoBehaviour. No reflection, no change to TutorialAnchor.
//   - The id must be in place BEFORE the anchor's OnEnable, because that is when
//     it registers. So the object is briefly deactivated if it is active, the
//     component added and filled, then the object restored.
//   - Ids are validated to the anchor id alphabet (letters, digits, . _ -) so the
//     tiny JSON string can never be malformed by an odd item id.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Static helper used by ShopItemCard and DecorationRoom.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.Decor
{
    using RestoriumEmporium.Tutorial;

    public static class RuntimeTutorialAnchor
    {
        /// <summary>
        /// Adds (or re-uses) a TutorialAnchor on <paramref name="target"/> and gives it
        /// <paramref name="anchorId"/>. Returns the component, or null when the id is invalid.
        /// </summary>
        public static TutorialAnchor Attach(GameObject target, string anchorId)
        {
            if (target == null || !IsValidId(anchorId))
            {
                Debug.LogWarning($"[RuntimeTutorialAnchor] Refused anchor id '{anchorId}'. " +
                                 "Use only letters, digits, '.', '_' and '-'.");
                return null;
            }

            var wasActive = target.activeSelf;

            if (wasActive)
            {
                target.SetActive(false);
            }

            var anchor = target.GetComponent<TutorialAnchor>();

            if (anchor == null)
            {
                anchor = target.AddComponent<TutorialAnchor>();
            }

            JsonUtility.FromJsonOverwrite("{\"anchorId\":\"" + anchorId + "\"}", anchor);

            if (wasActive)
            {
                target.SetActive(true);
            }

            return anchor;
        }

        private static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            foreach (var c in id)
            {
                var ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                         c == '.' || c == '_' || c == '-';

                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
