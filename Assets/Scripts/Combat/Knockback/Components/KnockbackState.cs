using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Runtime knockback state on any knockback-capable entity.
    /// Written by KnockbackResolveSystem, read by KnockbackMovementSystem.
    /// Entities WITHOUT this component ignore all KnockbackRequests targeting them.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct KnockbackState : IComponentData
    {
        /// <summary>True while knockback velocity is being applied.</summary>
        [GhostField]
        public bool IsActive;

        /// <summary>Current knockback velocity in world space (m/s). Decays over Duration via Easing.</summary>
        [GhostField(Quantization = 1000, Smoothing = SmoothingAction.InterpolateAndExtrapolate)]
        public float3 Velocity;

        /// <summary>Initial velocity magnitude at knockback start. Used for easing curve evaluation.</summary>
        [GhostField(Quantization = 100)]
        public float InitialSpeed;

        /// <summary>Total knockback duration in seconds.</summary>
        [GhostField(Quantization = 100)]
        public float Duration;

        /// <summary>Time elapsed since knockback started.</summary>
        [GhostField(Quantization = 100)]
        public float Elapsed;

        /// <summary>Easing curve for velocity decay.</summary>
        [GhostField]
        public KnockbackEasing Easing;

        /// <summary>Knockback type that produced this state (for animation selection).</summary>
        [GhostField]
        public KnockbackType Type;

        /// <summary>If true, knockback only applies while entity is grounded.</summary>
        [GhostField]
        public bool GroundedOnly;

        /// <summary>Source entity that caused this knockback.</summary>
        public Entity SourceEntity;

        /// <summary>Normalized progress (0-1).</summary>
        public float Progress => Duration > 0f ? math.saturate(Elapsed / Duration) : 1f;

        /// <summary>True when knockback has completed its duration.</summary>
        public bool IsExpired => Elapsed >= Duration;
    }
}
