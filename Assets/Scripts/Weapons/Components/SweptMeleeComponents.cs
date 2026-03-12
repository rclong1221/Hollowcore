using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons
{
    /// <summary>
    /// EPIC 15.5: Extended melee hitbox definition for swept collision detection.
    /// Prevents fast-moving weapons from passing through enemies (tunneling).
    /// </summary>
    public struct MeleeHitboxDefinition : IComponentData
    {
        /// <summary>
        /// Offset from weapon pivot to blade tip.
        /// </summary>
        public float3 TipOffset;

        /// <summary>
        /// Offset from weapon pivot to handle/base.
        /// </summary>
        public float3 HandleOffset;

        /// <summary>
        /// Radius for CapsuleCast between tip and handle.
        /// </summary>
        public float CapsuleRadius;

        /// <summary>
        /// Whether to use swept detection (recommended for fast attacks).
        /// </summary>
        public bool UseSweptDetection;

        /// <summary>
        /// Physics layers to detect (matches creature layer by default).
        /// </summary>
        public uint CollisionMask;

        /// <summary>
        /// Default configuration for a sword-like weapon.
        /// </summary>
        public static MeleeHitboxDefinition DefaultSword => new MeleeHitboxDefinition
        {
            TipOffset = new float3(0f, 0f, 1.2f),
            HandleOffset = new float3(0f, 0f, 0.1f),
            CapsuleRadius = 0.08f,
            UseSweptDetection = true,
            CollisionMask = ~0u // All layers by default
        };

        /// <summary>
        /// Default configuration for a fist/unarmed.
        /// </summary>
        public static MeleeHitboxDefinition DefaultFist => new MeleeHitboxDefinition
        {
            TipOffset = new float3(0f, 0f, 0.4f),
            HandleOffset = float3.zero,
            CapsuleRadius = 0.12f,
            UseSweptDetection = true,
            CollisionMask = ~0u
        };

        /// <summary>
        /// Default configuration for large/heavy weapons (slower, wider arc).
        /// </summary>
        public static MeleeHitboxDefinition DefaultGreatsword => new MeleeHitboxDefinition
        {
            TipOffset = new float3(0f, 0f, 1.8f),
            HandleOffset = new float3(0f, 0f, 0.2f),
            CapsuleRadius = 0.12f,
            UseSweptDetection = true,
            CollisionMask = ~0u
        };
    }

    /// <summary>
    /// EPIC 15.5: Runtime state for swept melee detection.
    /// Stores previous frame positions for continuous collision.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SweptMeleeState : IComponentData
    {
        /// <summary>
        /// Previous frame's world position of the weapon tip.
        /// </summary>
        public float3 PreviousTipPosition;

        /// <summary>
        /// Previous frame's world position of the weapon handle.
        /// </summary>
        public float3 PreviousHandlePosition;

        /// <summary>
        /// Whether positions were initialized (skip first frame).
        /// </summary>
        [GhostField]
        public bool IsInitialized;

        /// <summary>
        /// Entities hit this swing (prevent duplicate hits).
        /// Uses a simple array approach since NativeList isn't allowed in components.
        /// We track up to 8 simultaneous hits per swing.
        /// </summary>
        public Entity HitEntity0;
        public Entity HitEntity1;
        public Entity HitEntity2;
        public Entity HitEntity3;
        public Entity HitEntity4;
        public Entity HitEntity5;
        public Entity HitEntity6;
        public Entity HitEntity7;

        /// <summary>
        /// Number of entities hit this swing.
        /// </summary>
        [GhostField]
        public int HitCount;

        /// <summary>
        /// Maximum hits per swing (pierce/cleave limit).
        /// 0 = unlimited, 1 = single target, 3 = typical cleave
        /// </summary>
        public int MaxHitsPerSwing;

        /// <summary>
        /// Reset hit tracking for a new swing.
        /// </summary>
        public void ResetHits()
        {
            HitCount = 0;
            HitEntity0 = Entity.Null;
            HitEntity1 = Entity.Null;
            HitEntity2 = Entity.Null;
            HitEntity3 = Entity.Null;
            HitEntity4 = Entity.Null;
            HitEntity5 = Entity.Null;
            HitEntity6 = Entity.Null;
            HitEntity7 = Entity.Null;
        }

        /// <summary>
        /// Check if an entity was already hit this swing.
        /// </summary>
        public bool WasEntityHit(Entity entity)
        {
            if (entity == Entity.Null) return false;
            return entity == HitEntity0 || entity == HitEntity1 ||
                   entity == HitEntity2 || entity == HitEntity3 ||
                   entity == HitEntity4 || entity == HitEntity5 ||
                   entity == HitEntity6 || entity == HitEntity7;
        }

        /// <summary>
        /// Register a hit entity. Returns true if successfully registered.
        /// </summary>
        public bool RegisterHit(Entity entity)
        {
            if (entity == Entity.Null) return false;
            if (WasEntityHit(entity)) return false;
            if (MaxHitsPerSwing > 0 && HitCount >= MaxHitsPerSwing) return false;

            switch (HitCount)
            {
                case 0: HitEntity0 = entity; break;
                case 1: HitEntity1 = entity; break;
                case 2: HitEntity2 = entity; break;
                case 3: HitEntity3 = entity; break;
                case 4: HitEntity4 = entity; break;
                case 5: HitEntity5 = entity; break;
                case 6: HitEntity6 = entity; break;
                case 7: HitEntity7 = entity; break;
                default: return false; // Max 8 hits
            }

            HitCount++;
            return true;
        }

        /// <summary>
        /// Check if can hit more targets.
        /// </summary>
        public bool CanHitMore => MaxHitsPerSwing <= 0 || HitCount < MaxHitsPerSwing;
    }

    /// <summary>
    /// EPIC 15.5: Event raised when a swept melee hit is detected.
    /// Used to trigger damage, VFX, and feedback systems.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SweptMeleeHitEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that was hit.
        /// </summary>
        public Entity HitEntity;

        /// <summary>
        /// World position of the hit.
        /// </summary>
        public float3 HitPosition;

        /// <summary>
        /// Surface normal at hit point.
        /// </summary>
        public float3 HitNormal;

        /// <summary>
        /// Damage to apply (after hitbox multiplier).
        /// </summary>
        public float Damage;

        /// <summary>
        /// Whether this was a critical/headshot hit.
        /// </summary>
        public bool IsCritical;

        /// <summary>
        /// Server tick when hit occurred.
        /// </summary>
        public uint ServerTick;
    }
}
