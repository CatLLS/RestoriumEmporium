// ============================================================
// ParticleBurstPool — a fixed ring of particle systems, reused forever.
// WHAT & WHY: A drag emits particles many times per second. Instantiating a
//   ParticleSystem inside that loop is the classic mobile stutter: a GameObject
//   allocation plus a component wake-up, every few frames, straight into the GC.
//   This creates the systems once and cycles through them instead.
// KEY DECISIONS:
//   - A ring buffer, not a rent/return pool. Particle systems do not need to be
//     handed back: they simply finish playing. A ring means EmitAt is a array
//     index and a modulo, with nothing to leak and no free-list to corrupt.
//   - The transform is moved and Emit(count) is called, rather than using
//     EmitParams. EmitParams positions are expressed in the system simulation
//     space, which is a footgun; moving the object works identically whichever
//     space the prefab was authored in, as long as Simulation Space is World.
//   - Sorting layer and order are applied to every instance in Awake. The Canvas
//     is Screen Space - Camera, so a particle system with default sorting lands
//     behind the whole UI and is invisible. This is the single most common way
//     for UI particles to "not work".
//   - Configure() exists so ToolFxController can build one pool per tool from
//     ToolData.fxPrefab at runtime. The serialised fields cover the case where
//     the human drops a pool into the scene by hand.
//   - Size defaults to 4. Each system has its own Max Particles budget, so four
//     overlapping bursts is plenty for a single dragging finger.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// You do NOT normally add this component by hand — ToolFxController creates one
// pool per tool at runtime. What you DO have to author is the six particle
// prefabs it pools. Build each one like this:
//
// A) MAKE ONE PARTICLE PREFAB PER TOOL (repeat six times)
// [x] GameObject -> Effects -> Particle System.
// [x] Rename it to one of: FxDustRemover, FxWaterSpray, FxDeacidifier,
//     FxSqueegee, FxRoller, FxPencil.
// [x] In the Particle System module (the top one), set:
//       Duration = 1, Looping = OFF, Start Lifetime = 0.5,
//       Start Speed = 1.5, Start Size = 12, Gravity Modifier = 0.2,
//       Simulation Space = WORLD  (critical — otherwise particles follow the
//         finger instead of being left behind), Play On Awake = OFF,
//       Max Particles = 60.
// [x] Start Color — click the colour swatch and set RGB per tool:
//       FxDustRemover   warm brown        R 138  G  99  B  62   A 255
//       FxWaterSpray    blue              R  74  G 144  B 226   A 220
//       FxDeacidifier   white             R 245  G 245  B 245   A 200
//       FxSqueegee      brown-yellow      R 176  G 141  B  61   A 230
//       FxRoller        glossy off-white  R 238  G 233  B 220   A 235
//       FxPencil        grey graphite     R  92  G  92  B  96   A 240
// [x] Emission module: UNTICK it (set Rate over Time = 0 if you prefer). The
//     pool emits explicitly; a rate would spray constantly.
// [x] Shape module: Shape = Sphere, Radius = 6.
// [x] Renderer module (scroll to the bottom):
//       Render Mode = Billboard,
//       Material = Default-Particle
//         (click the circle -> switch the picker to the "All" tab ->
//          type "Default-Particle" -> pick the built-in one.
//          Do NOT create a new material and do NOT look for a texture file;
//          Default-Particle ships with Unity.)
//       Sorting Layer ID = Default, Order in Layer = 1.
// [x] Drag the finished object from the Hierarchy into Assets/Prefabs to make
//     it a prefab, then DELETE it from the Hierarchy.
//
// B) SORTING (why Order in Layer = 1)
// [x] The Canvas renders at Sorting Layer Default, Order in Layer 0.
// [x] Each screen ToolBarRoot has its own Canvas with Override Sorting ticked
//     and Order in Layer = 2 (see CleaningScreen.cs / LinenBackingScreen.cs).
// [x] Order 1 therefore sits ABOVE the poster and BELOW the tool bar, which is
//     exactly where the tool particles belong. If particles vanish, this number
//     is almost certainly the reason.
//
// C) IF YOU DO PLACE A POOL BY HAND
// [x] Create Empty under the screen -> Add Component -> Particle Burst Pool.
// [x] Drag one of the Fx prefabs into Prefab, leave Size at 4, leave
//     Sorting Layer Name at "Default" and Sorting Order at 1.
// ---------------------------------------------------------------

using UnityEngine;

namespace RestoriumEmporium.FX
{
    [DisallowMultipleComponent]
    public class ParticleBurstPool : MonoBehaviour
    {
        [Tooltip("Particle system to pool. One per tool; see the checklist above.")]
        [SerializeField] private ParticleSystem prefab;

        [Range(1, 16)]
        [Tooltip("How many copies to keep alive. Four covers one dragging finger.")]
        [SerializeField] private int size = 4;

        [Tooltip("Must match the Canvas sorting layer, normally \"Default\".")]
        [SerializeField] private string sortingLayerName = "Default";

        [Tooltip("1 puts particles above the poster and below the tool bar.")]
        [SerializeField] private int sortingOrder = 1;

        private ParticleSystem[] _instances;
        private int _next;

        /// <summary>True once the pool has systems to emit from.</summary>
        public bool IsReady => _instances != null && _instances.Length > 0;

        private void Awake()
        {
            Build();
        }

        /// <summary>
        /// Rebuilds the pool from a prefab supplied in code. Used by
        /// ToolFxController, which reads the prefab off each ToolData asset.
        /// </summary>
        public void Configure(ParticleSystem source, int poolSize, string layerName, int order)
        {
            prefab = source;
            size = Mathf.Clamp(poolSize, 1, 16);
            sortingLayerName = layerName;
            sortingOrder = order;

            Dispose();
            Build();
        }

        /// <summary>
        /// Moves the next system in the ring to <paramref name="worldPosition"/> and
        /// emits. Allocation-free; safe to call every frame of a drag.
        /// </summary>
        public void EmitAt(Vector3 worldPosition, int count)
        {
            if (!IsReady || count <= 0)
            {
                return;
            }

            ParticleSystem system = _instances[_next];
            _next++;

            if (_next >= _instances.Length)
            {
                _next = 0;
            }

            if (system == null)
            {
                return;
            }

            system.transform.position = worldPosition;
            system.Emit(count);
        }

        /// <summary>Stops every system and clears particles already in flight.</summary>
        public void StopAll()
        {
            if (_instances == null)
            {
                return;
            }

            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] != null)
                {
                    _instances[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void Build()
        {
            if (prefab == null)
            {
                _instances = null;
                return;
            }

            _instances = new ParticleSystem[Mathf.Max(1, size)];

            for (int i = 0; i < _instances.Length; i++)
            {
                ParticleSystem instance = Instantiate(prefab, transform);
                instance.transform.localPosition = Vector3.zero;

                ParticleSystem.MainModule main = instance.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                ParticleSystemRenderer renderer = instance.GetComponent<ParticleSystemRenderer>();

                if (renderer != null)
                {
                    // Without this the system renders behind the whole Screen Space -
                    // Camera canvas and looks like it never fired.
                    renderer.sortingLayerName = sortingLayerName;
                    renderer.sortingOrder = sortingOrder;
                }

                // Play() with no emission rate produces nothing on its own; it just
                // puts the system in a state where Emit() is honoured immediately.
                instance.Play();

                _instances[i] = instance;
            }

            _next = 0;
        }

        private void Dispose()
        {
            if (_instances == null)
            {
                return;
            }

            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] != null)
                {
                    Destroy(_instances[i].gameObject);
                }
            }

            _instances = null;
            _next = 0;
        }
    }
}
