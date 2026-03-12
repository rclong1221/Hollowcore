using System;
using UnityEngine;

namespace DIG.UI.Core.Input
{
    /// <summary>
    /// Service for retrieving input glyphs based on current device.
    /// Provides icons and text for input prompts.
    /// 
    /// EPIC 15.8: Input Glyph System
    /// 
    /// Usage:
    ///   // Get icon for current device
    ///   var icon = InputGlyphProvider.GetIcon("Interact");
    ///   
    ///   // Get formatted text
    ///   var text = InputGlyphProvider.GetText("Jump"); // Returns "[Space]" or "(A)"
    ///   
    ///   // Process rich text
    ///   var result = InputGlyphProvider.ProcessText("Press <Action:Interact> to pick up");
    ///   // Returns: "Press [F] to pick up" or "Press (X) to pick up"
    /// </summary>
    public static class InputGlyphProvider
    {
        private static InputGlyphDatabase _database;
        private static bool _initialized = false;
        
        /// <summary>
        /// Event fired when the input device changes (for UI refresh).
        /// </summary>
        public static event Action<InputDeviceType> OnDeviceChanged
        {
            add => InputDeviceDetector.OnDeviceChanged += value;
            remove => InputDeviceDetector.OnDeviceChanged -= value;
        }
        
        /// <summary>
        /// The currently active input device type.
        /// </summary>
        public static InputDeviceType CurrentDevice => InputDeviceDetector.CurrentDevice;
        
        /// <summary>
        /// Initializes the provider with a glyph database.
        /// Call this at game startup.
        /// </summary>
        public static void Initialize(InputGlyphDatabase database)
        {
            _database = database;
            _initialized = true;
        }
        
        /// <summary>
        /// Gets the icon for an action using the current device type.
        /// </summary>
        public static Sprite GetIcon(string actionName)
        {
            EnsureInitialized();
            return _database?.GetIcon(actionName, CurrentDevice);
        }
        
        /// <summary>
        /// Gets the icon for an action for a specific device type.
        /// </summary>
        public static Sprite GetIcon(string actionName, InputDeviceType deviceType)
        {
            EnsureInitialized();
            return _database?.GetIcon(actionName, deviceType);
        }
        
        /// <summary>
        /// Gets the text representation for an action using the current device type.
        /// </summary>
        public static string GetText(string actionName)
        {
            EnsureInitialized();
            return _database?.GetText(actionName, CurrentDevice) ?? $"[{actionName}]";
        }
        
        /// <summary>
        /// Gets the text representation for an action for a specific device type.
        /// </summary>
        public static string GetText(string actionName, InputDeviceType deviceType)
        {
            EnsureInitialized();
            return _database?.GetText(actionName, deviceType) ?? $"[{actionName}]";
        }
        
        /// <summary>
        /// Processes a string, replacing &lt;Action:Name&gt; tags with appropriate text.
        /// 
        /// Example:
        ///   Input: "Press &lt;Action:Interact&gt; to open"
        ///   Output: "Press [F] to open" (keyboard) or "Press (X) to open" (PlayStation)
        /// </summary>
        public static string ProcessText(string input)
        {
            return ProcessText(input, CurrentDevice);
        }
        
        /// <summary>
        /// Processes a string for a specific device type.
        /// </summary>
        public static string ProcessText(string input, InputDeviceType deviceType)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            EnsureInitialized();
            
            return RichTextTagProcessor.Process(input, deviceType, _database);
        }
        
        /// <summary>
        /// Checks if an action exists in the database.
        /// </summary>
        public static bool HasAction(string actionName)
        {
            EnsureInitialized();
            return _database?.HasAction(actionName) ?? false;
        }
        
        private static void EnsureInitialized()
        {
            if (_initialized) return;
            
            // Try to load default database from Resources
            _database = Resources.Load<InputGlyphDatabase>("InputGlyphDatabase");
            if (_database != null)
            {
                _initialized = true;
            }
            else
            {
                Debug.LogWarning("[InputGlyphProvider] No InputGlyphDatabase found. " +
                    "Create one at Resources/InputGlyphDatabase or call Initialize() manually.");
            }
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _database = null;
            _initialized = false;
        }
    }
}
