using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Weapons
{
    /// <summary>
    /// ECS Buffer Element that stores the baked combo chain for a weapon.
    /// Replaces hardcoded logic in MeleeSystem.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ComboData : IBufferElementData
    {
        // We bake the Animation Hash (int) instead of the Clip (Object) for ECS
        public int AnimationHash;
        
        // This corresponds to opsive's "Substate Index" for the Animator
        public int AnimatorSubStateIndex;
        
        public float Duration;
        public float InputWindowStart;
        public float InputWindowEnd;
        public float DamageMultiplier;
        public float KnockbackForce;
    }
}
