using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;
using Player.Components;

namespace Player.UI
{
    /// <summary>
    /// Syncs ECS PlayerStamina to a shader-based stamina bar.
    /// Attach to the same GameObject with the stamina bar Image.
    /// </summary>
    public class ShaderStaminaBarSync : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image _barImage;

        [Header("Shader")]
        [SerializeField] private Shader _shader;

        [Header("Thresholds")]
        [SerializeField] private float _emptyThreshold = 0.05f;

        [Header("Display")]
        [Tooltip("When true, fill drains smoothly. When false, uses chunky segments.")]
        [SerializeField] private bool _smoothFill = true;

        private Material _materialInstance;
        private World _world;
        private EntityQuery _playerQuery;
        private Entity _cachedPlayer;
        private bool _initialized;
        private int _retryCount;
        private const int MaxRetries = 10;

        private float _lastStamina;
        private float _lastDrainTime;

        private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
        private static readonly int IsDrainingId = Shader.PropertyToID("_IsDraining");
        private static readonly int IsRecoveringId = Shader.PropertyToID("_IsRecovering");
        private static readonly int IsEmptyId = Shader.PropertyToID("_IsEmpty");
        private static readonly int SmoothFillId = Shader.PropertyToID("_SmoothFill");

        private const string ShaderName = "DIG/UI/ProceduralStaminaBar";

        private void Awake()
        {
            if (_barImage == null)
                _barImage = GetComponent<Image>();

            var shader = _shader != null ? _shader : Shader.Find(ShaderName);
            if (shader == null) return;

            _materialInstance = new Material(shader);
            _materialInstance.SetFloat(SmoothFillId, _smoothFill ? 1f : 0f);

            if (_barImage != null)
                _barImage.material = _materialInstance;
        }

        private void OnEnable()
        {
            _initialized = false;
            _retryCount = 0;
            _cachedPlayer = Entity.Null;
            _lastStamina = 1f;
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
                ComponentType.ReadOnly<PlayerStamina>(),
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
            if (_cachedPlayer == Entity.Null || !em.Exists(_cachedPlayer) || !em.HasComponent<PlayerStamina>(_cachedPlayer))
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

            var stamina = em.GetComponentData<PlayerStamina>(_cachedPlayer);

            float staminaPercent = stamina.Max > 0 ? stamina.Current / stamina.Max : 0f;

            bool isDraining = staminaPercent < _lastStamina - 0.001f;
            bool isRecovering = staminaPercent > _lastStamina + 0.001f;
            bool isEmpty = staminaPercent <= _emptyThreshold;

            if (isDraining)
                _lastDrainTime = Time.time;

            _lastStamina = staminaPercent;

            _materialInstance.SetFloat(FillAmountId, staminaPercent);
            _materialInstance.SetFloat(IsDrainingId, isDraining ? 1f : 0f);
            _materialInstance.SetFloat(IsRecoveringId, isRecovering ? 1f : 0f);
            _materialInstance.SetFloat(IsEmptyId, isEmpty ? 1f : 0f);
        }

        private void OnDisable()
        {
            _initialized = false;
            _cachedPlayer = Entity.Null;
        }
    }
}
