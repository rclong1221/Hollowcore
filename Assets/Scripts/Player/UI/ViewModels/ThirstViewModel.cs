using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for PlayerThirst.
    /// </summary>
    public class ThirstViewModel : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Percent { get; private set; }  // Thirst amount (0 = hydrated, 1 = dehydrated)
        public float HydrationPercent { get; private set; }  // Inverted for UI display
        public bool IsThirsty { get; private set; }
        public bool IsDehydrated { get; private set; }
        
        public event Action<ThirstViewModel> OnChanged;
        
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        
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
                ComponentType.ReadOnly<PlayerThirst>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<PlayerThirst>(entities[0]);
            Current = data.Current;
            Max = data.Max;
            Percent = data.Percent;
            HydrationPercent = data.HydrationPercent;
            IsThirsty = data.IsThirsty;
            IsDehydrated = data.IsDehydrated;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
