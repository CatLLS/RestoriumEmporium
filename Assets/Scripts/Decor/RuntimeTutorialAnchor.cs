// ============================================================
// RuntimeTutorialAnchor — adds a TutorialAnchor with a given id from code.
// WHAT & WHY: Shop cards and the preview decoration are created at runtime, one
//   per catalogue item, so their tutorial anchors ("shop.item.lamp",
//   "preview.item") cannot be typed into the Inspector. TutorialAnchor exposes a
//   public SetAnchorId for exactly this; this helper is a one-line convenience
//   (AddComponent-if-missing + SetAnchorId) so callers don't repeat that pattern.
// KEY DECISIONS:
//   - Uses TutorialAnchor.SetAnchorId (its own public API), not reflection or
//     JsonUtility: SetAnchorId already does the right thing whether the object is
//     active or not (a blank id never registers, so AddComponent's own OnEnable
//     is a no-op; SetAnchorId then registers once, correctly).
//   - Ids are validated to the anchor id alphabet (letters, digits, . _ -) so a
//     malformed item id can never produce a silently-broken anchor.
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

            var anchor = target.GetComponent<TutorialAnchor>();

            if (anchor == null)
            {
                anchor = target.AddComponent<TutorialAnchor>();
            }

            anchor.SetAnchorId(anchorId);
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
