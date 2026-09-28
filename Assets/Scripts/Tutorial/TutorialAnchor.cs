// ============================================================
// TutorialAnchor — marks a UI element as something the tutorial can point at.
// WHAT & WHY: Tutorial steps are ScriptableObjects and cannot hold references
//   to scene objects. Each pointable element instead advertises a string id
//   here, and TutorialController / HandPointer / TutorialInputGate look the id
//   up at runtime.
// KEY DECISIONS:
//   - Registers in OnEnable and unregisters in OnDisable, not Awake/OnDestroy.
//     The gameplay screens are disabled GameObjects sharing one scene, so two
//     screens may legitimately declare the same anchor id; only the enabled one
//     should ever be found.
//   - The registry is a static dictionary keyed by id, holding the most recently
//     enabled anchor. Lookups are a dictionary hit with no allocation, so the
//     hand pointer can afford to re-resolve its anchor every frame — which is
//     what lets it follow anchors that are spawned at runtime (shop cards) or
//     that MOVE (the "sticker.next" anchor hops to the next sticker).
//   - Batch 2: SetAnchorId / SetPointAt let runtime-spawned objects (ShopItemCard
//     adds "shop.item.<id>" per card) configure an anchor after AddComponent.
//     Because AddComponent runs OnEnable before the id can be set, a blank id is
//     only reported in Start, once, if it is still blank then.
//   - Duplicate ids log a warning rather than throwing. A mis-typed id should
//     cost a hint in the console, not a crashed tutorial in a playtester's hands.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Add this component to every element the tutorial hand should point at
//     and type its Anchor Id. Use these exact strings (the tutorial assets use them):
//       journal.restoreButton, journal.nextPage, journal.prevPage, journal.back
//       toolbar.dustRemover, toolbar.waterSpray, toolbar.deacidifier
//       toolbar.squeegee, toolbar.roller, toolbar.pencil
//       poster.surface
//       finishedRepair.continueButton, finishedRepair.doubleButton
//       desk.shop, desk.book, desk.edit, desk.menu, desk.coins
//       shop.back, preview.buy, preview.cancel, preview.item
//       edit.place, edit.undo, edit.done, pause.button
//     (shop.item.<itemId> and sticker.next are added / moved from code at runtime.)
// [ ] Put the anchor on the object that RECEIVES the tap (the Button itself),
//     so "tap this" steps can hear the tap.
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
            Register();
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                Debug.LogWarning($"[TutorialAnchor] '{name}' has no Anchor Id and will never be found.", this);
            }
        }

        private void OnDisable()
        {
            Unregister();
        }

        /// <summary>
        /// Changes this anchor's id at runtime (e.g. a shop card spawned from code).
        /// Re-registers immediately when enabled.
        /// </summary>
        public void SetAnchorId(string id)
        {
            if (string.Equals(anchorId, id, System.StringComparison.Ordinal))
            {
                return;
            }

            Unregister();
            anchorId = id ?? string.Empty;

            if (isActiveAndEnabled)
            {
                Register();
            }
        }

        /// <summary>Changes which rect the hand points at. Null = this object's own rect.</summary>
        public void SetPointAt(RectTransform target)
        {
            pointAt = target;
        }

        private void Register()
        {
            if (string.IsNullOrEmpty(anchorId))
            {
                return;
            }

            if (Registry.TryGetValue(anchorId, out var existing) && existing != null && existing != this &&
                existing.isActiveAndEnabled)
            {
                Debug.LogWarning(
                    $"[TutorialAnchor] Anchor id '{anchorId}' is already registered by '{existing.name}'. " +
                    $"'{name}' will take over while it is enabled.", this);
            }

            Registry[anchorId] = this;
        }

        private void Unregister()
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

        /// <summary>Finds the enabled anchor with this id, or null. No allocation.</summary>
        public static TutorialAnchor Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return Registry.TryGetValue(id, out var anchor) && anchor != null ? anchor : null;
        }

        /// <summary>True when an enabled anchor with this id exists and its target is on screen.</summary>
        public static bool IsAvailable(string id)
        {
            var anchor = Find(id);

            if (anchor == null || !anchor.isActiveAndEnabled)
            {
                return false;
            }

            var target = anchor.Target;
            return target != null && target.gameObject.activeInHierarchy;
        }

        /// <summary>Test-only reset so EditMode tests start from a clean registry.</summary>
        public static void ClearRegistry() => Registry.Clear();
    }
}
