using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for PlayerOxygen - breath/oxygen bar.
    /// </summary>
    public class OxygenViewModel : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] private float _lowThreshold = 0.3f;
        
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Percent { get; private set; }
        public bool IsDraining { get; private set; }
        public bool IsRecovering { get; private set; }
        public bool IsLow { get; private set; }
        public bool IsSuffocating { get; private set; }
        
        public event Action<OxygenViewModel> OnChanged;
        
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private float _lastPercent;
        
        private void Update()
        {
            if (!_initialized) { TryInitialize(); return; }
            ReadFromECS();
        }
        
        private void TryInitialize()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient() && world.IsCreated) { _world = world; break; }
            }
            if (_world == null) return;
            
            _playerQuery = _world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerOxygen>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<PlayerOxygen>(entities[0]);
            Current = data.Current;
            Max = data.Max;
            Percent = data.Percent;
            IsDraining = Percent < _lastPercent - 0.001f;
            IsRecovering = Percent > _lastPercent + 0.001f;
            IsLow = Percent <= _lowThreshold;
            IsSuffocating = data.IsSuffocating;
            _lastPercent = Percent;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
