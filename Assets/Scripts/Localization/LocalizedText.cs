// ============================================================
// LocalizedText — binds one TMP_Text label to one localisation key.
// WHAT & WHY: Section 5 of the build spec forbids player-facing string literals
//   in code. This is the component that makes that practical: put it on a label,
//   type a key in the Inspector, and the text arrives from the LocaleTable and
//   updates itself whenever the language changes.
// KEY DECISIONS:
//   - Resolves in OnEnable rather than Start, so a label on a screen that is
//     hidden and shown repeatedly re-reads its text every time it comes back —
//     which is what makes a runtime SetKey on a pooled/reused label correct.
//   - Subscribes in OnEnable and unsubscribes in OnDisable, not Awake/OnDestroy.
//     A hidden screen's labels have nothing to redraw, and the localisation
//     service outlives every scene, so an Awake-time subscription would hold
//     destroyed labels alive until a locale change tripped over them.
//   - Start() retries the lookup when OnEnable found no service. Unity runs
//     Awake and OnEnable per object, in an order it does not promise, so a label
//     in the same scene as the Systems object can genuinely run first. The retry
//     costs one null check per label per scene and removes a whole class of
//     "why is my text a key" reports. The Script Execution Order step in the
//     checklist below fixes the cause; this fixes the symptom regardless.
//   - With no service at all, the label shows the key instead of blanking. A
//     designer opening the Game scene directly then sees which label is which,
//     rather than a screen of empty rectangles.
//   - No Update. Text changes only on enable, on SetKey, and on LocaleChanged.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Select any text label in the Hierarchy (an object with a
//     "TextMeshPro - Text (UI)" component; create one with
//     GameObject -> UI -> Text - TextMeshPro).
// [ ] Click "Add Component", type "LocalizedText", press Enter.
// [ ] In the "Key" field type the lookup key for this label, for example
//     ui.journal.restore. It must match a "Key" row in the pt-BR LocaleTable
//     asset exactly, including dots and capitalisation.
// [ ] Leave the TextMeshPro component's own "Text" box alone. Whatever you type
//     there is overwritten at runtime; type the Portuguese there anyway if you
//     want the Scene view to look right while you lay the screen out.
// [ ] Every label needs a font asset that has Portuguese accented characters
//     (a a e o c with accents). If they show as squares, select the TMP font
//     asset and use Window -> TextMeshPro -> Font Asset Creator to regenerate it
//     with the "Extended ASCII" character set.
// [ ] Labels whose text is chosen at runtime (stage headers, Tracy's tutorial
//     lines) still get this component, but with the "Key" field left empty; the
//     controlling script calls SetKey on it.
// ---------------------------------------------------------------

using TMPro;
using UnityEngine;

namespace RestoriumEmporium.Localization
{
    using RestoriumEmporium.Core;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("Lookup key in the LocaleTable, e.g. \"ui.journal.restore\". " +
                 "Leave empty for labels whose key is set from script via SetKey.")]
        [SerializeField] private string key;

        private TMP_Text _label;
        private ILocalizationService _localization;
        private bool _subscribed;

        /// <summary>The key this label currently shows.</summary>
        public string Key => key;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Bind();
            Refresh();
        }

        private void Start()
        {
            // Covers the case where this label's OnEnable ran before the Systems
            // object had registered its services. See KEY DECISIONS.
            if (_localization != null)
            {
                return;
            }

            Bind();

            if (_localization != null)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            Unbind();
        }

        /// <summary>
        /// Points this label at a different key and redraws it immediately.
        /// Used by stage headers and tutorial lines, which pick their key at runtime.
        /// </summary>
        public void SetKey(string newKey)
        {
            if (string.Equals(key, newKey, System.StringComparison.Ordinal))
            {
                return;
            }

            key = newKey;
            Refresh();
        }

        /// <summary>Re-reads the text for the current key. Safe to call at any time.</summary>
        public void Refresh()
        {
            if (_label == null)
            {
                // SetKey can legitimately arrive before Awake on a freshly
                // instantiated object; OnEnable will draw it.
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            // No service (scene opened on its own) means show the key, never blank.
            _label.text = _localization != null ? _localization.Get(key) : key;
        }

        private void Bind()
        {
            if (_subscribed)
            {
                return;
            }

            _localization = ServiceLocator.Get<ILocalizationService>();

            if (_localization == null)
            {
                return;
            }

            _localization.LocaleChanged += Refresh;
            _subscribed = true;
        }

        private void Unbind()
        {
            if (_subscribed && _localization != null)
            {
                _localization.LocaleChanged -= Refresh;
            }

            _subscribed = false;
            _localization = null;
        }
    }
}
