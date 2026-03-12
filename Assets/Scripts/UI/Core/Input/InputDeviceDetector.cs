using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DIG.UI.Core.Input
{
    /// <summary>
    /// Detects the current input device and fires events on device change.
    /// Monitors Unity's Input System for device activity.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// 
    /// Usage:
    ///   InputDeviceDetector.OnDeviceChanged += type => UpdateUI(type);
    ///   var current = InputDeviceDetector.CurrentDevice;
    /// </summary>
    public static class InputDeviceDetector
    {
        private static InputDeviceType _currentDevice = InputDeviceType.KeyboardMouse;
        private static bool _initialized = false;
        
        /// <summary>
        /// Event fired when the active input device type changes.
        /// </summary>
        public static event Action<InputDeviceType> OnDeviceChanged;
        
        /// <summary>
        /// The currently active input device type.
        /// </summary>
        public static InputDeviceType CurrentDevice
        {
            get
            {
                EnsureInitialized();
                return _currentDevice;
            }
        }
        
        /// <summary>
        /// Whether a gamepad is currently the active device.
        /// </summary>
        public static bool IsGamepad => CurrentDevice != InputDeviceType.KeyboardMouse;
        
        /// <summary>
        /// Forces re-detection of the current device.
        /// </summary>
        public static void Refresh()
        {
            var newDevice = DetectCurrentDevice();
            if (newDevice != _currentDevice)
            {
                _currentDevice = newDevice;
                OnDeviceChanged?.Invoke(_currentDevice);
            }
        }
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            
            // Initial detection
            _currentDevice = DetectCurrentDevice();
            
            // Subscribe to input events
            InputSystem.onActionChange += OnActionChange;
        }
        
        private static void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed) return;
            
            if (obj is InputAction action && action.activeControl != null)
            {
                var device = action.activeControl.device;
                var newType = ClassifyDevice(device);
                
                if (newType != _currentDevice)
                {
                    _currentDevice = newType;
                    OnDeviceChanged?.Invoke(_currentDevice);
                }
            }
        }
        
        private static InputDeviceType DetectCurrentDevice()
        {
            // Check for connected gamepads first
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.wasUpdatedThisFrame)
            {
                return ClassifyDevice(gamepad);
            }
            
            // Default to keyboard/mouse
            return InputDeviceType.KeyboardMouse;
        }
        
        private static InputDeviceType ClassifyDevice(InputDevice device)
        {
            if (device == null)
                return InputDeviceType.KeyboardMouse;
                
            // Check device name/type for platform identification
            var name = device.name.ToLowerInvariant();
            var layout = device.layout?.ToLowerInvariant() ?? "";
            
            // PlayStation detection
            if (name.Contains("dualshock") || name.Contains("dualsense") ||
                layout.Contains("dualshock") || layout.Contains("dualsense") ||
                name.Contains("playstation") || name.Contains("ps4") || name.Contains("ps5"))
            {
                return InputDeviceType.PlayStation;
            }
            
            // Switch detection
            if (name.Contains("switch") || name.Contains("joy-con") || name.Contains("joycon") ||
                layout.Contains("switch") || name.Contains("nintendo"))
            {
                return InputDeviceType.Switch;
            }
            
            // Xbox / XInput detection (most common)
            if (name.Contains("xbox") || layout.Contains("xinput") ||
                layout.Contains("xbox") || device is Gamepad)
            {
                return InputDeviceType.Xbox;
            }
            
            // Keyboard/Mouse
            if (device is Keyboard || device is Mouse)
            {
                return InputDeviceType.KeyboardMouse;
            }
            
            // Unknown gamepad
            if (device is Gamepad)
            {
                return InputDeviceType.GenericGamepad;
            }
            
            return InputDeviceType.KeyboardMouse;
        }
        
        /// <summary>
        /// Call this to clean up when the application quits.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _initialized = false;
            _currentDevice = InputDeviceType.KeyboardMouse;
            OnDeviceChanged = null;
        }
    }
}
