// ============================================================
// TutorialAnchor — marks a UI element as something the tutorial can point at.
// WHAT & WHY: Tutorial steps are ScriptableObjects and cannot hold references
//   to scene objects. Each pointable element instead advertises a string id
//   here, and TutorialController looks the id up at runtime.
// KEY DECISIONS:
//   - Registers in OnEnable and unregisters in OnDisable, not Awake/OnDestroy.
//     The gameplay screens are disabled GameObjects sharing one scene, so two
//     screens may legitimately declare the same anchor id; only the enabled one
//     should ever be found.
//   - The registry is a static dictionary keyed by id, holding the most recently
//     enabled anchor. Lookups happen once per tutorial step, not per frame, so
//     a dictionary is more than fast enough and keeps the API trivial.
//   - Duplicate ids log a warning rather than throwing. A mis-typed id should
//     cost a hint in the console, not a crashed tutorial in a playtester's hands.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Add this component to every element the tutorial hand should point at:
//     the journal Restore button, each of the six tool buttons, the poster
//     itself, and the FinishedRepair Continue button.
// [ ] Give each one a unique Anchor Id. Use these exact strings, because the
//     TutorialStep assets reference them:
//       journal.restoreButton
//       toolbar.dustRemover, toolbar.waterSpray, toolbar.deacidifier
//       toolbar.squeegee, toolbar.roller, toolbar.pencil
//       poster.surface
//       finishedRepair.continueButton
// [ ] Leave Point At empty to point at this object's own RectTransform.
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.Tutorial
{
    [DisallowMultipleComponent]
    public class TutorialAnchor : MonoBehaviour
    {
        [Tooltip("Unique id referenced by TutorialStepData.targetAnchorId.")]
        [SerializeField] private string anchorId = string.Empty;

        [Tooltip("Optional. The rect the hand should point at. " +
                 "Leave empty to use this object's own RectTransform.")]
        [SerializeField] private RectTransform pointAt;

        private static readonly Dictionary<string, TutorialAnchor> Registry =
            new Dictionary<string, TutorialAnchor>();

        public string AnchorId => anchorId;

        /// <summary>The rect the tutorial hand should hover over.</summary>
        public RectTransform Target => pointAt != null ? pointAt : transform as RectTransform;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                Debug.LogWarning($"[TutorialAnchor] '{name}' has no Anchor Id and will never be found.", this);
                return;
            }

            if (Registry.TryGetValue(anchorId, out var existing) && existing != null && existing != this)
            {
                Debug.LogWarning(
                    $"[TutorialAnchor] Anchor id '{anchorId}' is already registered by '{existing.name}'. " +
                    $"'{name}' will take over while it is enabled.", this);
            }

            Registry[anchorId] = this;
        }

        private void OnDisable()
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                return;
            }

            // Only clear the entry if we are still the registered owner: a screen
            // being disabled must not unregister the anchor that replaced it.
            if (Registry.TryGetValue(anchorId, out var current) && current == this)
            {
                Registry.Remove(anchorId);
            }
        }

        /// <summary>Finds the enabled anchor with this id, or null.</summary>
        public static TutorialAnchor Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return Registry.TryGetValue(id, out var anchor) && anchor != null ? anchor : null;
        }

        /// <summary>Test-only reset so EditMode tests start from a clean registry.</summary>
        public static void ClearRegistry() => Registry.Clear();
    }
}
