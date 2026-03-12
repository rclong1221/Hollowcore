using Unity.Entities;
using Unity.NetCode;

namespace DIG.Party
{
    /// <summary>RPC types for party operations.</summary>
    public enum PartyRpcType : byte
    {
        Invite = 0,
        AcceptInvite = 1,
        DeclineInvite = 2,
        Leave = 3,
        Kick = 4,
        Promote = 5,
        SetLootMode = 6,
        LootVote = 7
    }

    /// <summary>
    /// EPIC 17.2: Client -> Server RPC for all party operations — 16 bytes.
    /// </summary>
    public struct PartyRpc : IRpcCommand
    {
        public PartyRpcType Type;
        public Entity TargetPlayer;
        public byte Payload;
    }

    /// <summary>
    /// EPIC 17.2: Server -> Client notification RPC — 16 bytes.
    /// Sent to inform clients of party events (invite received, member joined, etc.).
    /// </summary>
    public struct PartyNotifyRpc : IRpcCommand
    {
        public PartyNotifyType Type;
        public Entity SourcePlayer;
        public byte Payload;
    }

    /// <summary>Notification types for party events.</summary>
    public enum PartyNotifyType : byte
    {
        InviteReceived = 0,
        InviteExpired = 1,
        MemberJoined = 2,
        MemberLeft = 3,
        MemberKicked = 4,
        LeaderChanged = 5,
        LootModeChanged = 6,
        PartyDisbanded = 7,
        LootRollStart = 8,
        LootRollResult = 9,
        InviteDeclined = 10
    }
}
