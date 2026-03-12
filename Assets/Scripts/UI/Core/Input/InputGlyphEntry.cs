using System;
using UnityEngine;

namespace DIG.UI.Core.Input
{
    /// <summary>
    /// Maps an input action to icons for different device types.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// </summary>
    [Serializable]
    public class InputGlyphEntry
    {
        [Tooltip("The action name (e.g., 'Jump', 'Interact', 'Fire'). Case-insensitive.")]
        public string ActionName;
        
        [Header("Keyboard/Mouse")]
        [Tooltip("Icon for keyboard/mouse (e.g., 'F' key, 'LMB')")]
        public Sprite KeyboardIcon;
        
        [Tooltip("Text fallback for keyboard (e.g., '[F]', '[LMB]')")]
        public string KeyboardText = "";
        
        [Header("Xbox Controller")]
        [Tooltip("Icon for Xbox controller (e.g., A button, RT)")]
        public Sprite XboxIcon;
        
        [Tooltip("Text fallback for Xbox (e.g., '(A)', '(RT)')")]
        public string XboxText = "";
        
        [Header("PlayStation Controller")]
        [Tooltip("Icon for PlayStation controller (e.g., X button, R2)")]
        public Sprite PlayStationIcon;
        
        [Tooltip("Text fallback for PlayStation (e.g., '(X)', '(R2)')")]
        public string PlayStationText = "";
        
        [Header("Nintendo Switch")]
        [Tooltip("Icon for Switch controller (e.g., B button, ZR)")]
        public Sprite SwitchIcon;
        
        [Tooltip("Text fallback for Switch (e.g., '(B)', '(ZR)')")]
        public string SwitchText = "";
        
        /// <summary>
        /// Gets the appropriate icon for the given device type.
        /// Falls back to keyboard icon if specific icon not set.
        /// </summary>
        public Sprite GetIcon(InputDeviceType deviceType)
        {
            return deviceType switch
            {
                InputDeviceType.KeyboardMouse => KeyboardIcon,
                InputDeviceType.Xbox => XboxIcon ?? KeyboardIcon,
                InputDeviceType.PlayStation => PlayStationIcon ?? KeyboardIcon,
                InputDeviceType.Switch => SwitchIcon ?? KeyboardIcon,
                InputDeviceType.GenericGamepad => XboxIcon ?? KeyboardIcon,
                _ => KeyboardIcon
            };
        }
        
        /// <summary>
        /// Gets the appropriate text for the given device type.
        /// Falls back to keyboard text if specific text not set.
        /// </summary>
        public string GetText(InputDeviceType deviceType)
        {
            return deviceType switch
            {
                InputDeviceType.KeyboardMouse => KeyboardText,
                InputDeviceType.Xbox => !string.IsNullOrEmpty(XboxText) ? XboxText : KeyboardText,
                InputDeviceType.PlayStation => !string.IsNullOrEmpty(PlayStationText) ? PlayStationText : KeyboardText,
                InputDeviceType.Switch => !string.IsNullOrEmpty(SwitchText) ? SwitchText : KeyboardText,
                InputDeviceType.GenericGamepad => !string.IsNullOrEmpty(XboxText) ? XboxText : KeyboardText,
                _ => KeyboardText
            };
        }
    }
}
