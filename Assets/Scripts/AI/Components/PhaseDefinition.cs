using Unity.Entities;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: Boss phase definition. Baked from EncounterProfileSO.
    /// PhaseTransitionSystem checks HP thresholds and trigger requests to advance phases.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct PhaseDefinition : IBufferElementData
    {
        public byte PhaseIndex;
        public float HPThresholdEntry;       // Enter this phase when HP% drops below (-1 = trigger-only)
        public float SpeedMultiplier;        // Movement speed modifier (1.0 = normal)
        public float DamageMultiplier;       // Damage output modifier
        public float GlobalCooldownOverride; // Override GCD for this phase (-1 = no override)
        public float InvulnerableDuration;   // Immune window during phase transition
        public ushort TransitionAbilityId;   // Ability to cast on phase entry (0 = none)
        public byte SpawnGroupId;            // Add group to spawn on entry (0 = none)
    }

    /// <summary>
    /// EPIC 15.32: Spawn group definition for add spawning.
    /// Baked from EncounterProfileSO. Referenced by triggers and phase transitions.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SpawnGroupDefinition : IBufferElementData
    {
        public byte GroupId;              // Referenced by triggers and phases
        public Entity PrefabEntity;       // Ghost prefab to spawn
        public byte Count;                // Number of adds to spawn
        public Unity.Mathematics.float3 SpawnOffset;  // Offset from boss position
        public float SpawnRadius;         // Random scatter radius
        public bool TetherToBoss;         // Adds leash to boss, not spawn point
    }
}
