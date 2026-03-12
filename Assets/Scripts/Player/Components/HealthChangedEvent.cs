using Unity.Entities;

namespace Player.Components
{
    /// <summary>
    /// Event fired when Health changes. (13.16.11)
    /// Used for UI updates, feedback effects, etc.
    /// </summary>
    public struct HealthChangedEvent : IComponentData, IEnableableComponent
    {
        public float OldValue;
        public float NewValue;
        public float Delta; // New - Old
        public Entity Source; // Optional source of change
    }
    
    /// <summary>
    /// Tracks previous health to detect changes.
    /// </summary>
    public struct HealthStateTracker : IComponentData
    {
        public float PreviousHealth;
    }
}
