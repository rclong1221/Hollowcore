namespace DIG.UI.Core.Input
{
    /// <summary>
    /// Represents the type of input device currently active.
    /// Used by InputGlyphProvider to select appropriate icons.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// </summary>
    public enum InputDeviceType
    {
        /// <summary>Keyboard and Mouse</summary>
        KeyboardMouse,
        
        /// <summary>Xbox controller (also generic XInput)</summary>
        Xbox,
        
        /// <summary>PlayStation controller (DualShock/DualSense)</summary>
        PlayStation,
        
        /// <summary>Nintendo Switch controller (Pro Controller, Joy-Cons)</summary>
        Switch,
        
        /// <summary>Generic/Unknown gamepad</summary>
        GenericGamepad
    }
}
