using Unity.Entities;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>Tag to identify party entities in queries.</summary>
    public struct PartyTag : IComponentData { }

    /// <summary>
    /// EPIC 17.2: Core party state on party entity — 28 bytes.
    /// Ghost:All so all clients can see party metadata (leader, loot mode).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PartyState : IComponentData
    {
        [GhostField] public Entity LeaderEntity;
        [GhostField] public LootMode LootMode;
        [GhostField] public byte MaxSize;
        [GhostField] public byte MemberCount;
        [GhostField] public uint CreationTick;
        [GhostField] public int RoundRobinIndex;
        [GhostField] public Entity PartyOwnerConnection;
    }

    /// <summary>Loot distribution mode.</summary>
    public enum LootMode : byte
    {
        FreeForAll = 0,
        RoundRobin = 1,
        NeedGreed = 2,
        MasterLoot = 3
    }

    /// <summary>
    /// EPIC 17.2: Buffer element tracking each party member — 20 bytes per entry.
    /// Ghost:All so all clients see party composition.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    [InternalBufferCapacity(6)]
    public struct PartyMemberElement : IBufferElementData
    {
        [GhostField] public Entity PlayerEntity;
        [GhostField] public Entity ConnectionEntity;
        [GhostField] public uint JoinTick;
    }

    /// <summary>
    /// EPIC 17.2: Proximity tracking per member — 12 bytes per entry.
    /// Updated by PartyProximitySystem. NOT ghost-replicated (server-only spatial data).
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct PartyProximityState : IBufferElementData
    {
        public Entity PlayerEntity;
        public bool InXPRange;
        public bool InLootRange;
        public bool InKillCreditRange;
    }

    /// <summary>
    /// Ephemeral tag added to KillCredited events distributed by PartyKillCreditSystem.
    /// Prevents re-distribution of already-distributed kills.
    /// </summary>
    public struct PartyKillTag : IComponentData { }

    /// <summary>
    /// Ephemeral modifier written by PartyXPSharingSystem, read by XPAwardSystem.
    /// Removed after XPAwardSystem processes.
    /// </summary>
    public struct PartyXPModifier : IComponentData
    {
        public float XPMultiplier;
    }
}
