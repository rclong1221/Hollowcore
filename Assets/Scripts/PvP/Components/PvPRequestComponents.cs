using Unity.Entities;
using Unity.NetCode;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Transient entity requesting a PvP match start.
    /// Created by matchmaking or manual lobby trigger.
    /// </summary>
    public struct PvPMatchRequest : IComponentData
    {
        public PvPGameMode GameMode;
        public byte MapId;
        public byte MaxPlayers;
        public byte NormalizationMode;
        public int MaxScore;
        public float MatchDuration;
    }

    /// <summary>
    /// EPIC 17.10: Transient entity created when a match ends.
    /// Read by PvPUIBridgeSystem for results display, then destroyed.
    /// </summary>
    public struct PvPMatchResult : IComponentData
    {
        public PvPGameMode GameMode;
        public byte WinningTeam;
        public byte PlayerCount;
        public byte Padding;
        public float MatchDurationActual;
    }

    /// <summary>
    /// EPIC 17.10: RPC: Client requests to join PvP queue.
    /// </summary>
    public struct PvPJoinQueueRpc : IRpcCommand
    {
        public PvPGameMode GameMode;
        public byte PreferredMapId;
    }

    /// <summary>
    /// EPIC 17.10: RPC: Client requests to leave PvP queue or forfeit match.
    /// </summary>
    public struct PvPLeaveRpc : IRpcCommand
    {
        public byte Reason;
    }
}
