using Unity.Entities;

namespace DIG.Party
{
    /// <summary>EPIC 17.2: Full party state snapshot for UI.</summary>
    public struct PartyUIState
    {
        public bool InParty;
        public bool IsLeader;
        public LootMode CurrentLootMode;
        public int MemberCount;
        public int MaxSize;
        public PartyMemberUIState[] Members;
    }

    /// <summary>EPIC 17.2: Per-member UI state.</summary>
    public struct PartyMemberUIState
    {
        public Entity PlayerEntity;
        public string DisplayName;
        public int Level;
        public float HealthCurrent;
        public float HealthMax;
        public float ManaCurrent;
        public float ManaMax;
        public bool IsLeader;
        public bool IsInRange;
        public bool IsAlive;
    }

    /// <summary>EPIC 17.2: Invite dialog state.</summary>
    public struct PartyInviteUIState
    {
        public Entity InviterEntity;
        public string InviterName;
        public int InviterLevel;
        public float TimeRemainingSeconds;
    }

    /// <summary>EPIC 17.2: NeedGreed loot roll state.</summary>
    public struct LootRollUIState
    {
        public Entity LootEntity;
        public string ItemName;
        public int ItemTypeId;
        public float TimeRemainingSeconds;
    }

    /// <summary>EPIC 17.2: Loot roll result state.</summary>
    public struct LootRollResultUIState
    {
        public Entity WinnerEntity;
        public string WinnerName;
        public string ItemName;
        public LootVoteType WinningVote;
    }
}
