using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using Visuals.Components;

namespace DIG.UI.ViewModels
{
    /// <summary>
    /// ECS System that pushes Flashlight data to the FlashlightViewModel.
    /// Runs on client side only to update UI.
    /// 
    /// EPIC 15.8: MVVM Architecture - ECS to ViewModel Bridge
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FlashlightViewModelSyncSystem : SystemBase
    {
        private FlashlightViewModel _viewModel;
        private float _lastBattery = -1f;
        private bool _lastIsOn = false;
        private bool _lastIsFlickering = false;
        
        /// <summary>
        /// Registers a FlashlightViewModel to receive updates.
        /// </summary>
        public void RegisterViewModel(FlashlightViewModel viewModel)
        {
            _viewModel = viewModel;
            _lastBattery = -1f; // Force update on next tick
        }
        
        /// <summary>
        /// Unregisters the current ViewModel.
        /// </summary>
        public void UnregisterViewModel()
        {
            _viewModel = null;
        }
        
        protected override void OnUpdate()
        {
            if (_viewModel == null || _viewModel.IsDisposed)
            {
                // Debug: Uncomment to see if ViewModel is registered
                // Debug.Log("[UI.MVVM] FlashlightSync: No ViewModel registered");
                return;
            }
            
            // Query for local player flashlight using GhostOwnerIsLocal pattern
            foreach (var (state, config, ghostOwnerIsLocal, entity) in 
                SystemAPI.Query<RefRO<FlashlightState>, RefRO<FlashlightConfig>, RefRO<GhostOwnerIsLocal>>()
                    .WithEntityAccess())
            {
                // Check if GhostOwnerIsLocal is enabled
                if (!SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(entity))
                    continue;
                
                float battery = config.ValueRO.BatteryCurrent;
                float maxBattery = config.ValueRO.BatteryMax;
                bool isOn = state.ValueRO.IsOn;
                bool isFlickering = state.ValueRO.IsFlickering;
                
                // Only update if values changed
                bool batteryChanged = !Mathf.Approximately(battery, _lastBattery);
                bool stateChanged = isOn != _lastIsOn || isFlickering != _lastIsFlickering;
                
                if (batteryChanged)
                {
                    _lastBattery = battery;
                    // Debug: Uncomment to verify data flow
                    // Debug.Log($"[UI.MVVM] FlashlightSync: Pushing battery {battery}/{maxBattery}");
                    _viewModel.SetBattery(battery, maxBattery);
                }
                
                if (stateChanged)
                {
                    _lastIsOn = isOn;
                    _lastIsFlickering = isFlickering;
                    // Debug: Uncomment to verify data flow
                    // Debug.Log($"[UI.MVVM] FlashlightSync: Pushing state On={isOn}, Flickering={isFlickering}");
                    _viewModel.SetState(isOn, isFlickering);
                }
                
                // Only process first local player found
                break;
            }
            
            // Debug: Uncomment to verify player entity is found
            // if (!foundPlayer) Debug.LogWarning("[UI.MVVM] FlashlightSync: No local player with FlashlightState + FlashlightConfig + GhostOwnerIsLocal found");
        }
    }
}
