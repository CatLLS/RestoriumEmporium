// ============================================================
// RestorationScreenBase — shared plumbing for the screens you restore on.
// WHAT & WHY: CleaningScreen and LinenBackingScreen are the same object with
//   different art: a header bound to the current stage, a root the poster stack
//   lives under, and a root the tool bar lives under. This holds that plumbing
//   once so neither screen has to repeat the subscription bookkeeping — and,
//   more importantly, so neither can quietly drift into implementing rules.
// KEY DECISIONS:
//   - The restoration is reached through a serialised MonoBehaviour that is cast
//     to IRestorationRuntime. Unity cannot serialise an interface, and the
//     alternative (ServiceLocator) is reserved for the three process-lifetime
//     services. A scene reference keeps the dependency visible in the Inspector.
//   - Subscribes in OnEnable and unsubscribes in OnDisable, because these
//     screens are disabled GameObjects toggled by the router: Awake would never
//     run for a screen that starts hidden, and a subscription held while hidden
//     would repaint a header nobody is looking at.
//   - OnEnable also pulls CurrentStage directly, so a screen shown *after* its
//     stage started still gets the right header. Relying on the event alone is
//     how you get a blank header exactly once, at the worst moment.
//   - Header text is resolved here rather than by a LocalizedText binder because
//     the key changes with every stage. Static labels keep their binders.
//   - No rules, no coverage maths, no transitions. This class only reads.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This is an abstract base class; there is no component to attach.
//     Attach CleaningScreen or LinenBackingScreen instead and follow the
//     checklist in that file.
// ---------------------------------------------------------------

using TMPro;
using UnityEngine;

namespace RestoriumEmporium.UI
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Localization;
    using RestoriumEmporium.Restoration;

    public abstract class RestorationScreenBase : ScreenView
    {
        [Header("Restoration source")]
        [Tooltip("The object carrying RestorationController. Must implement " +
                 "IRestorationRuntime; this screen only reads from it.")]
        [SerializeField] protected MonoBehaviour restorationSource;

        [Header("Layout roots")]
        [Tooltip("Empty RectTransform the poster layer stack is parented under.")]
        [SerializeField] protected RectTransform posterStackRoot;

        [Tooltip("Empty RectTransform the tool bar background and tools live under.")]
        [SerializeField] protected RectTransform toolBarRoot;

        [Header("Header")]
        [Tooltip("Label showing the current stage title (RestorationStageData.titleKey).")]
        [SerializeField] protected TMP_Text headerLabel;

        private IRestorationRuntime _runtime;
        private bool _runtimeResolved;
        private bool _subscribed;

        /// <summary>The running restoration, or null when nothing is wired.</summary>
        protected IRestorationRuntime Runtime
        {
            get
            {
                if (_runtimeResolved)
                {
                    return _runtime;
                }

                _runtimeResolved = true;

                if (restorationSource != null)
                {
                    // Dropping a GameObject on a MonoBehaviour field makes Unity keep
                    // that object's FIRST component, which is rarely the intended one.
                    // Look along the whole object before calling it a mis-wiring.
                    _runtime = restorationSource as IRestorationRuntime
                               ?? restorationSource.GetComponent<IRestorationRuntime>();

                    if (_runtime == null)
                    {
                        Debug.LogError(
                            "[RestorationScreenBase] The object in Restoration Source " +
                            $"('{restorationSource.name}') has no component implementing " +
                            "IRestorationRuntime. Drag the object that carries " +
                            "RestorationController into it.", this);
                    }
                }

                return _runtime;
            }
        }

        public RectTransform PosterStackRoot => posterStackRoot;

        public RectTransform ToolBarRoot => toolBarRoot;

        protected virtual void OnEnable()
        {
            Subscribe();

            // A stage may have started while this screen was hidden; pull the
            // current one rather than waiting for an event that already fired.
            RestorationStageData stage = Runtime != null ? Runtime.CurrentStage : null;
            ApplyStage(stage, Runtime != null ? Runtime.CurrentStageIndex : -1);
        }

        protected virtual void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || Runtime == null)
            {
                return;
            }

            Runtime.StageStarted += HandleStageStarted;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _runtime == null)
            {
                return;
            }

            _runtime.StageStarted -= HandleStageStarted;
            _subscribed = false;
        }

        private void HandleStageStarted(RestorationStageData stage, int index)
        {
            ApplyStage(stage, index);
        }

        /// <summary>
        /// Repaints anything that depends on the active stage. Override to add
        /// screen-specific reactions, then call base.
        /// </summary>
        protected virtual void ApplyStage(RestorationStageData stage, int index)
        {
            if (headerLabel == null)
            {
                return;
            }

            headerLabel.text = Localize(stage != null ? stage.titleKey : string.Empty);
        }

        protected static string Localize(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return ServiceLocator.TryGet(out ILocalizationService loc) ? loc.Get(key) : key;
        }
    }
}
