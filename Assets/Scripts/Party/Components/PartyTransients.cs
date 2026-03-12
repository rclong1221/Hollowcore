using Unity.Entities;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Transient entity representing a pending party invite — 28 bytes.
    /// Created by PartyRpcReceiveSystem, destroyed by accept/decline/timeout.
    /// </summary>
    public struct PartyInvite : IComponentData
    {
        public Entity InviterEntity;
        public Entity InviteeEntity;
        public uint ExpirationTick;
        public Entity InviterParty;
    }

    /// <summary>
    /// EPIC 17.2: Transient entity for NeedGreed loot claims.
    /// Created by PartyLootSystem, collected by PartyNeedGreedResolveSystem.
    /// </summary>
    public struct PartyLootClaim : IComponentData
    {
        public Entity LootEntity;
        public Entity PartyEntity;
        public uint ExpirationTick;
    }

    /// <summary>
    /// EPIC 17.2: Buffer on PartyLootClaim entity tracking each member's vote — 12 bytes per entry.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct LootVoteElement : IBufferElementData
    {
        public Entity PlayerEntity;
        public LootVoteType Vote;
    }

    /// <summary>Vote types for NeedGreed loot mode.</summary>
    public enum LootVoteType : byte
    {
        Pending = 0,
        Need = 1,
        Greed = 2,
        Pass = 3
    }

    /// <summary>
    /// EPIC 17.2: Ephemeral component on loot entities. Designates which player
    /// is allowed to pick up the loot (for RoundRobin/MasterLoot/NeedGreed winner).
    /// Entity.Null = anyone (FreeForAll mode).
    /// </summary>
    public struct LootDesignation : IComponentData
    {
        public Entity DesignatedOwner;
        public uint ExpirationTick;
    }
}
