using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace DIG.Player.Abilities
{
    /// <summary>
    /// Configuration for input-based ability triggering.
    /// Added to the player entity.
    /// </summary>
    public struct AbilityInputStarter : IComponentData
    {
        public int AbilityIndex; // Index in AbilityDefinition buffer
        public int InputActionId; // ID mapping to Input System action
        // public TriggerBehavior Behavior; // Press, Hold, Release, DoubleTap
    }

    /// <summary>
    /// Component to automatically stop an ability after a set duration.
    /// </summary>
    public struct AbilityDurationStopper : IComponentData
    {
        public int AbilityIndex;
        public float Duration;
    }
}
