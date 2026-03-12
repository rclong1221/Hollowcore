using UnityEngine;
using DIG.UI.Core.MVVM;

namespace DIG.UI.ViewModels
{
    /// <summary>
    /// ViewModel for Flashlight HUD display.
    /// Exposes reactive properties for battery level and flashlight state.
    /// 
    /// EPIC 15.8: MVVM Architecture - FlashlightViewModel
    /// </summary>
    public class FlashlightViewModel : ECSViewModelBase
    {
        #region Battery Properties
        
        /// <summary>Current battery level (seconds remaining).</summary>
        public BindableProperty<float> BatteryCurrent { get; } = new(100f);
        
        /// <summary>Maximum battery capacity (seconds).</summary>
        public BindableProperty<float> BatteryMax { get; } = new(100f);
        
        /// <summary>Battery as percentage (0-1).</summary>
        public BindableProperty<float> BatteryPercent { get; } = new(1f);
        
        /// <summary>Whether battery is critically low (&lt;5%).</summary>
        public BindableProperty<bool> IsLowBattery { get; } = new(false);
        
        /// <summary>Whether battery is empty.</summary>
        public BindableProperty<bool> IsEmpty { get; } = new(false);
        
        #endregion
        
        #region State Properties
        
        /// <summary>Whether the flashlight is on.</summary>
        public BindableProperty<bool> IsOn { get; } = new(false);
        
        /// <summary>Whether the flashlight is flickering (low battery effect).</summary>
        public BindableProperty<bool> IsFlickering { get; } = new(false);
        
        /// <summary>Status text: "ON", "OFF", or "EMPTY".</summary>
        public BindableProperty<string> StatusText { get; } = new("OFF");
        
        #endregion
        
        #region Configuration
        
        /// <summary>Battery percentage below which is considered low.</summary>
        public float LowBatteryThreshold { get; set; } = 0.05f;
        
        #endregion
        
        public FlashlightViewModel()
        {
            // Auto-calculate derived values
            BatteryCurrent.OnChanged += OnBatteryChanged;
            BatteryMax.OnChanged += OnBatteryChanged;
            IsOn.OnChanged += OnStateChanged;
        }
        
        private void OnBatteryChanged(float _)
        {
            float max = BatteryMax.Value;
            if (max <= 0f)
            {
                BatteryPercent.Value = 0f;
                IsLowBattery.Value = true;
                IsEmpty.Value = true;
                UpdateStatusText();
                return;
            }
            
            float percent = Mathf.Clamp01(BatteryCurrent.Value / max);
            BatteryPercent.Value = percent;
            IsLowBattery.Value = percent <= LowBatteryThreshold;
            IsEmpty.Value = BatteryCurrent.Value <= 0f;
            UpdateStatusText();
        }
        
        private void OnStateChanged(bool _)
        {
            UpdateStatusText();
        }
        
        private void UpdateStatusText()
        {
            if (IsEmpty.Value)
            {
                StatusText.Value = "EMPTY";
            }
            else if (IsOn.Value)
            {
                StatusText.Value = "ON";
            }
            else
            {
                StatusText.Value = "OFF";
            }
        }
        
        /// <summary>
        /// Updates all battery values at once (batch update from ECS).
        /// </summary>
        public void SetBattery(float current, float max)
        {
            BatteryMax.SetSilent(max);
            BatteryCurrent.Value = current; // Triggers recalculation
        }
        
        /// <summary>
        /// Updates flashlight state from ECS.
        /// </summary>
        public void SetState(bool isOn, bool isFlickering)
        {
            IsFlickering.SetSilent(isFlickering);
            IsOn.Value = isOn; // Triggers status text update
        }
        
        /// <summary>
        /// Gets a USS class name based on current state.
        /// </summary>
        public string GetBatteryBarClass()
        {
            if (IsEmpty.Value) return "battery-bar__fill--empty";
            if (IsLowBattery.Value) return "battery-bar__fill--low";
            if (IsOn.Value) return "battery-bar__fill--active";
            return "battery-bar__fill--off";
        }
        
        protected override void OnDispose()
        {
            BatteryCurrent.ClearListeners();
            BatteryMax.ClearListeners();
            BatteryPercent.ClearListeners();
            IsLowBattery.ClearListeners();
            IsEmpty.ClearListeners();
            IsOn.ClearListeners();
            IsFlickering.ClearListeners();
            StatusText.ClearListeners();
        }
    }
}
