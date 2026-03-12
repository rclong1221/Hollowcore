using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tag and velocity data for moving platforms.
    /// Add to any object that should carry the player when stood upon.
    /// <para>
    /// <b>Architecture:</b> Platforms exist in both client and server worlds.
    /// Velocity is calculated from position delta each frame.
    /// </para>
    /// <para><b>Performance:</b> Uses byte instead of bool for Burst blittability.</para>
    /// </summary>
    public struct MovingPlatform : IComponentData
    {
        /// <summary>Position from previous frame for velocity calculation.</summary>
        public float3 LastPosition;
        
        /// <summary>Rotation from previous frame for angular velocity calculation.</summary>
        public quaternion LastRotation;
        
        /// <summary>Calculated linear velocity (units per second).</summary>
        [GhostField(Quantization = 100)] public float3 Velocity;
        
        /// <summary>Calculated angular velocity (radians per second).</summary>
        [GhostField(Quantization = 100)] public float3 AngularVelocity;
        
        /// <summary>If 1, player inherits platform momentum when jumping off.</summary>
        public byte InheritMomentumOnDisconnect;
        
        /// <summary>Threshold for detecting sudden platform stops.</summary>
        public float SuddenStopThreshold;
    }
    
    /// <summary>
    /// Component added to players when standing on a moving platform.
    /// Stores the player's position relative to the platform.
    /// Uses IEnableableComponent pattern - disabled when not on a platform.
    /// </summary>
    public struct OnMovingPlatform : IComponentData, IEnableableComponent
    {
        /// <summary>The platform entity the player is attached to.</summary>
        public Entity PlatformEntity;
        
        /// <summary>Player's position in platform-local space.</summary>
        [GhostField(Quantization = 1000)] public float3 LocalPosition;
        
        /// <summary>Player's rotation relative to platform.</summary>
        [GhostField] public quaternion LocalRotation;
        
        /// <summary>Time the player has been on this platform.</summary>
        public float TimeOnPlatform;
    }
    
    /// <summary>
    /// Component for inherited momentum after leaving a platform.
    /// Decays over time to prevent infinite sliding.
    /// </summary>
    public struct PlatformMomentum : IComponentData
    {
        /// <summary>Velocity inherited from the platform.</summary>
        [GhostField(Quantization = 100)] public float3 InheritedVelocity;
        
        /// <summary>How long the momentum has been decaying.</summary>
        public float DecayTimer;
        
        /// <summary>Total decay duration before momentum is zero.</summary>
        public float DecayDuration;
        
        /// <summary>Returns 1 if momentum is still active, 0 otherwise.</summary>
        public byte IsActive => (DecayTimer < DecayDuration && math.lengthsq(InheritedVelocity) > 0.01f) ? (byte)1 : (byte)0;
        
        /// <summary>Gets the current decayed momentum.</summary>
        public float3 GetCurrentMomentum()
        {
            if (DecayTimer >= DecayDuration) return float3.zero;
            float t = 1f - (DecayTimer / DecayDuration);
            return InheritedVelocity * t;
        }
    }
    
    /// <summary>
    /// Settings for moving platform behavior on the player.
    /// </summary>
    public struct MovingPlatformSettings : IComponentData
    {
        /// <summary>How long momentum from platforms lasts after disconnecting.</summary>
        public float MomentumDecayDuration;
        
        /// <summary>Minimum platform velocity to trigger momentum inheritance.</summary>
        public float MinVelocityForMomentum;
        
        /// <summary>Whether to rotate the player with the platform. 1 = true, 0 = false.</summary>
        public byte RotateWithPlatform;
        
        public static MovingPlatformSettings Default => new MovingPlatformSettings
        {
            MomentumDecayDuration = 0.5f,
            MinVelocityForMomentum = 0.5f,
            RotateWithPlatform = 1
        };
    }
    
    /// <summary>
    /// Internal tag to mark entities as needing platform attachment check.
    /// Added when player lands, removed after processing.
    /// </summary>
    public struct NeedsPlatformCheck : IComponentData, IEnableableComponent { }
}
