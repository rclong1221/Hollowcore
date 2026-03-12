using Unity.Entities;
using Unity.NetCode;

namespace DIG.Cinematic
{
    /// <summary>
    /// EPIC 17.9: Server-to-client RPC commanding cinematic playback start.
    /// </summary>
    public struct CinematicPlayRpc : IRpcCommand
    {
        public int CinematicId;
        public CinematicType CinematicType;
        public SkipPolicy SkipPolicy;
        public float Duration;
        public byte TotalPlayers;
    }

    /// <summary>
    /// EPIC 17.9: Client-to-server RPC requesting cinematic skip.
    /// </summary>
    public struct CinematicSkipRpc : IRpcCommand
    {
        public int CinematicId;
        public int NetworkId;  // player who voted to skip
    }

    /// <summary>
    /// EPIC 17.9: Server-to-client RPC commanding cinematic end.
    /// </summary>
    public struct CinematicEndRpc : IRpcCommand
    {
        public int CinematicId;
        public bool WasSkipped;
    }

    /// <summary>
    /// EPIC 17.9: Server-to-client RPC updating skip vote count for UI display.
    /// </summary>
    public struct CinematicSkipUpdateRpc : IRpcCommand
    {
        public int CinematicId;
        public byte SkipVotesReceived;
    }
}
