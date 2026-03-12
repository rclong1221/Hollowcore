using UnityEngine;
using UnityEngine.UIElements;
using DIG.UI.Core.MVVM;
using DIG.UI.ViewModels;
using System.Collections.Generic;

namespace DIG.UI.Views
{
    /// <summary>
    /// Flashlight HUD View using UI Toolkit and MVVM pattern - AAA Edition.
    /// Displays battery level with segmented cells and status indicator.
    /// 
    /// EPIC 15.8: MVVM Architecture - Premium Flashlight HUD
    /// 
    /// Features:
    /// - Segmented battery cells (like real battery indicator)
    /// - Status indicator dot with glow
    /// - Color transitions: Cyan → Orange → Red
    /// - Flicker effect at low battery
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FlashlightHUDView : UIView<FlashlightViewModel>
    {
        [Header("HUD Configuration")]
        [SerializeField] private bool _autoCreateViewModel = true;
        [SerializeField] private bool _hideWhenOff = false;
        [SerializeField] private int _cellCount = 10;
        
        [Header("Flicker Settings")]
        [SerializeField] private float _flickerSpeed = 15f;
        [SerializeField] private float _flickerIntensity = 0.7f;
        
        private VisualElement _container;
        private VisualElement _panel;
        private VisualElement _statusIndicator;
        private Label _statusText;
        private Label _batteryPercentText;
        private List<VisualElement> _batteryCells = new List<VisualElement>();
        
        private bool _pendingRegistration = false;
        private FlashlightViewModel _pendingViewModel;
        private bool _isFlickering = false;
        private float _flickerTime = 0f;
        
        protected override void Start()
        {
            base.Start();
            
            if (_autoCreateViewModel && !IsBound)
            {
                var viewModel = new FlashlightViewModel();
                Bind(viewModel);
                
                // Register with ECS sync system (may need retry if world not ready)
                if (!TryRegisterWithECS(viewModel))
                {
                    _pendingRegistration = true;
                    _pendingViewModel = viewModel;
                    Debug.LogWarning("[UI.MVVM] FlashlightHUD: ECS World not ready, will retry registration...");
                }
            }
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Retry ECS registration if pending
            if (_pendingRegistration && _pendingViewModel != null)
            {
                if (TryRegisterWithECS(_pendingViewModel))
                {
                    _pendingRegistration = false;
                    _pendingViewModel = null;
                    Debug.Log("[UI.MVVM] FlashlightHUD: Successfully registered with ECS sync system");
                }
            }
            
            // Flicker animation when low battery
            if (_isFlickering)
            {
                UpdateFlickerAnimation();
            }
        }
        
        private bool TryRegisterWithECS(FlashlightViewModel viewModel)
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                return false;
            }
            
            var syncSystem = world.GetExistingSystemManaged<FlashlightViewModelSyncSystem>();
            if (syncSystem == null)
            {
                Debug.LogWarning("[UI.MVVM] FlashlightHUD: FlashlightViewModelSyncSystem not found in world");
                return false;
            }
            
            syncSystem.RegisterViewModel(viewModel);
            viewModel.SetWorld(world);
            Debug.Log("[UI.MVVM] FlashlightHUD: Registered ViewModel with sync system");
            return true;
        }
        
        protected override void OnBind()
        {
            // Query UI elements (AAA template)
            _container = Q("flashlight-container");
            _panel = Q("flashlight-panel");
            _statusIndicator = Q("status-indicator");
            _statusText = Q<Label>("status-text");
            _batteryPercentText = Q<Label>("battery-percent-text");
            
            // Query battery cells
            _batteryCells.Clear();
            for (int i = 0; i < _cellCount; i++)
            {
                var cell = Q($"cell-{i}");
                if (cell != null)
                    _batteryCells.Add(cell);
            }
            
            // Bind battery level
            ViewModel.BatteryPercent.OnChanged += UpdateBatteryCells;
            UpdateBatteryCells(ViewModel.BatteryPercent.Value);
            
            // Bind battery percent text
            if (_batteryPercentText != null)
            {
                ViewModel.BatteryPercent.OnChanged += UpdateBatteryPercentText;
                UpdateBatteryPercentText(ViewModel.BatteryPercent.Value);
            }
            
            // Bind status indicator
            ViewModel.IsOn.OnChanged += _ => UpdateStatusIndicator();
            ViewModel.IsLowBattery.OnChanged += _ => UpdateStatusIndicator();
            ViewModel.IsEmpty.OnChanged += _ => UpdateStatusIndicator();
            UpdateStatusIndicator();
            
            // Bind status text
            if (_statusText != null)
            {
                ViewModel.StatusText.OnChanged += text => _statusText.text = text;
                ViewModel.IsOn.OnChanged += _ => UpdateStatusTextColor();
                ViewModel.IsEmpty.OnChanged += _ => UpdateStatusTextColor();
                ViewModel.IsLowBattery.OnChanged += _ => UpdateStatusTextColor();
                _statusText.text = ViewModel.StatusText.Value;
                UpdateStatusTextColor();
            }
            
            // Bind flicker state
            ViewModel.IsFlickering.OnChanged += OnFlickerChanged;
            OnFlickerChanged(ViewModel.IsFlickering.Value);
            
            // Bind container visibility and dim state
            if (_panel != null)
            {
                ViewModel.IsOn.OnChanged += UpdatePanelState;
                UpdatePanelState(ViewModel.IsOn.Value);
            }
            
            if (_container != null && _hideWhenOff)
            {
                ViewModel.IsOn.OnChanged += isOn =>
                {
                    _container.style.display = isOn ? DisplayStyle.Flex : DisplayStyle.None;
                };
            }
        }
        
        protected override void OnUnbind()
        {
            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var syncSystem = world.GetExistingSystemManaged<FlashlightViewModelSyncSystem>();
                syncSystem?.UnregisterViewModel();
            }
            
            _batteryCells.Clear();
            _isFlickering = false;
        }
        
        #region Battery Cells
        
        private void UpdateBatteryCells(float percent)
        {
            int filledCells = Mathf.CeilToInt(percent * _batteryCells.Count);
            bool isLow = ViewModel.IsLowBattery.Value;
            bool isEmpty = ViewModel.IsEmpty.Value;
            bool isOn = ViewModel.IsOn.Value;
            
            for (int i = 0; i < _batteryCells.Count; i++)
            {
                var cell = _batteryCells[i];
                
                // Remove all state classes
                cell.RemoveFromClassList("battery-cell--filled");
                cell.RemoveFromClassList("battery-cell--filled-off");
                cell.RemoveFromClassList("battery-cell--low");
                cell.RemoveFromClassList("battery-cell--empty");
                
                if (i < filledCells)
                {
                    // This cell should be filled
                    if (isEmpty)
                        cell.AddToClassList("battery-cell--empty");
                    else if (isLow)
                        cell.AddToClassList("battery-cell--low");
                    else if (isOn)
                        cell.AddToClassList("battery-cell--filled");
                    else
                        cell.AddToClassList("battery-cell--filled-off");
                }
                else
                {
                    // Empty cell
                    cell.AddToClassList("battery-cell--empty");
                }
            }
        }
        
        private void UpdateBatteryPercentText(float percent)
        {
            if (_batteryPercentText == null) return;
            _batteryPercentText.text = $"{Mathf.RoundToInt(percent * 100)}%";
        }
        
        #endregion
        
        #region Status Indicator
        
        private void UpdateStatusIndicator()
        {
            if (_statusIndicator == null) return;
            
            _statusIndicator.RemoveFromClassList("status-indicator--on");
            _statusIndicator.RemoveFromClassList("status-indicator--off");
            _statusIndicator.RemoveFromClassList("status-indicator--low");
            _statusIndicator.RemoveFromClassList("status-indicator--empty");
            
            if (ViewModel.IsEmpty.Value)
                _statusIndicator.AddToClassList("status-indicator--empty");
            else if (ViewModel.IsLowBattery.Value)
                _statusIndicator.AddToClassList("status-indicator--low");
            else if (ViewModel.IsOn.Value)
                _statusIndicator.AddToClassList("status-indicator--on");
            else
                _statusIndicator.AddToClassList("status-indicator--off");
        }
        
        private void UpdateStatusTextColor()
        {
            if (_statusText == null) return;
            
            Color color;
            if (ViewModel.IsEmpty.Value)
                color = new Color(1f, 0.2f, 0.2f);
            else if (ViewModel.IsLowBattery.Value)
                color = new Color(1f, 0.6f, 0.2f);
            else if (ViewModel.IsOn.Value)
                color = new Color(0.2f, 0.8f, 1f);
            else
                color = new Color(0.4f, 0.4f, 0.4f);
            
            _statusText.style.color = color;
        }
        
        private void UpdatePanelState(bool isOn)
        {
            if (_panel == null) return;
            
            if (isOn)
                _panel.RemoveFromClassList("battery-bar-segmented--off");
            else
                _panel.AddToClassList("battery-bar-segmented--off");
        }
        
        #endregion
        
        #region Flicker Effect
        
        private void OnFlickerChanged(bool isFlickering)
        {
            _isFlickering = isFlickering;
            _flickerTime = 0f;
            
            // Reset cell opacity when not flickering
            if (!isFlickering)
            {
                foreach (var cell in _batteryCells)
                {
                    cell.RemoveFromClassList("battery-cell--flickering");
                    cell.style.opacity = 1f;
                }
            }
        }
        
        private void UpdateFlickerAnimation()
        {
            _flickerTime += Time.deltaTime * _flickerSpeed;
            
            // Random-ish flicker pattern using sin waves at different frequencies
            float flicker1 = Mathf.Sin(_flickerTime * 7.3f);
            float flicker2 = Mathf.Sin(_flickerTime * 13.7f);
            float flicker3 = Mathf.Sin(_flickerTime * 23.1f);
            
            float combined = (flicker1 + flicker2 * 0.5f + flicker3 * 0.25f) / 1.75f;
            float opacity = Mathf.Lerp(_flickerIntensity, 1f, (combined + 1f) * 0.5f);
            
            // Apply flicker to filled cells
            foreach (var cell in _batteryCells)
            {
                if (cell.ClassListContains("battery-cell--low") || 
                    cell.ClassListContains("battery-cell--filled"))
                {
                    cell.style.opacity = opacity;
                }
            }
            
            // Also flicker the status indicator
            if (_statusIndicator != null)
            {
                _statusIndicator.style.opacity = opacity;
            }
        }
        
        #endregion
    }
}