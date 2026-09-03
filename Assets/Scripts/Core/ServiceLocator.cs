// ============================================================
// ServiceLocator — tiny registry for the three cross-scene services.
// WHAT & WHY: SaveManager, LocalizationService and AudioManager live on a
//   persistent "Systems" object and must be reachable from objects in scenes
//   that load later. This gives them one lookup point without every consumer
//   holding a hard reference to a concrete MonoBehaviour.
// KEY DECISIONS:
//   - Deliberately scoped to interfaces registered by the Systems prefab and
//     nothing else. Everything inside a single scene uses [SerializeField]
//     references instead, which stay visible and debuggable in the Inspector.
//     A service locator used for everything becomes a global-variable bag; used
//     for three process-lifetime singletons it is the least intrusive option.
//   - Get<T>() returns default instead of throwing, and TryGet<T>() exists, so
//     a scene opened directly in the Editor (no Systems object) degrades to
//     "no audio / untranslated keys" instead of a NullReferenceException storm.
//   - Clear() exists purely so EditMode tests start from a known state.
// ============================================================

// ---- UNITY EDITOR SETUP (required for this script to work) ----
// [ ] Nothing. This is a static class; there is no component to attach.
//     The services register themselves in their own Awake().
// ---------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace RestoriumEmporium.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        /// <summary>Registers (or replaces) the implementation for service type T.</summary>
        public static void Register<T>(T service) where T : class
        {
            if (service == null)
            {
                Services.Remove(typeof(T));
                return;
            }

            Services[typeof(T)] = service;
        }

        /// <summary>Unregisters T, but only if <paramref name="service"/> is the current holder.</summary>
        /// <remarks>
        /// The identity check matters on scene reloads: a duplicate Systems object
        /// destroys itself in Awake, and without this guard its OnDestroy would
        /// unregister the surviving instance.
        /// </remarks>
        public static void Unregister<T>(T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var existing) && ReferenceEquals(existing, service))
            {
                Services.Remove(typeof(T));
            }
        }

        /// <summary>Returns the registered service, or null when none is registered.</summary>
        public static T Get<T>() where T : class
        {
            return Services.TryGetValue(typeof(T), out var service) ? service as T : null;
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            service = Get<T>();
            return service != null;
        }

        /// <summary>Test-only reset. Not called by gameplay code.</summary>
        public static void Clear() => Services.Clear();
    }
}
