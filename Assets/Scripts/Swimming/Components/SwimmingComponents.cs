using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Swimming
{
    /// <summary>
    /// Tracks the swimming state of a player entity.
    /// Updated by WaterDetectionSystem when player enters/exits water zones.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SwimmingState : IComponentData
    {
        /// <summary>World Y coordinate of the water surface</summary>
        [GhostField(Quantization = 100)]
        public float WaterSurfaceY;
        
        /// <summary>How deep the player is submerged (meters)</summary>
        [GhostField(Quantization = 100)]
        public float SubmersionDepth;
        
        /// <summary>True when player is in swimming mode</summary>
        [GhostField]
        public bool IsSwimming;
        
        /// <summary>True when player's head is underwater</summary>
        [GhostField]
        public bool IsSubmerged;
        
        /// <summary>Current water zone entity (if any)</summary>
        public Entity WaterZoneEntity;
        
        /// <summary>Threshold ratio (submersion/height) to enter swimming</summary>
        public float SwimEntryThreshold;
        
        /// <summary>Threshold ratio to exit swimming (must be lower than entry)</summary>
        public float SwimExitThreshold;
        
        /// <summary>Player height for calculating submersion ratio</summary>
        public float PlayerHeight;

        /// <summary>Opsive swim state (0-4) for animation</summary>
        [GhostField] 
        public int OpsiveSwimState;
        
        public static SwimmingState Default => new SwimmingState
        {
            WaterSurfaceY = float.MinValue,
            SubmersionDepth = 0f,
            IsSwimming = false,
            IsSubmerged = false,
            WaterZoneEntity = Entity.Null,
            SwimEntryThreshold = 0.6f,  // Enter swim mode when 60% submerged
            SwimExitThreshold = 0.3f,   // Exit swim mode when only 30% submerged
            PlayerHeight = 1.8f
        };
    }

    /// <summary>
    /// Properties of a water volume.
    /// Added to water zone entities alongside EnvironmentZone.
    /// </summary>
    public struct WaterProperties : IComponentData
    {
        /// <summary>Water density (kg/m³). Default 1000 for water, 1025 for seawater</summary>
        public float Density;
        
        /// <summary>Viscosity affects movement drag. Higher = slower movement</summary>
        public float Viscosity;
        
        /// <summary>Flow direction and speed (for currents/rivers)</summary>
        public float3 CurrentVelocity;
        
        /// <summary>Buoyancy multiplier. 0 = neutral, positive = float up, negative = sink</summary>
        public float BuoyancyModifier;
        
        /// <summary>Surface Y coordinate of this water body</summary>
        public float SurfaceY;
        
        public static WaterProperties Default => new WaterProperties
        {
            Density = 1000f,        // Fresh water
            Viscosity = 1.0f,       // Medium-High drag
            CurrentVelocity = float3.zero,
            BuoyancyModifier = 0.5f,  // Noticeable positive buoyancy
            SurfaceY = 0f
        };
    }

    /// <summary>
    /// Breath/lung state for underwater without suit.
    /// Used alongside SwimmingState for drowning mechanics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct BreathState : IComponentData
    {
        /// <summary>Current breath remaining (seconds)</summary>
        [GhostField(Quantization = 100)]
        public float CurrentBreath;
        
        /// <summary>Maximum breath capacity (seconds)</summary>
        [GhostField(Quantization = 100)]
        public float MaxBreath;
        
        /// <summary>Is player currently holding breath (underwater without suit)?</summary>
        [GhostField]
        public bool IsHoldingBreath;
        
        /// <summary>Time until next drowning damage tick</summary>
        public float DrowningDamageTimer;
        
        /// <summary>Damage per tick when drowning</summary>
        public float DrowningDamagePerTick;
        
        /// <summary>Interval between drowning damage ticks</summary>
        public float DrowningDamageInterval;
        
        /// <summary>Breath recovery rate when above water (per second)</summary>
        public float BreathRecoveryRate;
        
        public static BreathState Default => new BreathState
        {
            CurrentBreath = 30f,
            MaxBreath = 30f,
            IsHoldingBreath = false,
            DrowningDamageTimer = 0f,
            DrowningDamagePerTick = 10f,
            DrowningDamageInterval = 1f,
            BreathRecoveryRate = 10f  // Recover 10 seconds of breath per second above water
        };
    }

    /// <summary>
    /// Swimming movement configuration.
    /// Attached to entities that can swim.
    /// </summary>
    public struct SwimmingMovementSettings : IComponentData
    {
        /// <summary>Base swim speed (m/s)</summary>
        public float SwimSpeed;
        
        /// <summary>Sprint swim speed multiplier</summary>
        public float SprintMultiplier;
        
        /// <summary>Vertical movement speed (ascending/descending)</summary>
        public float VerticalSpeed;
        
        /// <summary>Drag coefficient (higher = more resistance)</summary>
        public float DragCoefficient;
        
        /// <summary>Acceleration in water</summary>
        public float Acceleration;
        
        /// <summary>Deceleration when no input</summary>
        public float Deceleration;
        
        /// <summary>Buoyancy force multiplier (based on player mass)</summary>
        public float BuoyancyForce;
        
        public static SwimmingMovementSettings Default => new SwimmingMovementSettings
        {
            SwimSpeed = 4f,
            SprintMultiplier = 1.5f,
            VerticalSpeed = 1.5f,
            DragCoefficient = 3f,
            Acceleration = 4f,
            Deceleration = 5f,
            BuoyancyForce = 2f
        };
    }
    
    /// <summary>
    /// Tag component to mark an entity as capable of swimming.
    /// </summary>
    public struct CanSwim : IComponentData { }

    /// <summary>
    /// Tracks controller state overrides during swimming.
    /// Used to disable ground check, gravity, and cache original values for restoration.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SwimmingControllerState : IComponentData
    {
        /// <summary>Was swimming in the previous frame (for edge detection)</summary>
        [GhostField]
        public bool WasSwimming;

        /// <summary>Cached original ground check distance before swim</summary>
        public float OriginalGroundCheckDistance;

        /// <summary>Cached original collider height before swim</summary>
        public float OriginalColliderHeight;

        /// <summary>Cached original collider radius before swim</summary>
        public float OriginalColliderRadius;

        /// <summary>True if we've cached the original values</summary>
        public bool HasCachedValues;

        public static SwimmingControllerState Default => new SwimmingControllerState
        {
            WasSwimming = false,
            OriginalGroundCheckDistance = 0.1f,
            OriginalColliderHeight = 2.0f,
            OriginalColliderRadius = 0.5f,
            HasCachedValues = false
        };
    }

    /// <summary>
    /// 12.3.9: Swimming Event Callbacks
    /// Tracks state transitions for other systems to react to.
    /// Each flag is true for exactly ONE frame when the transition occurs.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SwimmingEvents : IComponentData
    {
        /// <summary>True for one frame when player enters a water zone</summary>
        [GhostField]
        public bool OnEnterWater;

        /// <summary>True for one frame when player exits water zone completely</summary>
        [GhostField]
        public bool OnExitWater;

        /// <summary>True for one frame when player surfaces (head comes above water)</summary>
        [GhostField]
        public bool OnSurface;

        /// <summary>True for one frame when player submerges (head goes underwater)</summary>
        [GhostField]
        public bool OnSubmerge;

        /// <summary>True for one frame when player enters swim mode</summary>
        [GhostField]
        public bool OnStartSwimming;

        /// <summary>True for one frame when player exits swim mode</summary>
        [GhostField]
        public bool OnStopSwimming;

        /// <summary>Previous frame's IsSwimming state (for edge detection)</summary>
        public bool PrevIsSwimming;

        /// <summary>Previous frame's IsSubmerged state (for edge detection)</summary>
        public bool PrevIsSubmerged;

        /// <summary>Previous frame's WaterZoneEntity (for enter/exit detection)</summary>
        public Entity PrevWaterZone;

        public static SwimmingEvents Default => new SwimmingEvents
        {
            OnEnterWater = false,
            OnExitWater = false,
            OnSurface = false,
            OnSubmerge = false,
            OnStartSwimming = false,
            OnStopSwimming = false,
            PrevIsSwimming = false,
            PrevIsSubmerged = false,
            PrevWaterZone = Entity.Null
        };

        /// <summary>Clears all one-frame event flags</summary>
        public void ClearEvents()
        {
            OnEnterWater = false;
            OnExitWater = false;
            OnSurface = false;
            OnSubmerge = false;
            OnStartSwimming = false;
            OnStopSwimming = false;
        }
    }

    /// <summary>
    /// Configuration for swimming physics adjustments.
    /// Controls collider size reduction and surface positioning.
    /// </summary>
    public struct SwimmingPhysicsSettings : IComponentData
    {
        /// <summary>Collider height when underwater (prevents wall clipping)</summary>
        public float UnderwaterColliderHeight;

        /// <summary>Collider radius when underwater</summary>
        public float UnderwaterColliderRadius;

        /// <summary>Speed to lerp towards surface when idle at water level</summary>
        public float SurfaceAnchorSpeed;

        /// <summary>Offset from water surface for idle position (negative = below surface)</summary>
        public float SurfaceAnchorOffset;

        /// <summary>Threshold for considering "at surface" for anchoring</summary>
        public float SurfaceThreshold;

        public static SwimmingPhysicsSettings Default => new SwimmingPhysicsSettings
        {
            UnderwaterColliderHeight = 1.0f,    // Reduced from 2.0
            UnderwaterColliderRadius = 0.4f,    // Reduced from 0.5
            SurfaceAnchorSpeed = 2.0f,          // Lerp speed
            SurfaceAnchorOffset = -0.3f,        // Slightly below surface (neck level)
            SurfaceThreshold = 0.5f             // Within 0.5m of surface
        };
    }
}
