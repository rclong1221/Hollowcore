using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 6: Ranged Initiation
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// How a ranged interaction is initiated.
    /// </summary>
    public enum RangedInitType : byte
    {
        /// <summary>Instant raycast from eye to target.</summary>
        Raycast = 0,
        /// <summary>Thrown projectile traveling at ProjectileSpeed.</summary>
        Projectile = 1,
        /// <summary>Arced throw (grenade-style trajectory).</summary>
        ArcProjectile = 2
    }

    /// <summary>
    /// EPIC 16.1 Phase 6: Configuration for ranged interaction on an INTERACTABLE entity.
    /// Allows interactions to be initiated from beyond normal radius via raycast or projectile.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct RangedInteraction : IComponentData
    {
        /// <summary>Maximum range for initiating this interaction.</summary>
        public float MaxRange;

        /// <summary>Projectile travel speed in units/sec. 0 = instant (Raycast type).</summary>
        public float ProjectileSpeed;

        /// <summary>How the ranged interaction starts.</summary>
        public RangedInitType InitType;

        /// <summary>Whether the player must aim within a cone of the target to initiate.</summary>
        public bool RequireAimAtTarget;
    }

    /// <summary>
    /// EPIC 16.1 Phase 6: Runtime state for a ranged interaction in progress.
    /// Placed on the INTERACTABLE entity alongside RangedInteraction.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct RangedInteractionState : IComponentData
    {
        /// <summary>Whether a projectile is in flight / connection in progress.</summary>
        [GhostField]
        public bool IsConnecting;

        /// <summary>Progress of projectile travel (0 to 1).</summary>
        [GhostField(Quantization = 100)]
        public float ConnectionProgress;

        /// <summary>The entity that initiated the ranged interaction.</summary>
        [GhostField]
        public Entity InitiatorEntity;

        /// <summary>World position where the projectile was launched from.</summary>
        public float3 LaunchPosition;

        /// <summary>World position the projectile is traveling toward.</summary>
        public float3 TargetPosition;
    }
}
