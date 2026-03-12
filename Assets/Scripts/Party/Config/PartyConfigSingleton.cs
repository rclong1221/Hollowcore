using Unity.Entities;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Singleton loaded by PartyBootstrapSystem from Resources/PartyConfig.
    /// 40 bytes.
    /// </summary>
    public struct PartyConfigSingleton : IComponentData
    {
        public byte MaxPartySize;
        public int InviteTimeoutTicks;
        public float XPShareRange;
        public float XPShareBonusPerMember;
        public float LootRange;
        public float KillCreditRange;
        public int LootDesignationTimeoutTicks;
        public int NeedGreedVoteTimeoutTicks;
        public float LootGoldSplitPercent;
        public bool AllowLootModeVote;
        public LootMode DefaultLootMode;
    }
}
