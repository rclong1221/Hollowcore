using UnityEngine;
using Unity.Entities;

namespace DIG.Items.Interfaces
{
    /// <summary>
    /// View mode enumeration for different camera/render contexts.
    /// </summary>
    public enum ViewMode
    {
        ThirdPerson = 0,
        FirstPerson = 1,
        FirstPersonFullBody = 2,
        VR = 3,
        Spectator = 4,
        UI = 5
    }

    /// <summary>
    /// Abstracts camera-mode-specific equipment rendering.
    /// Implement this interface to support different view modes
    /// (third-person, first-person, VR, etc.)
    /// </summary>
    public interface IViewModeHandler
    {
        /// <summary>
        /// Current view mode.
        /// </summary>
        ViewMode CurrentMode { get; }
        
        /// <summary>
        /// Called when the view mode changes.
        /// </summary>
        void OnViewModeChanged(ViewMode newMode);
        
        /// <summary>
        /// Render/show equipment in the specified slot.
        /// </summary>
        /// <param name="slotId">Slot identifier (e.g., "MainHand", "OffHand")</param>
        /// <param name="itemPrefab">The weapon/item prefab to instantiate</param>
        /// <returns>The instantiated GameObject, or null if not rendered in this mode</returns>
        GameObject RenderEquipment(string slotId, GameObject itemPrefab);
        
        /// <summary>
        /// Hide/destroy equipment in the specified slot.
        /// </summary>
        void HideEquipment(string slotId);
        
        /// <summary>
        /// Get the attachment point transform for a slot.
        /// </summary>
        Transform GetAttachPoint(string slotId);
        
        /// <summary>
        /// Check if this view mode supports rendering the specified slot.
        /// </summary>
        bool SupportsSlot(string slotId);
        
        /// <summary>
        /// Initialize the handler with the character root.
        /// </summary>
        void Initialize(Transform characterRoot);
    }
}
