using UnityEngine;
using Unity.Entities;
using DIG.Targeting;

namespace DIG.Core.Input
{
    /// <summary>
    /// Syncs targeting mode from the active paradigm profile to the local player's TargetData.
    /// Clears stale TargetEntity when switching away from ClickSelect/LockOn.
    ///
    /// EPIC 18.19 - Paradigm-Targeting Bridge
    /// </summary>
    public class TargetingConfigurable : MonoBehaviour, IParadigmConfigurable
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        public static TargetingConfigurable Instance { get; private set; }

        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Debug")]
        [SerializeField] private bool _logStateChanges = false;

        // ============================================================
        // RUNTIME STATE
        // ============================================================

        private TargetingMode _currentMode = TargetingMode.CameraRaycast;
        private World _cachedWorld;
        private EntityQuery _cachedQuery;

        // ============================================================
        // PUBLIC API
        // ============================================================

        public TargetingMode CurrentMode => _currentMode;

        // ============================================================
        // IParadigmConfigurable IMPLEMENTATION
        // ============================================================

        public int ConfigurationOrder => 250; // After FacingController(200)
        public string SubsystemName => "TargetingConfigurable";

        public bool CanConfigure(InputParadigmProfile profile, out string errorReason)
        {
            errorReason = null;
            return true;
        }

        public IConfigSnapshot CaptureSnapshot()
        {
            return new TargetingSnapshot
            {
                Mode = _currentMode,
            };
        }

        public void Configure(InputParadigmProfile profile)
        {
            var previousMode = _currentMode;
            _currentMode = profile.defaultTargetingMode;

            // Write TargetData.Mode on local player entity
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                ApplyToLocalPlayer(world, previousMode);
            }

            if (_logStateChanges)
            {
                Debug.Log($"[TargetingConfigurable] Configured: {previousMode} -> {_currentMode}");
            }
        }

        public void Rollback(IConfigSnapshot snapshot)
        {
            if (snapshot is TargetingSnapshot ts)
            {
                _currentMode = ts.Mode;

                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null && world.IsCreated)
                {
                    ApplyToLocalPlayer(world, TargetingMode.CameraRaycast); // previousMode doesn't matter for rollback
                }
            }
        }

        private class TargetingSnapshot : IConfigSnapshot
        {
            public TargetingMode Mode;
        }

        // ============================================================
        // TARGETING SYNC
        // ============================================================

        private EntityQuery GetOrCreateQuery(World world)
        {
            if (_cachedWorld != world || !_cachedWorld.IsCreated)
            {
                _cachedWorld = world;
                _cachedQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<TargetData>(),
                    ComponentType.ReadOnly<Unity.NetCode.GhostOwnerIsLocal>()
                );
            }
            return _cachedQuery;
        }

        private void ApplyToLocalPlayer(World world, TargetingMode previousMode)
        {
            var em = world.EntityManager;
            var query = GetOrCreateQuery(world);

            if (query.IsEmpty) return;

            var entity = query.GetSingletonEntity();
            var targetData = em.GetComponentData<TargetData>(entity);

            targetData.Mode = _currentMode;

            // Clear stale target when switching away from entity-based modes
            bool wasEntityBased = previousMode == TargetingMode.ClickSelect || previousMode == TargetingMode.LockOn;
            bool isEntityBased = _currentMode == TargetingMode.ClickSelect || _currentMode == TargetingMode.LockOn;
            if (wasEntityBased && !isEntityBased)
            {
                targetData.TargetEntity = Entity.Null;
                targetData.HasValidTarget = false;
            }

            em.SetComponentData(entity, targetData);
        }

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[TargetingConfigurable]");
            go.AddComponent<TargetingConfigurable>();
            Debug.Log("[TargetingConfigurable] Auto-initialized");
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ParadigmStateMachine.Instance?.RegisterConfigurable(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                ParadigmStateMachine.Instance?.UnregisterConfigurable(this);
            }
        }
    }
}
