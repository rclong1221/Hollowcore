using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for PlayerHunger.
    /// </summary>
    public class HungerViewModel : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Percent { get; private set; }  // Hunger amount (0 = full, 1 = starving)
        public float SatietyPercent { get; private set; }  // Inverted for UI display
        public bool IsHungry { get; private set; }
        public bool IsStarving { get; private set; }
        
        public event Action<HungerViewModel> OnChanged;
        
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
                ComponentType.ReadOnly<PlayerHunger>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<PlayerHunger>(entities[0]);
            Current = data.Current;
            Max = data.Max;
            Percent = data.Percent;
            SatietyPercent = data.SatietyPercent;
            IsHungry = data.IsHungry;
            IsStarving = data.IsStarving;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
