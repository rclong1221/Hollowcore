using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Player;

namespace Player.UI.ViewModels
{
    /// <summary>
    /// ViewModel for AbilityCharges - discrete charge display.
    /// </summary>
    public class AbilityChargesViewModel : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs;
        
        public int CurrentCharges { get; private set; }
        public int MaxCharges { get; private set; }
        public float RechargeProgress { get; private set; }
        public float Percent { get; private set; }
        public bool HasCharges { get; private set; }
        public bool IsFull { get; private set; }
        public bool IsRecharging { get; private set; }
        
        public event Action<AbilityChargesViewModel> OnChanged;
        public event Action OnChargeGained;
        public event Action OnChargeUsed;
        
        private World _world;
        private EntityQuery _playerQuery;
        private bool _initialized;
        private int _lastCharges;
        
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
                ComponentType.ReadOnly<AbilityCharges>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            _initialized = true;
            _lastCharges = -1;
        }
        
        private void ReadFromECS()
        {
            if (_world == null || !_world.IsCreated || _playerQuery.IsEmpty) return;
            
            using var entities = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            if (entities.Length == 0) return;
            
            var data = _world.EntityManager.GetComponentData<AbilityCharges>(entities[0]);
            CurrentCharges = data.CurrentCharges;
            MaxCharges = data.MaxCharges;
            RechargeProgress = data.RechargeProgress;
            Percent = data.Percent;
            HasCharges = data.HasCharges;
            IsFull = data.IsFull;
            IsRecharging = data.IsRecharging;
            
            if (_lastCharges >= 0)
            {
                if (CurrentCharges > _lastCharges) OnChargeGained?.Invoke();
                else if (CurrentCharges < _lastCharges) OnChargeUsed?.Invoke();
            }
            _lastCharges = CurrentCharges;
            
            OnChanged?.Invoke(this);
        }
        
        private void OnDisable() => _initialized = false;
    }
}
