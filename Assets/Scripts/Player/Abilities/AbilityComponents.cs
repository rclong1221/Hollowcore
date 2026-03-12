using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Combat.Resources;

namespace DIG.Player.Abilities
{
    /// <summary>
    /// Type of ability start trigger.
    /// </summary>
    public enum AbilityStartType : byte
    {
        Manual,           // Started via code
        InputDown,        // Button press
        InputHeld,        // Button held
        InputDoublePress, // Double-tap
        ButtonDownContinuous, // Held with repeat
        Automatic,        // Conditions checked each frame
        OnAnimatorEvent,  // Animation event triggers
    }

    /// <summary>
    /// Type of ability stop trigger.
    /// </summary>
    public enum AbilityStopType : byte
    {
        Manual,           // Stopped via code
        InputUp,          // Button released
        Duration,         // Fixed time limit
        OnAnimatorEvent,  // Animation event
        OnGrounded,       // When player lands
        OnNotGrounded,    // When player leaves ground
        Automatic,        // Conditions checked each frame
    }

    /// <summary>
    /// Tracks the current state of abilities for an entity.
    /// </summary>
    [GhostComponent]
    public struct AbilityState : IComponentData
    {
        [GhostField] public int ActiveAbilityIndex; // -1 = none
        [GhostField] public int PendingAbilityIndex; // Queued ability, -1 = none
        [GhostField] public float AbilityStartTime;
        public float AbilityElapsedTime; // Local simulation time
        
        // Helper to check if any ability is active
        public bool IsAbilityActive => ActiveAbilityIndex >= 0;
    }

    /// <summary>
    /// Configuration for a single ability.
    /// Stored in a dynamic buffer on the player entity.
    /// </summary>
    public struct AbilityDefinition : IBufferElementData
    {
        public int AbilityTypeId; // Unique ID per ability type
        public int Priority; // Higher = takes precedence
        public bool IsActive; // Is this capability currently enabled?
        
        // Runtime state flags
        public bool CanStart;
        public bool CanStop;
        public bool HasStarted; // True if updated at least once since start
        
        public float StartTime;
        public AbilityStartType StartType;
        public AbilityStopType StopType;
        
        // Conflict settings
        public int BlockedByMask; // Bitmask of abilities that block this
        public int BlocksMask; // Bitmask of abilities this blocks
        
        // Input config (index into InputBuffer if needed, or specific action ID)
        public int InputActionId;

        // Resource Cost (EPIC 16.8)
        public ResourceType ResourceCostType;
        public float ResourceCostAmount;
    }
    
    /// <summary>
    /// Tag to identify an entity as having ability support.
    /// </summary>
    public struct AbilitySystemTag : IComponentData { }
    
    /// <summary>
    /// Request to start a specific ability manually.
    /// </summary>
    public struct AbilityStartRequest : IComponentData
    {
        public int AbilityIndex;
        public bool Force; // Ignore priority/blocking?
    }
}
