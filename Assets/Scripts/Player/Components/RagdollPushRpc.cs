using Unity.NetCode;
using Unity.Mathematics;

namespace Player.Components
{
    /// <summary>
    /// RPC sent from client to server when a player pushes a ragdoll body.
    /// Server applies the impulse to the physics simulation, which then replicates via RagdollHipsSync.
    /// </summary>
    public struct RagdollPushRpc : IRpcCommand
    {
        /// <summary>
        /// GhostId of the ragdoll's owner entity (the dead player being pushed).
        /// </summary>
        public int TargetGhostId;
        
        /// <summary>
        /// World position where the push occurred.
        /// </summary>
        public float3 WorldPosition;
        
        /// <summary>
        /// Impulse vector to apply (direction and magnitude of push).
        /// </summary>
        public float3 Impulse;
    }
}
