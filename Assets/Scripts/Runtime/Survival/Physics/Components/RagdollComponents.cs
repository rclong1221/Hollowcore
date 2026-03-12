using Unity.Entities;

namespace DIG.Survival.Physics
{
    /// <summary>
    /// Component on the root entity controlling the ragdoll state.
    /// </summary>
    public struct RagdollController : IComponentData
    {
        public bool IsRagdolled;
        public Entity Pelvis;
    }

    /// <summary>
    /// Component on individual bones that participate in the ragdoll.
    /// </summary>
    public struct RagdollBone : IComponentData
    {
        // We can store original pose if we want recovery later.
        public bool IsActive;
        
        /// <summary>
        /// True if this bone is the pelvis/hips root bone.
        /// Used for dynamic pelvis lookup at runtime (fixes NetCode entity reference sharing).
        /// </summary>
        public bool IsPelvis;
        
        /// <summary>
        /// The original parent entity before ragdoll unparented.
        /// </summary>
        public Entity OriginalParent;
    }
}
