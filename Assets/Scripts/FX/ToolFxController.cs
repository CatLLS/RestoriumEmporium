// ============================================================
// ToolFxController — puffs the right particles at the point the finger paints.
// WHAT & WHY: Every tool needs its own visual feedback under the stroke: brown
//   dust off the dust remover, blue droplets off the spray, graphite off the
//   pencil. This owns one pool per tool, follows which tool is active, and emits
//   at the paint position.
// KEY DECISIONS:
//   - The painter pushes into a public EmitAt(Vector2 screenPos); this class
//     does NOT subscribe to it. Agent B RevealMaskPainter is being written in
//     parallel, and a serialised MonoBehaviour cast to a locally-declared
//     interface would only compile as long as both sides agreed on a member name
//     nobody owns. A public method is the smaller contract: the painter calls it
//     directly if it holds a reference, or the human wires a UnityEvent<Vector2>
//     to it in the Inspector. Either way nothing here has to compile against
//     anything that does not exist yet.
//   - Tool selection DOES come through IRestorationRuntime, because that is a
//     shared contract that already exists and already promises a ToolSelected
//     event. Reading it beats making the painter tell us twice.
//   - Pools are built once in Awake from ToolData.fxPrefab, one child object per
//     tool. Building them lazily on first use would put an Instantiate inside
//     the first drag, which is exactly the hitch pooling exists to avoid.
//   - Emission is throttled by screen distance, not by time. A slow, careful
//     stroke and a fast flick should leave the same density of particles along
//     the path; a timer would make the flick sparse and the slow drag a solid
//     wall.
//   - Screen-to-world uses the UI camera at the canvas plane distance, so the
//     particles land on the same plane the poster is drawn on. A perspective UI
//     camera makes any other depth visibly wrong.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] First build the six particle prefabs. See the checklist at the top of
//     ParticleBurstPool.cs — do that before this, or there is nothing to pool.
// [ ] Assign each prefab to its tool asset: select Assets/Data/Tools/ToolDustRemover
//     and drag Assets/Prefabs/FxDustRemover into its "Fx Prefab" field. Repeat:
//       ToolDustRemover -> FxDustRemover
//       ToolWaterSpray  -> FxWaterSpray
//       ToolDeacidifier -> FxDeacidifier
//       ToolSqueegee    -> FxSqueegee
//       ToolRoller      -> FxRoller
//       ToolPencil      -> FxPencil
// [ ] In Game.unity, right-click the Canvas -> Create Empty. Name it exactly:
//     ToolFx
//     Rect Transform: anchor preset stretch/stretch, Left/Right/Top/Bottom = 0.
// [ ] Select ToolFx -> Add Component -> Tool Fx Controller.
// [ ] Wire the Inspector on ToolFx:
//       Restoration Source <- the GameFlow object (has RestorationController)
//       Ui Camera          <- the Main Camera (the same one in Canvas ->
//                             Render Camera; leave empty to auto-find it)
//       Tools              -> set Size to 6, then drag the six ToolData assets
//                             from Assets/Data/Tools into elements 0..5
//       Pool Size            leave at 4
//       Particles Per Emit   leave at 3
//       Min Screen Distance  leave at 8
//       Sorting Layer Name   leave at "Default"
//       Sorting Order        leave at 1
//       Plane Distance       leave at 100 — it MUST match the Canvas
//                             Plane Distance, or the puffs appear at the wrong
//                             size and drift as you drag.
// [ ] Tell the painter where to send its paint positions. Whichever of these
//     Agent B RevealMaskPainter exposes:
//       - if it has a "Painted At" UnityEvent in the Inspector: click +, drag
//         the ToolFx object in, and choose ToolFxController -> EmitAt.
//       - if it has a "Tool Fx" object field instead: drag ToolFx into it.
//     Nothing else needs wiring; without this step the particles never fire.
// [ ] IMPORTANT sorting check: each screen ToolBarRoot must have its own Canvas
//     with Override Sorting ticked and Order in Layer = 2, and the main Canvas
//     must be Order in Layer = 0. Particles sit at 1, between the two, so they
//     puff over the poster but never over the tools. If you skip this, the
//     particles will either be invisible or draw on top of the tool bar.
// ---------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace RestoriumEmporium.FX
{
    using RestoriumEmporium.Core;
    using RestoriumEmporium.Data;
    using RestoriumEmporium.Restoration;

    [DisallowMultipleComponent]
    public class ToolFxController : MonoBehaviour
    {
        [Header("Sources")]
        [Tooltip("The object carrying RestorationController. Must implement " +
                 "IRestorationRuntime; only ToolSelected is read from it.")]
        [SerializeField] private MonoBehaviour restorationSource;

        [Tooltip("The camera the Canvas renders through. Leave empty to use " +
                 "Camera.main at Awake.")]
        [SerializeField] private Camera uiCamera;

        [Header("Content")]
        [Tooltip("Every tool. Each supplies its own particle prefab via Fx Prefab. " +
                 "A tool with no prefab simply has no particles.")]
        [SerializeField] private ToolData[] tools = new ToolData[0];

        [Header("Pooling")]
        [Range(1, 16)]
        [Tooltip("Particle systems kept alive per tool.")]
        [SerializeField] private int poolSize = 4;

        [Range(1, 30)]
        [Tooltip("Particles emitted per burst.")]
        [SerializeField] private int particlesPerEmit = 3;

        [Range(0f, 64f)]
        [Tooltip("Minimum screen-pixel gap between bursts. Keeps a slow drag from " +
                 "emitting a solid wall of particles.")]
        [SerializeField] private float minScreenDistance = 8f;

        [Header("Sorting")]
        [Tooltip("Must match the Canvas sorting layer, normally \"Default\".")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("1 puts particles above the poster and below the tool bar.")]
        [SerializeField] private int sortingOrder = 1;

        [Tooltip("Must equal the Canvas Plane Distance, so particles land on the " +
                 "same plane the UI is drawn on.")]
        [SerializeField] private float planeDistance = 100f;

        private readonly Dictionary<ToolId, ParticleBurstPool> _pools =
            new Dictionary<ToolId, ParticleBurstPool>();

        private IRestorationRuntime _runtime;
        private ParticleBurstPool _activePool;
        private Vector2 _lastEmitScreenPos;
        private bool _hasEmitted;
        private bool _subscribed;

        private void Awake()
        {
            if (uiCamera == null)
            {
                uiCamera = Camera.main;
            }

            BuildPools();
        }

        private void OnEnable()
        {
            _runtime = restorationSource as IRestorationRuntime;

            if (_runtime == null)
            {
                if (restorationSource != null)
                {
                    Debug.LogError(
                        "[ToolFxController] Restoration Source does not implement " +
                        "IRestorationRuntime. Drag the object that carries " +
                        "RestorationController into it.", this);
                }

                return;
            }

            _runtime.ToolSelected += HandleToolSelected;
            _subscribed = true;

            HandleToolSelected(_runtime.ActiveTool);
        }

        private void OnDisable()
        {
            if (_subscribed && _runtime != null)
            {
                _runtime.ToolSelected -= HandleToolSelected;
            }

            _subscribed = false;
            _hasEmitted = false;
        }

        /// <summary>
        /// Emits the active tool particles at a screen position. Call this from the
        /// painter on every point of a stroke; it is allocation-free and throttles
        /// itself.
        /// </summary>
        public void EmitAt(Vector2 screenPosition)
        {
            if (_activePool == null || !_activePool.IsReady || uiCamera == null)
            {
                return;
            }

            if (_hasEmitted)
            {
                float dx = screenPosition.x - _lastEmitScreenPos.x;
                float dy = screenPosition.y - _lastEmitScreenPos.y;

                if ((dx * dx) + (dy * dy) < minScreenDistance * minScreenDistance)
                {
                    return;
                }
            }

            _lastEmitScreenPos = screenPosition;
            _hasEmitted = true;

            Vector3 world = uiCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, planeDistance));

            _activePool.EmitAt(world, particlesPerEmit);
        }

        /// <summary>Call when a stroke ends, so the next one is not throttled against it.</summary>
        public void EndStroke()
        {
            _hasEmitted = false;
        }

        /// <summary>Stops every pool. Useful when a stage ends mid-drag.</summary>
        public void StopAll()
        {
            foreach (KeyValuePair<ToolId, ParticleBurstPool> pair in _pools)
            {
                if (pair.Value != null)
                {
                    pair.Value.StopAll();
                }
            }

            _hasEmitted = false;
        }

        private void HandleToolSelected(ToolId tool)
        {
            _hasEmitted = false;
            _pools.TryGetValue(tool, out _activePool);
        }

        private void BuildPools()
        {
            if (tools == null)
            {
                return;
            }

            for (int i = 0; i < tools.Length; i++)
            {
                ToolData tool = tools[i];

                if (tool == null || tool.id == ToolId.None || tool.fxPrefab == null)
                {
                    continue;
                }

                if (_pools.ContainsKey(tool.id))
                {
                    Debug.LogWarning(
                        "[ToolFxController] Two entries in Tools share the id " +
                        tool.id + ". The second is ignored.", this);
                    continue;
                }

                ParticleSystem prefabSystem = tool.fxPrefab.GetComponent<ParticleSystem>();

                if (prefabSystem == null)
                {
                    Debug.LogWarning(
                        "[ToolFxController] Fx Prefab on " + tool.name +
                        " has no Particle System component. No particles for " +
                        tool.id + ".", this);
                    continue;
                }

                // One child object per tool, so each pool keeps its own systems
                // parented tidily and can be inspected in the Hierarchy at runtime.
                var holder = new GameObject("Pool_" + tool.id);
                holder.transform.SetParent(transform, false);

                ParticleBurstPool pool = holder.AddComponent<ParticleBurstPool>();
                pool.Configure(prefabSystem, poolSize, sortingLayerName, sortingOrder);

                _pools.Add(tool.id, pool);
            }
        }
    }
}
