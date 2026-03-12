using Unity.NetCode;
using Unity.Mathematics;

namespace Player.Components
{
    /// <summary>
    /// RPC sent from owning client to server when ragdoll has settled.
    /// Contains the final world position of the body (pelvis).
    /// Server will update LocalTransform to this position.
    /// </summary>
    public struct RagdollSettledV3Rpc : IRpcCommand
    {
        /// <summary>
        /// Final world position of the ragdoll pelvis after settling.
        /// </summary>
        public float3 FinalPosition;
        
        /// <summary>
        /// Final world rotation of the ragdoll pelvis after settling.
        /// </summary>
        public quaternion FinalRotation;
        
        /// <summary>
        /// The GhostId of the player entity.
        /// Entity IDs are world-local, so we use GhostId for cross-network lookup.
        /// </summary>
        public int PlayerGhostId;
    }
}
