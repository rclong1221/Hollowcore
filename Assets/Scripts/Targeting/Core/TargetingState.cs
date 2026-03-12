using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Targeting.Core
{
    /// <summary>
    /// Current targeting state for a player entity.
    /// Separated from lock BEHAVIOR (how the lock affects camera/character).
    /// 
    /// ARCHITECTURE:
    /// 
    /// ┌─────────────────────────────────────────────────────────────────────────┐
    /// │                         TARGETING PIPELINE                              │
    /// ├─────────────────────────────────────────────────────────────────────────┤
    /// │                                                                         │
    /// │  ┌──────────────┐     ┌──────────────┐     ┌──────────────────────┐    │
    /// │  │   TARGET     │     │   TARGET     │     │   LOCK BEHAVIOR      │    │
    /// │  │  SELECTION   │────▶│    STATE     │────▶│   APPLICATION        │    │
    /// │  │   SYSTEM     │     │  (This Comp) │     │   (Per Camera Mode)  │    │
    /// │  └──────────────┘     └──────────────┘     └──────────────────────┘    │
    /// │        │                                            │                   │
    /// │        ▼                                            ▼                   │
    /// │  ┌──────────────┐                          ┌──────────────────────┐    │
    /// │  │ LockOnTarget │                          │ HardLockSystem       │    │
    /// │  │ Components   │                          │ SoftLockSystem       │    │
    /// │  │ (on enemies) │                          │ IsometricLockSystem  │    │
    /// │  └──────────────┘                          └──────────────────────┘    │
    /// │                                                                         │
    /// └─────────────────────────────────────────────────────────────────────────┘
    /// 
    /// This separation allows:
    /// 1. Same target selection logic for all camera modes
    /// 2. Different lock EFFECTS per camera mode
    /// 3. Easy switching between modes at runtime
    /// </summary>
    public struct TargetingState : IComponentData
    {
        // ═══════════════════════════════════════════════════════════════════
        // TARGET SELECTION STATE
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Currently locked target entity. Entity.Null if no lock.
        /// </summary>
        public Entity CurrentTarget;
        
        /// <summary>
        /// True if player has actively engaged lock (pressed lock button).
        /// False if just soft-targeting (nearest enemy for aim assist).
        /// </summary>
        public bool IsHardLocked;
        
        /// <summary>
        /// Soft-target for aim assist when not hard-locked.
        /// Auto-updates to nearest valid target in front of player.
        /// </summary>
        public Entity SoftTarget;
        
        /// <summary>
        /// Cached position of current target for smooth interpolation.
        /// </summary>
        public float3 TargetPosition;
        
        /// <summary>
        /// Time since target was acquired (for UI fade-in effects).
        /// </summary>
        public float LockDuration;
        
        // ═══════════════════════════════════════════════════════════════════
        // TARGET CYCLING
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Index for cycling through multiple targets.
        /// </summary>
        public int TargetCycleIndex;
        
        /// <summary>
        /// Cooldown to prevent rapid target cycling.
        /// </summary>
        public float CycleCooldown;
        
        // ═══════════════════════════════════════════════════════════════════
        // INPUT TRACKING
        // ═══════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Previous frame's lock button state (for edge detection).
        /// </summary>
        public bool WasLockPressed;
        
        /// <summary>
        /// Previous frame's cycle input (for edge detection).
        /// </summary>
        public float2 PreviousCycleInput;
        
        // ═══════════════════════════════════════════════════════════════════
        // HELPER PROPERTIES
        // ═══════════════════════════════════════════════════════════════════
        
        public bool HasTarget => CurrentTarget != Entity.Null;
        public bool HasSoftTarget => SoftTarget != Entity.Null;
        
        /// <summary>
        /// Returns the effective target (hard lock takes priority over soft).
        /// </summary>
        public Entity EffectiveTarget => IsHardLocked ? CurrentTarget : SoftTarget;
    }
    
    /// <summary>
    /// Configuration for target selection (shared across all modes).
    /// </summary>
    public struct TargetSelectionConfig : IComponentData
    {
        /// <summary>
        /// Maximum distance to search for targets.
        /// </summary>
        public float MaxRange;
        
        /// <summary>
        /// Field of view for initial target acquisition (degrees).
        /// Targets outside this cone won't be selected when first locking.
        /// </summary>
        public float AcquisitionFOV;
        
        /// <summary>
        /// If true, only targets in front of the camera can be locked.
        /// </summary>
        public bool RequireLineOfSight;
        
        /// <summary>
        /// Minimum distance to target (to prevent locking self/nearby objects).
        /// </summary>
        public float MinRange;
        
        /// <summary>
        /// How long lock persists after target leaves range (seconds).
        /// </summary>
        public float LockPersistTime;
        
        /// <summary>
        /// Cooldown between target cycle inputs.
        /// </summary>
        public float CycleCooldown;
        
        public static TargetSelectionConfig Default => new TargetSelectionConfig
        {
            MaxRange = 30f,
            AcquisitionFOV = 60f, // 60 degree cone
            RequireLineOfSight = true,
            MinRange = 1.5f,
            LockPersistTime = 0.5f,
            CycleCooldown = 0.2f
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-LOCK SUPPORT (Zone of the Enders, Ace Combat style missile salvos)
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// State for multi-target lock systems.
    /// Used with LockedTargetElement buffer.
    /// </summary>
    public struct MultiLockState : IComponentData
    {
        /// <summary>
        /// Current number of locked targets.
        /// </summary>
        public int LockedCount;
        
        /// <summary>
        /// Whether multi-lock is being held (accumulating targets).
        /// </summary>
        public bool IsAccumulating;
        
        /// <summary>
        /// Whether targets are ready to fire (salvo complete).
        /// </summary>
        public bool ReadyToFire;
        
        /// <summary>
        /// Maximum targets that can be locked simultaneously.
        /// </summary>
        public int MaxTargets;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // PART TARGETING SUPPORT (Monster Hunter, Fallout VATS style)
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Defines a targetable part on an enemy.
    /// Attached as a buffer to entities with multiple weak points.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TargetablePartElement : IBufferElementData
    {
        /// <summary>
        /// Part identifier (0 = center mass, 1 = head, etc).
        /// </summary>
        public int PartId;
        
        /// <summary>
        /// Local offset from entity center to this part.
        /// </summary>
        public float3 LocalOffset;
        
        /// <summary>
        /// Damage multiplier when hitting this part (e.g., 2.0 for headshot).
        /// </summary>
        public float DamageMultiplier;
        
        /// <summary>
        /// Whether this part is currently exposed/targetable.
        /// Some parts only targetable in certain enemy states.
        /// </summary>
        public bool IsExposed;
        
        /// <summary>
        /// Radius of this hit zone (for targeting proximity).
        /// </summary>
        public float HitRadius;
    }
    
    /// <summary>
    /// Current part targeting state (what part player is aiming at).
    /// Added to player entity when part targeting is enabled.
    /// </summary>
    public struct PartTargetingState : IComponentData
    {
        /// <summary>
        /// Currently selected part index.
        /// </summary>
        public int CurrentPartIndex;
        
        /// <summary>
        /// Offset to the targeted part (world space).
        /// </summary>
        public float3 PartOffset;
        
        /// <summary>
        /// Damage multiplier of current part.
        /// </summary>
        public float CurrentDamageMultiplier;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // OVER-THE-SHOULDER STATE (RE4, Gears, TLOU style)
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// State for over-the-shoulder camera mode.
    /// Tracks shoulder side, ADS state, etc.
    /// </summary>
    public struct OverTheShoulderState : IComponentData
    {
        /// <summary>
        /// Current shoulder offset (-1 = left, 1 = right).
        /// Smoothly interpolates when swapping.
        /// </summary>
        public float CurrentShoulderSide;
        
        /// <summary>
        /// Desired shoulder side after swap request.
        /// </summary>
        public float DesiredShoulderSide;
        
        /// <summary>
        /// Whether player is aiming down sights.
        /// </summary>
        public bool IsAiming;
        
        /// <summary>
        /// Current zoom level (1.0 = normal, 0.5 = zoomed in).
        /// </summary>
        public float CurrentZoom;
        
        /// <summary>
        /// Target zoom based on ADS state.
        /// </summary>
        public float DesiredZoom;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // PREDICTIVE AIM STATE (Flight sims, space games)
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// State for predictive aim / lead indicator.
    /// Shows where to aim to hit a moving target.
    /// </summary>
    public struct PredictiveAimState : IComponentData
    {
        /// <summary>
        /// Target's current velocity.
        /// </summary>
        public float3 TargetVelocity;
        
        /// <summary>
        /// Target's previous position (for velocity calculation).
        /// </summary>
        public float3 PreviousTargetPosition;
        
        /// <summary>
        /// Predicted intercept point (where to aim).
        /// </summary>
        public float3 PredictedAimPoint;
        
        /// <summary>
        /// Time to intercept at current projectile speed.
        /// </summary>
        public float TimeToIntercept;
        
        /// <summary>
        /// Whether the predicted aim is valid (target in range, etc).
        /// </summary>
        public bool IsValid;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // AIM ASSIST STATE (Console FPS style)
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// State for sticky aim and aim magnetism.
    /// Used in FPS/TPS games to help controller aiming.
    /// </summary>
    public struct AimAssistState : IComponentData
    {
        /// <summary>
        /// Entity currently being "stuck" to (if any).
        /// </summary>
        public Entity StickyTarget;
        
        /// <summary>
        /// How much to slow aim movement (0 = normal, 1 = stopped).
        /// </summary>
        public float CurrentStickyStrength;
        
        /// <summary>
        /// Direction of aim magnetism pull this frame.
        /// </summary>
        public float2 MagnetismPull;
        
        /// <summary>
        /// Whether aim is currently in sticky zone.
        /// </summary>
        public bool InStickyZone;
    }
}
