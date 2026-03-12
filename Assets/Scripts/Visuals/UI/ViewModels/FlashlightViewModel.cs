using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Visuals.Components;

namespace Visuals.UI.ViewModels
{
    /// <summary>
    /// ViewModel that reads FlashlightState/FlashlightConfig from ECS and exposes UI-friendly properties.
    /// Decoupled from any specific UI implementation - can drive shaders, UI Toolkit, IMGUI, etc.
    /// </summary>
    public class FlashlightViewModel : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] private float _lowBatteryThreshold = 0.2f;
        [SerializeField] private float _flickerThreshold = 0.1f;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        // === Public Properties (UI-agnostic) ===
        public float BatteryCurrent { get; private set; }
        public float BatteryMax { get; private set; }
        public float BatteryPercent { get; private set; }
        public bool IsOn { get; private set; }
        public bool IsFlickering { get; private set; }
        public bool IsLowBattery { get; private set; }
        public bool IsEmpty { get; private set; }
        
        // === Events for reactive UI ===
        public event Action<FlashlightViewModel> OnChanged;
        public event Action OnToggled;
        public event Action OnBatteryDepleted;
        
        // === Private State ===
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private int _retryCount;
        private const int MaxRetries = 10;
        private bool _wasOn;
        private bool _wasEmpty;
        
        private void OnEnable()
        {
            _initialized = false;
            _retryCount = 0;
        }
        
        private void Update()
        {
            if (!_initialized)
            {
                TryInitialize();
                return;
            }
            
            ReadFromECS();
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
                if (_retryCount >= MaxRetries && _showDebugLogs)
                    Debug.LogWarning("[FlashlightVM] No client world found after retries");
                return;
            }
            
            var em = _world.EntityManager;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<FlashlightState>(),
                ComponentType.ReadOnly<FlashlightConfig>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>()
            );
            
            _initialized = true;
            if (_showDebugLogs)
                Debug.Log("[FlashlightVM] Initialized");
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated)
            {
                _initialized = false;
                return;
            }
            
            if (_playerQuery.IsEmpty)
                return;
            
            var em = _world.EntityManager;
            using (var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                if (entities.Length == 0) return;
                
                var state = em.GetComponentData<FlashlightState>(entities[0]);
                var config = em.GetComponentData<FlashlightConfig>(entities[0]);
                
                BatteryCurrent = config.BatteryCurrent;
                BatteryMax = config.BatteryMax;
                BatteryPercent = BatteryMax > 0 ? BatteryCurrent / BatteryMax : 0f;
                
                IsOn = state.IsOn;
                IsFlickering = state.IsFlickering || BatteryPercent <= _flickerThreshold;
                IsLowBattery = BatteryPercent <= _lowBatteryThreshold;
                IsEmpty = BatteryPercent <= 0.01f;
                
                // Fire events
                if (IsOn != _wasOn)
                    OnToggled?.Invoke();
                if (IsEmpty && !_wasEmpty)
                    OnBatteryDepleted?.Invoke();
                
                _wasOn = IsOn;
                _wasEmpty = IsEmpty;
                
                // Notify listeners
                OnChanged?.Invoke(this);
                
                if (_showDebugLogs && Time.frameCount % 60 == 0)
                    Debug.Log($"[FlashlightVM] {BatteryCurrent:F1}/{BatteryMax:F1} ({BatteryPercent:P0}) On:{IsOn} Flicker:{IsFlickering}");
            }
        }
        
        private void OnDisable()
        {
            _initialized = false;
        }
    }
}
