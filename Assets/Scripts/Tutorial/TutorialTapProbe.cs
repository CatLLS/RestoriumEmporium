// ============================================================
// TutorialTapProbe — a one-line listener that reports a tap on the element it sits on.
// WHAT & WHY: A "press this button" tutorial step has to know the player pressed
//   THAT button, without the tutorial owning or editing the button. The controller
//   attaches this probe to the anchored object for the duration of the step and
//   removes it afterwards, so no other system has to know the tutorial exists.
// KEY DECISIONS:
//   - IPointerClickHandler rather than a Button.onClick subscription: the anchor
//     may be a poster, a tool slot or anything else, and not every anchored thing
//     is a Button. This works for any object under a GraphicRaycaster.
//   - The probe does NOT consume the click. Unity's ExecuteEvents dispatches a
//     pointer event to every component on the handler GameObject that implements
//     the interface, so the real Button still fires normally.
//   - Added and destroyed at runtime with HideFlags.DontSave, so it can never be
//     accidentally saved into a scene or a prefab.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. Do NOT add this component by hand in the Inspector.
//     TutorialController adds and removes it automatically while a
//     "tap this target" step is running.
// ---------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RestoriumEmporium.Tutorial
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class TutorialTapProbe : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Raised when the player taps the object this probe is attached to.</summary>
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            Clicked?.Invoke();
        }
    }
}
