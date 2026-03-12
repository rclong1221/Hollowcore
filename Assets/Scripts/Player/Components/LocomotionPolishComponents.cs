using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Motor polish settings for fine-tuned movement feel.
    /// Inspired by Opsive UCC CharacterLocomotion.
    /// <para>
    /// <b>Architecture:</b> Baked on Warrok_Server, replicated via GhostField where needed.
    /// Systems run in PredictedSimulationSystemGroup with Burst compilation.
    /// </para>
    /// <para><b>Performance:</b> Uses byte instead of bool for Burst blittability.</para>
    /// </summary>
    public struct MotorPolishSettings : IComponentData
    {
        /// <summary>
        /// Multiplier applied when walking backwards (0.7 = 70% speed).
        /// Higher values = less penalty for backwards movement.
        /// </summary>
        [GhostField] public float MotorBackwardsMultiplier;
        
        /// <summary>
        /// How much previous acceleration influences current movement (0-1).
        /// 0 = instant direction changes (arcade), 1 = full momentum preservation (realistic).
        /// </summary>
        [GhostField] public float PreviousAccelerationInfluence;
        
        /// <summary>Whether to adjust motor force when on slopes. 1 = true, 0 = false.</summary>
        public byte AdjustMotorForceOnSlope;
        
        /// <summary>Multiplier when moving uphill. 1.0 = no change, 0.8 = 20% slower.</summary>
        public float MotorSlopeForceUp;
        
        /// <summary>Multiplier when moving downhill. 1.0 = no change, 1.25 = 25% faster.</summary>
        public float MotorSlopeForceDown;
        
        /// <summary>Ground stick distance - how far below current position to check for ground.</summary>
        public float GroundStickDistance;
        
        public static MotorPolishSettings Default => new MotorPolishSettings
        {
            MotorBackwardsMultiplier = 0.7f,
            PreviousAccelerationInfluence = 0.5f,
            AdjustMotorForceOnSlope = 1,
            MotorSlopeForceUp = 1.0f,
            MotorSlopeForceDown = 1.15f,
            GroundStickDistance = 0.1f
        };
        
        /// <summary>Arcade preset - snappy, responsive controls</summary>
        public static MotorPolishSettings Arcade => new MotorPolishSettings
        {
            MotorBackwardsMultiplier = 0.9f,
            PreviousAccelerationInfluence = 0.3f,
            AdjustMotorForceOnSlope = 1,
            MotorSlopeForceUp = 1.0f,
            MotorSlopeForceDown = 1.0f,
            GroundStickDistance = 0.05f
        };
        
        /// <summary>Realistic preset - weighty, momentum-based</summary>
        public static MotorPolishSettings Realistic => new MotorPolishSettings
        {
            MotorBackwardsMultiplier = 0.6f,
            PreviousAccelerationInfluence = 1.0f,
            AdjustMotorForceOnSlope = 1,
            MotorSlopeForceUp = 0.8f,
            MotorSlopeForceDown = 1.3f,
            GroundStickDistance = 0.15f
        };
    }
    
    /// <summary>
    /// Wall collision behavior settings.
    /// Controls how the character slides and bounces off walls.
    /// </summary>
    public struct WallSlideSettings : IComponentData
    {
        /// <summary>Multiplier for wall friction from PhysicMaterial.</summary>
        public float WallFrictionModifier;
        
        /// <summary>Multiplier for wall bounce from PhysicMaterial.</summary>
        public float WallBounceModifier;
        
        /// <summary>Ground friction multiplier from PhysicMaterial.</summary>
        public float GroundFrictionModifier;
        
        public static WallSlideSettings Default => new WallSlideSettings
        {
            WallFrictionModifier = 1.0f,
            WallBounceModifier = 0f,
            GroundFrictionModifier = 1.0f
        };
    }
    
    /// <summary>
    /// Advanced collision detection settings.
    /// Controls penetration resolution and continuous collision.
    /// </summary>
    public struct CollisionPolishSettings : IComponentData
    {
        /// <summary>Check for penetrations when stationary. 1 = true, 0 = false.</summary>
        public byte ContinuousCollisionDetection;
        
        /// <summary>Maximum iterations for penetration resolution.</summary>
        public int MaxPenetrationChecks;
        
        /// <summary>Maximum iterations for movement collision checks.</summary>
        public int MaxMovementCollisionChecks;
        
        /// <summary>Small gap to prevent floating point overlap issues.</summary>
        public float ColliderSpacing;
        
        /// <summary>Cancel vertical external force on ceiling hit. 1 = true, 0 = false.</summary>
        public byte CancelForceOnCeiling;
        
        public static CollisionPolishSettings Default => new CollisionPolishSettings
        {
            ContinuousCollisionDetection = 1,
            MaxPenetrationChecks = 5,
            MaxMovementCollisionChecks = 5,
            ColliderSpacing = 0.01f,
            CancelForceOnCeiling = 1
        };
    }
    
    /// <summary>
    /// Runtime state for motor polish calculations.
    /// Tracks previous frame data for momentum preservation.
    /// </summary>
    public struct MotorPolishState : IComponentData
    {
        /// <summary>Previous frame's motor rotation for momentum calculation.</summary>
        [GhostField] public quaternion PrevMotorRotation;
        
        /// <summary>Previous frame's horizontal velocity.</summary>
        [GhostField] public float3 PrevHorizontalVelocity;
        
        /// <summary>Current slope angle in degrees (0 = flat).</summary>
        public float CurrentSlopeAngle;
        
        /// <summary>Is the player moving uphill? 1 = true, 0 = false.</summary>
        public byte IsMovingUphill;
    }
    
    /// <summary>
    /// Buffer element for soft force distribution.
    /// Spreads impulses across multiple frames to prevent jerky movement.
    /// </summary>
    public struct SoftForceFrame : IBufferElementData
    {
        /// <summary>Force to apply this frame.</summary>
        public float3 Force;
    }
    
    /// <summary>
    /// Configuration for soft force distribution.
    /// </summary>
    public struct SoftForceSettings : IComponentData
    {
        /// <summary>Maximum frames to distribute forces across.</summary>
        public int MaxSoftForceFrames;
        
        /// <summary>Current index in the soft force buffer.</summary>
        public int CurrentFrameIndex;
        
        public static SoftForceSettings Default => new SoftForceSettings
        {
            MaxSoftForceFrames = 30,
            CurrentFrameIndex = 0
        };
    }
}
