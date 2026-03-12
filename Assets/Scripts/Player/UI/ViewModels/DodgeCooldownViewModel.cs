using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;
using Player.Components;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for DodgeState - dodge cooldown.
    /// </summary>
    public class DodgeCooldownViewModel : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public float CooldownRemaining { get; private set; }
        public float CooldownMax { get; private set; }
        public float ReadyPercent { get; private set; }  // 1 = ready, 0 = just used
        public bool IsReady { get; private set; }
        public bool IsDodging { get; private set; }
        public bool IsOnCooldown { get; private set; }
        
        public event Action<DodgeCooldownViewModel> OnChanged;
        public event Action OnBecameReady;
        
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private bool _wasReady;
        
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
                ComponentType.ReadOnly<DodgeState>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<DodgeState>(entities[0]);
            CooldownRemaining = data.CooldownRemaining;
            CooldownMax = data.DodgeCooldown;
            ReadyPercent = data.DodgeCooldown > 0 ? 1f - (data.CooldownRemaining / data.DodgeCooldown) : 1f;
            IsReady = data.CooldownRemaining <= 0 && !data.IsDodging;
            IsDodging = data.IsDodging;
            IsOnCooldown = data.CooldownRemaining > 0;
            
            if (IsReady && !_wasReady)
                OnBecameReady?.Invoke();
            _wasReady = IsReady;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
