using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel that reads PlayerStamina from ECS and exposes UI-friendly properties.
    /// Decoupled from any specific UI implementation - can drive shaders, UI Toolkit, IMGUI, etc.
    /// </summary>
    public class StaminaViewModel : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] private float _lowThreshold = 0.3f;
        [SerializeField] private float _emptyThreshold = 0.05f;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        // === Public Properties (UI-agnostic) ===
        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Percent { get; private set; }
        public bool IsDraining { get; private set; }
        public bool IsRecovering { get; private set; }
        public bool IsLow { get; private set; }
        public bool IsEmpty { get; private set; }
        
        // === Events for reactive UI ===
        public event Action<StaminaViewModel> OnChanged;
        
        // === Private State ===
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private int _retryCount;
        private const int MaxRetries = 10;
        private float _lastPercent;
        
        private void OnEnable()
        {
            _initialized = false;
            _retryCount = 0;
            _lastPercent = 1f;
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
                    Debug.LogWarning("[StaminaVM] No client world found after retries");
                return;
            }
            
            var em = _world.EntityManager;
            _playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerStamina>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>()
            );
            
            _initialized = true;
            if (_showDebugLogs)
                Debug.Log("[StaminaVM] Initialized");
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
                
                var stamina = em.GetComponentData<PlayerStamina>(entities[0]);
                
                Current = stamina.Current;
                Max = stamina.Max;
                Percent = Max > 0 ? Current / Max : 0f;
                
                // Detect state changes
                IsDraining = Percent < _lastPercent - 0.001f;
                IsRecovering = Percent > _lastPercent + 0.001f;
                IsLow = Percent <= _lowThreshold;
                IsEmpty = Percent <= _emptyThreshold;
                
                _lastPercent = Percent;
                
                // Notify listeners
                OnChanged?.Invoke(this);
                
                if (_showDebugLogs && Time.frameCount % 60 == 0)
                    Debug.Log($"[StaminaVM] {Current:F1}/{Max:F1} ({Percent:P0}) Drain:{IsDraining} Recover:{IsRecovering}");
            }
        }
        
        private void OnDisable()
        {
            _initialized = false;
        }
    }
}
