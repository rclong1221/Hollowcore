using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// RPC to request a death state change from client to server.
    /// Used by debug system to ensure only the requesting player is affected.
    /// </summary>
    public struct DeathDebugRpc : IRpcCommand
    {
        /// <summary>
        /// The requested death phase (Dead, Downed, or Alive).
        /// </summary>
        public DeathPhase RequestedPhase;
        
        /// <summary>
        /// If true, also restore health to max (used for Alive/revive).
        /// </summary>
        public bool RestoreHealth;
    }
}
