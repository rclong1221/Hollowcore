using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tuning data for slide mechanic, authored per-prefab.
    /// Slide can be triggered manually (X button) or automatically on steep slopes/slippery surfaces.
    /// </summary>
    public struct SlideComponent : IComponentData
    {
        /// <summary>Maximum duration of slide in seconds</summary>
        public float Duration;
        
        /// <summary>Minimum speed required to start/maintain slide (m/s)</summary>
        public float MinSpeed;
        
        /// <summary>Maximum speed cap during slide (m/s)</summary>
        public float MaxSpeed;
        
        /// <summary>Acceleration applied in slide direction (m/s²)</summary>
        public float Acceleration;
        
        /// <summary>Friction applied to slow down slide (m/s²)</summary>
        public float Friction;
        
        /// <summary>Stamina cost for manual slide activation</summary>
        public float StaminaCost;
        
        /// <summary>Cooldown before another slide can be triggered (seconds)</summary>
        public float Cooldown;
        
        /// <summary>Minimum slope angle to auto-trigger slide (degrees)</summary>
        public float MinSlopeAngle;
        
        /// <summary>Friction multiplier on slippery surfaces (0-1, lower = more slippery)</summary>
        public float SlipperyFrictionMultiplier;

        public static SlideComponent Default => new SlideComponent
        {
            Duration = 1.5f,
            MinSpeed = 3.0f,
            MaxSpeed = 12.0f,
            Acceleration = 8.0f,
            Friction = 2.0f,
            StaminaCost = 5.0f,
            Cooldown = 1.0f,
            MinSlopeAngle = 15.0f,
            SlipperyFrictionMultiplier = 0.1f
        };
    }

    /// <summary>
    /// Trigger type for slide - tracks how the slide was initiated
    /// </summary>
    public enum SlideTriggerType : byte
    {
        Manual = 0,      // Player pressed slide button
        Slope = 1,       // Auto-triggered on steep slope
        Slippery = 2     // Auto-triggered on slippery surface
    }

    /// <summary>
    /// Runtime state for an active slide.
    /// Replicated across network for multiplayer prediction.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct SlideState : IComponentData
    {
        /// <summary>Is the player currently sliding?</summary>
        [GhostField] public bool IsSliding;
        
        /// <summary>Time elapsed since slide started (seconds)</summary>
        [GhostField] public float SlideProgress;
        
        /// <summary>Current slide speed (m/s)</summary>
        [GhostField] public float CurrentSpeed;
        
        /// <summary>Direction of slide (world space, normalized)</summary>
        [GhostField] public float3 SlideDirection;
        
        /// <summary>How was this slide triggered?</summary>
        [GhostField] public SlideTriggerType TriggerType;
        
        /// <summary>Network tick when slide started (for prediction)</summary>
        [GhostField] public uint StartTick;
        
        /// <summary>Cooldown timer remaining (seconds)</summary>
        [GhostField] public float CooldownRemaining;
        
        /// <summary>Maximum duration for this slide instance</summary>
        public float Duration;
        
        /// <summary>Cached slide settings (copied from SlideComponent at start)</summary>
        public float MinSpeed;
        public float MaxSpeed;
        public float Acceleration;
        public float Friction;
    }
}
