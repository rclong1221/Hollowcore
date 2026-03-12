using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for WeaponDurability.
    /// </summary>
    public class WeaponDurabilityViewModel : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public float Current { get; private set; }
        public float Max { get; private set; }
        public float Percent { get; private set; }
        public bool IsLow { get; private set; }
        public bool IsBroken { get; private set; }
        public bool NeedsRepair { get; private set; }
        
        public event Action<WeaponDurabilityViewModel> OnChanged;
        public event Action OnBroke;
        
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private bool _wasBroken;
        
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
                ComponentType.ReadOnly<WeaponDurability>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<WeaponDurability>(entities[0]);
            Current = data.Current;
            Max = data.Max;
            Percent = data.Percent;
            IsLow = data.IsLow;
            IsBroken = data.IsBroken;
            NeedsRepair = data.NeedsRepair;
            
            if (IsBroken && !_wasBroken)
                OnBroke?.Invoke();
            _wasBroken = IsBroken;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
