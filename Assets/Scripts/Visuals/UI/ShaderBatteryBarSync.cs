using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;
using Visuals.Components;

namespace Visuals.UI
{
    /// <summary>
    /// Syncs ECS FlashlightState/FlashlightConfig to a shader-based battery bar.
    /// Attach to the same GameObject with the battery bar Image.
    /// </summary>
    public class ShaderBatteryBarSync : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _barImage;

        [Header("Shader")]
        [SerializeField] private Shader _shader;

        [Header("Fill Mode")]
        [Tooltip("Smooth = continuous drain, Chunky = fills/drains in discrete cells")]
        [SerializeField] private bool _smoothFill = true;

        [Header("Thresholds")]
        [SerializeField] private float _lowBatteryThreshold = 0.2f;
        [SerializeField] private float _flickerThreshold = 0.1f;

        private Material _materialInstance;
        private World _world;
        private EntityQuery _playerQuery;
        private Entity _cachedPlayer;
        private bool _initialized;
        private int _retryCount;
        private const int MaxRetries = 10;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsOnId = Shader.PropertyToID("_IsOn");
        private static readonly int IsFlickeringId = Shader.PropertyToID("_IsFlickering");
        private static readonly int IsLowBatteryId = Shader.PropertyToID("_IsLowBattery");
        private static readonly int SmoothFillId = Shader.PropertyToID("_SmoothFill");

        private void Awake()
        {
            if (_barImage == null)
                _barImage = GetComponent<Image>();

            if (_barImage == null) return;

            var shader = _shader != null ? _shader : Shader.Find("DIG/UI/ProceduralBatteryBar");
            if (shader == null) return;

            _materialInstance = new Material(shader);
            _materialInstance.name = "BatteryBar_Instance";
            _materialInstance.SetFloat(SmoothFillId, _smoothFill ? 1f : 0f);
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
            _world = null;
            foreach (var world in World.All)
            {
                if (world.IsClient() && world.IsCreated)
                {
                    _world = world;
                    break;
                }
            }

            if (_world == null)
            {
                _retryCount++;
                return;
            }

            var em = _world.EntityManager;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<FlashlightState>(),
                ComponentType.ReadOnly<FlashlightConfig>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>()
            );

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
            if (_cachedPlayer == Entity.Null || !em.Exists(_cachedPlayer) || !em.HasComponent<FlashlightState>(_cachedPlayer))
            {
                _cachedPlayer = Entity.Null;
                if (_playerQuery.IsEmpty) return;

                using (var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
                {
                    if (entities.Length == 0) return;
                    _cachedPlayer = entities[0];
                }

                if (_cachedPlayer == Entity.Null) return;
            }

            var state = em.GetComponentData<FlashlightState>(_cachedPlayer);
            var config = em.GetComponentData<FlashlightConfig>(_cachedPlayer);

            float batteryPercent = config.BatteryMax > 0 ? config.BatteryCurrent / config.BatteryMax : 0f;
            bool isLowBattery = batteryPercent <= _lowBatteryThreshold;
            bool isFlickering = batteryPercent <= _flickerThreshold;

            _materialInstance.SetFloat(FillAmountId, batteryPercent);
            _materialInstance.SetFloat(IsOnId, state.IsOn ? 1f : 0f);
            _materialInstance.SetFloat(IsFlickeringId, (state.IsFlickering || isFlickering) ? 1f : 0f);
            _materialInstance.SetFloat(IsLowBatteryId, isLowBattery ? 1f : 0f);
            _barImage.SetMaterialDirty();
        }

        private void OnDisable()
        {
            _initialized = false;
            _cachedPlayer = Entity.Null;
        }
    }
}
