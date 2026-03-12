using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;
using Player.Components;

namespace Combat.UI
{
    /// <summary>
    /// Syncs ECS Health to a shader-based health bar.
    /// Reads Player.Components.Health (ghost-replicated) from the local player entity.
    /// Attach to the same GameObject with the health bar Image.
    ///
    /// Entity selection strategy:
    /// 1. Client world: Uses GhostOwnerIsLocal to find the local player (ALWAYS, regardless of candidate count)
    /// 2. Server world (listen server fallback): Uses CommandTarget on the first NetworkId connection
    /// 3. Local/default world: Picks first entity with Health
    /// </summary>
    public class ShaderHealthBarSync : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _barImage;

        [Header("Shader")]
        [SerializeField] private Shader _shader;

        [Header("Thresholds")]
        [SerializeField] private float _criticalThreshold = 0.25f;

        private Material _materialInstance;
        private World _world;
        private EntityQuery _playerQuery;
        private Entity _cachedPlayer;
        private bool _initialized;
        private int _retryCount;
        private bool _isClientWorld;
        private int _emptyFrames;
        private const int ReinitAfterEmptyFrames = 60;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsCriticalId = Shader.PropertyToID("_IsCritical");

        private void Awake()
        {
            if (_barImage == null)
                _barImage = GetComponent<Image>();

            if (_barImage == null) return;

            var shader = _shader != null ? _shader : Shader.Find("DIG/UI/ProceduralHealthBar");
            if (shader == null) return;

            _materialInstance = new Material(shader);
            _materialInstance.name = "HealthBar_Instance";
            _barImage.material = _materialInstance;
        }

        private void OnEnable()
        {
            _initialized = false;
            _retryCount = 0;
            _cachedPlayer = Entity.Null;
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(_materialInstance);
                else
                    DestroyImmediate(_materialInstance);
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                TryInitialize();
                return;
            }

            SyncFromECS();
        }

        private void TryInitialize()
        {
            // Find the best world: prefer client world, fall back to server (listen server), then any world
            _world = null;
            World fallbackWorld = null;
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;
                if (world.IsClient())
                {
                    _world = world;
                    break;
                }
                if (world.IsServer())
                    fallbackWorld = world;
            }

            if (_world == null)
                _world = fallbackWorld;

            // Last resort: DefaultGameObjectInjectionWorld (covers LocalWorld)
            if (_world == null && World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
                _world = World.DefaultGameObjectInjectionWorld;

            if (_world == null)
            {
                _retryCount++;
                return;
            }

            var em = _world.EntityManager;
            _isClientWorld = _world.IsClient();

            if (_isClientWorld)
            {
                // Client world: Query Health only (PlayerTag is NOT ghost-replicated,
                // so remote player ghosts won't have it). Use GhostOwnerIsLocal to
                // identify the local player from among all Health entities.
                _playerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<GhostOwnerIsLocal>()
                );
            }
            else
            {
                // Server/local world: Query Health + PlayerTag (both present server-side)
                _playerQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<PlayerTag>()
                );
            }

            _initialized = true;
        }

        private void SyncFromECS()
        {
            if (_materialInstance == null) return;

            if (_world == null || !_world.IsCreated)
            {
                _initialized = false;
                return;
            }

            var em = _world.EntityManager;

            // Validate cached entity or find player
            if (_cachedPlayer == Entity.Null || !em.Exists(_cachedPlayer) || !em.HasComponent<Health>(_cachedPlayer))
            {
                _cachedPlayer = Entity.Null;

                if (_playerQuery.IsEmpty)
                {
                    _emptyFrames++;
                    if (_emptyFrames >= ReinitAfterEmptyFrames)
                    {
                        _initialized = false;
                        _emptyFrames = 0;
                    }
                    return;
                }
                _emptyFrames = 0;

                using (var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (entities.Length == 0) return;

                    if (_isClientWorld)
                    {
                        // Client world: GhostOwnerIsLocal is an enableable component.
                        // The query includes it but we must check it's actually enabled
                        // to find the local player (not a remote ghost).
                        for (int i = 0; i < entities.Length; i++)
                        {
                            if (em.IsComponentEnabled<GhostOwnerIsLocal>(entities[i]))
                            {
                                _cachedPlayer = entities[i];
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Server/local: pick first match (Health + PlayerTag)
                        _cachedPlayer = entities[0];
                    }

                    Debug.Log($"[HealthBarSync] Picked entity={_cachedPlayer.Index}:{_cachedPlayer.Version} " +
                        $"from world={_world.Name} (isClient={_isClientWorld}) " +
                        $"candidates={entities.Length} GO={gameObject.name}");
                }

                if (_cachedPlayer == Entity.Null) return;
            }

            var health = em.GetComponentData<Health>(_cachedPlayer);

            float healthPercent = health.Max > 0 ? health.Current / health.Max : 0f;
            bool isCritical = healthPercent <= _criticalThreshold;

            _materialInstance.SetFloat(FillAmountId, healthPercent);
            _materialInstance.SetFloat(IsCriticalId, isCritical ? 1f : 0f);
        }

        private void OnDisable()
        {
            _initialized = false;
        }
    }
}
