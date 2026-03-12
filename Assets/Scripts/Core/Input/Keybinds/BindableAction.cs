using System;

namespace DIG.Core.Input.Keybinds
{
    /// <summary>
    /// Data model representing an action that can be rebound by the player.
    /// Used by KeybindService and UI components.
    /// </summary>
    [Serializable]
    public class BindableAction
    {
        /// <summary>
        /// The action name as defined in the Input Actions asset (e.g., "Jump", "Attack").
        /// </summary>
        public string ActionName;
        
        /// <summary>
        /// Human-readable display name for UI (e.g., "Jump", "Primary Attack").
        /// </summary>
        public string DisplayName;
        
        /// <summary>
        /// Category for grouping in UI (e.g., "Movement", "Combat", "Interaction").
        /// </summary>
        public string Category;
        
        /// <summary>
        /// The action map this action belongs to (e.g., "Core", "Combat_Shooter").
        /// </summary>
        public string ActionMap;
        
        /// <summary>
        /// Which paradigms use this action. Empty means all paradigms.
        /// </summary>
        public InputParadigm[] Paradigms;
        
        /// <summary>
        /// Zero-based binding index within the action (0 = primary, 1 = secondary, etc.).
        /// </summary>
        public int BindingIndex;
        
        public BindableAction() { }
        
        public BindableAction(string actionName, string displayName, string category, string actionMap, int bindingIndex = 0, params InputParadigm[] paradigms)
        {
            ActionName = actionName;
            DisplayName = displayName;
            Category = category;
            ActionMap = actionMap;
            BindingIndex = bindingIndex;
            Paradigms = paradigms ?? Array.Empty<InputParadigm>();
        }
        
        /// <summary>
        /// Returns true if this action is available in the specified paradigm.
        /// </summary>
        public bool IsAvailableInParadigm(InputParadigm paradigm)
        {
            // Empty means available in all paradigms
            if (Paradigms == null || Paradigms.Length == 0)
                return true;
                
            foreach (var p in Paradigms)
            {
                if (p == paradigm)
                    return true;
            }
            return false;
        }
    }
}
