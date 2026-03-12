using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Event component added to an entity when they score a kill (13.16.12).
    /// Used for UI (Kill Feed), XP, stats, etc.
    /// Ephemeral - should be removed by a system after processing.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct KillCredited : IComponentData
    {
        [GhostField] public Entity Victim;
        [GhostField(Quantization = 100)] public float3 VictimPosition;
        [GhostField] public uint ServerTick;
    }

    /// <summary>
    /// Event component added to an entity when they contribute to a kill (Assist).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct AssistCredited : IComponentData
    {
        [GhostField] public Entity Victim;
        [GhostField] public float DamageDealt;
        [GhostField] public uint ServerTick;
    }

    /// <summary>
    /// Tracks combat history for attribution.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CombatState : IComponentData
    {
        [GhostField]
        public Entity LastAttacker;
        
        [GhostField(Quantization = 100)]
        public double LastAttackTime;
    }

    /// <summary>
    /// Buffer to store recent damage sources for assist tracking.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RecentAttackerElement : IBufferElementData
    {
        public Entity Attacker;
        public float DamageDealt;
        public double Time;
    }
}
