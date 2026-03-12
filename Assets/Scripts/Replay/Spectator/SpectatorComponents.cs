using Unity.Entities;
using Unity.NetCode;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: RPC sent by client to join as spectator instead of player.
    /// Server responds by marking the connection with SpectatorTag
    /// and NOT spawning a player entity.
    /// </summary>
    public struct SpectatorJoinRequest : IRpcCommand
    {
    }

    /// <summary>
    /// EPIC 18.10: Tag component on the NetworkId connection entity.
    /// Marks this connection as spectator — GoInGameServerSystem skips player spawn.
    /// Does NOT go on any ghost prefab (no archetype impact).
    /// </summary>
    public struct SpectatorTag : IComponentData
    {
    }

    /// <summary>
    /// EPIC 18.10: Singleton on the client, present only when in spectator mode.
    /// Managed by SpectatorSystem.
    /// </summary>
    public struct SpectatorState : IComponentData
    {
        /// <summary>Ghost ID of the currently followed entity (0 = free cam).</summary>
        public ushort FollowedGhostId;

        /// <summary>Current camera mode.</summary>
        public SpectatorCameraMode CameraMode;

        /// <summary>Index into the player list for Tab cycling.</summary>
        public byte FollowedPlayerIndex;
    }
}
