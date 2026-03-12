using Unity.Entities;
using Unity.Mathematics;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: A single encounter trigger definition.
    /// Condition → Action pair evaluated by EncounterTriggerSystem.
    /// Supports composite triggers (AND/OR) via sub-trigger references.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct EncounterTriggerDefinition : IBufferElementData
    {
        // Condition
        public TriggerConditionType ConditionType;
        public float ConditionValue;         // Threshold (HP%, seconds, count, etc.)
        public byte ConditionParam;          // SpawnGroupId, AbilityIndex, etc.
        public float ConditionRange;         // Distance for position/player-count checks
        public float3 ConditionPosition;     // World position for BossAtPosition checks

        // For composite triggers
        public byte SubTriggerIndex0;        // Index of first sub-trigger (255 = unused)
        public byte SubTriggerIndex1;        // Index of second sub-trigger (255 = unused)
        public byte SubTriggerIndex2;        // Index of third sub-trigger (255 = unused)

        // Action
        public TriggerActionType ActionType;
        public float ActionValue;            // Phase number, duration, multiplier, etc.
        public byte ActionParam;             // AbilityId, SpawnGroupId, etc.
        public float3 ActionPosition;        // Teleport destination, VFX position

        // State
        public bool Enabled;                 // Can be enabled/disabled at runtime
        public bool FireOnce;                // If true, auto-disables after first fire
        public bool HasFired;                // Runtime: has this trigger fired?
        public float Delay;                  // Seconds to wait before executing action
        public float DelayTimer;             // Runtime: countdown for delayed execution
        public bool DelayStarted;            // Runtime: delay countdown active
    }
}
