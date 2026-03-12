using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Server-authoritative ragdoll hips position sync (EPIC 13.19).
    ///
    /// Written by SERVER (RagdollHipsSyncServerSystem) after physics simulation.
    /// Read by CLIENTS (RagdollHipsSyncReaderSystem) to position kinematic ragdolls.
    ///
    /// This component lives on the ROOT player entity (not child bones) so it
    /// benefits from standard ghost snapshot delta compression.
    ///
    /// NOTE: Using GhostPrefabType.All to replicate to INTERPOLATED ghosts (non-owned players).
    /// AllPredicted only replicates to predicted ghosts (owned players), but this data
    /// is specifically needed for remote/non-owned players who observe ragdolls.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RagdollHipsSync : IComponentData
    {
        /// <summary>
        /// World position of ragdoll hips/pelvis from server physics.
        /// </summary>
        [GhostField(Quantization = 100)] // 1cm precision
        public float3 Position;

        /// <summary>
        /// World rotation of ragdoll hips/pelvis from server physics.
        /// </summary>
        [GhostField(Quantization = 1000)] // ~0.1 degree precision
        public quaternion Rotation;

        /// <summary>
        /// True while ragdoll is active on server. False when not ragdolling.
        /// Clients use this to know whether to apply sync data.
        /// </summary>
        [GhostField]
        public bool IsActive;
        
        /// <summary>
        /// Linear velocity of the hips for momentum continuity.
        /// Helps non-owned ragdolls initialize with correct momentum.
        /// </summary>
        [GhostField(Quantization = 10)] // 0.1 m/s precision
        public float3 LinearVelocity;
    }
}
