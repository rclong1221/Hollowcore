using System.Collections.Generic;
using UnityEngine;

namespace DIG.UI.Core.Input
{
    /// <summary>
    /// ScriptableObject database containing input action to icon mappings.
    /// Create via: Assets > Create > DIG/UI/Input Glyph Database
    /// 
    /// EPIC 15.8: Input Glyph System
    /// 
    /// Usage:
    ///   1. Create database asset in project
    ///   2. Add entries for each action (Jump, Interact, Fire, etc.)
    ///   3. Assign icons for each platform
    ///   4. Reference in InputGlyphProvider
    /// </summary>
    [CreateAssetMenu(fileName = "InputGlyphDatabase", menuName = "DIG/UI/Input Glyph Database")]
    public class InputGlyphDatabase : ScriptableObject
    {
        [Header("Glyph Entries")]
        [Tooltip("List of action-to-icon mappings")]
        [SerializeField] private List<InputGlyphEntry> _entries = new();
        
        [Header("Fallback")]
        [Tooltip("Default icon when action not found")]
        [SerializeField] private Sprite _fallbackIcon;
        
        [Tooltip("Default text when action not found (e.g., '[?]')")]
        [SerializeField] private string _fallbackText = "[?]";
        
        // Runtime lookup cache
        private Dictionary<string, InputGlyphEntry> _lookup;
        
        /// <summary>
        /// Gets a glyph entry by action name (case-insensitive).
        /// Returns null if not found.
        /// </summary>
        public InputGlyphEntry GetEntry(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                return null;
                
            BuildLookupIfNeeded();
            
            _lookup.TryGetValue(actionName.ToLowerInvariant(), out var entry);
            return entry;
        }
        
        /// <summary>
        /// Gets the icon for an action and device type.
        /// Returns fallback icon if action not found.
        /// </summary>
        public Sprite GetIcon(string actionName, InputDeviceType deviceType)
        {
            var entry = GetEntry(actionName);
            if (entry != null)
            {
                return entry.GetIcon(deviceType);
            }
            return _fallbackIcon;
        }
        
        /// <summary>
        /// Gets the text for an action and device type.
        /// Returns fallback text if action not found.
        /// </summary>
        public string GetText(string actionName, InputDeviceType deviceType)
        {
            var entry = GetEntry(actionName);
            if (entry != null)
            {
                return entry.GetText(deviceType);
            }
            return _fallbackText;
        }
        
        /// <summary>
        /// Checks if an action exists in the database.
        /// </summary>
        public bool HasAction(string actionName)
        {
            return GetEntry(actionName) != null;
        }
        
        private void BuildLookupIfNeeded()
        {
            if (_lookup != null) return;
            
            _lookup = new Dictionary<string, InputGlyphEntry>();
            foreach (var entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.ActionName))
                {
                    _lookup[entry.ActionName.ToLowerInvariant()] = entry;
                }
            }
        }
        
        private void OnValidate()
        {
            // Rebuild cache when edited in inspector
            _lookup = null;
        }
        
        #if UNITY_EDITOR
        /// <summary>
        /// Editor helper to add common game actions.
        /// </summary>
        [ContextMenu("Add Common Actions")]
        private void AddCommonActions()
        {
            var commonActions = new[]
            {
                "Jump", "Interact", "Fire", "Aim", "Reload", "Dodge",
                "Sprint", "Crouch", "Prone", "Use", "Inventory", "Map",
                "Pause", "Back", "Confirm", "Cancel", "NextWeapon", "PrevWeapon"
            };
            
            foreach (var action in commonActions)
            {
                if (!HasAction(action))
                {
                    _entries.Add(new InputGlyphEntry { ActionName = action });
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif
    }
}
