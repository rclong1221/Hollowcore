using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for InteractionProgress - hold-to-interact.
    /// </summary>
    public class InteractionViewModel : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public float Current { get; private set; }
        public float Required { get; private set; }
        public float Percent { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsComplete { get; private set; }
        
        public event Action<InteractionViewModel> OnChanged;
        public event Action OnCompleted;
        
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private bool _wasComplete;
        
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
                ComponentType.ReadOnly<InteractionProgress>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<InteractionProgress>(entities[0]);
            Current = data.Current;
            Required = data.Required;
            Percent = data.Percent;
            IsActive = data.IsActive;
            IsComplete = data.IsComplete;
            
            if (IsComplete && !_wasComplete)
                OnCompleted?.Invoke();
            _wasComplete = IsComplete;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
