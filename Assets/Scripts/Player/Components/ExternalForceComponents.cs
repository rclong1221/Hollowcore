using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Defines an external force zone (wind, conveyor belt, etc.).
    /// Entities with triggers that contain this component will push players.
    /// <para>
    /// <b>Architecture:</b> Zone detection runs server-side.
    /// Force application is predicted for smooth client experience.
    /// </para>
    /// <para><b>Performance:</b> Uses byte instead of bool for Burst blittability.</para>
    /// </summary>
    public struct ExternalForceZone : IComponentData
    {
        /// <summary>Force vector (world space for directional, magnitude for radial).</summary>
        public float3 Force;
        
        /// <summary>How quickly force decays when leaving the zone (per second).</summary>
        public float ExitDamping;
        
        /// <summary>1 = directional (push in Force direction), 0 = radial (push from Center).</summary>
        public byte IsDirectional;
        
        /// <summary>Center point for radial forces (world space). Ignored if IsDirectional = 1.</summary>
        public float3 Center;
        
        /// <summary>1 = continuous (every frame), 0 = impulse (once on entry).</summary>
        public byte IsContinuous;
        
        /// <summary>Priority for overlapping zones. Higher priority overrides lower.</summary>
        public int Priority;
    }
    
    /// <summary>
    /// Buffer of external forces currently affecting the player.
    /// Multiple forces can stack (e.g., wind + explosion).
    /// </summary>
    public struct ExternalForceElement : IBufferElementData
    {
        /// <summary>Unique ID of the force source (entity index or hash).</summary>
        public int SourceId;
        
        /// <summary>Current force being applied.</summary>
        public float3 Force;
        
        /// <summary>How quickly this force decays (per second).</summary>
        public float Decay;
        
        /// <summary>Remaining frames for soft force distribution (0 = apply immediately).</summary>
        public int FramesRemaining;
        
        /// <summary>Force mode: 0 = Continuous, 1 = Impulse</summary>
        public byte ForceMode;
    }
    
    /// <summary>
    /// Accumulated external force state on the player.
    /// </summary>
    public struct ExternalForceState : IComponentData
    {
        /// <summary>Combined force from all active sources.</summary>
        [GhostField(Quantization = 100)] public float3 AccumulatedForce;
        
        /// <summary>Current damping rate being applied.</summary>
        public float CurrentDamping;
        
        /// <summary>Whether player is currently in any force zone. 1 = true, 0 = false.</summary>
        public byte IsInForceZone;
        
        /// <summary>Entity of the highest-priority active force zone.</summary>
        public Entity ActiveZoneEntity;
    }
    
    /// <summary>
    /// Settings for external force handling.
    /// </summary>
    public struct ExternalForceSettings : IComponentData
    {
        /// <summary>Maximum external force magnitude.</summary>
        public float MaxForceMagnitude;
        
        /// <summary>Default decay rate when no zone specifies one.</summary>
        public float DefaultDecay;
        
        /// <summary>Multiplier for force resistance (e.g., heavy characters resist more).</summary>
        public float ForceResistance;
        
        /// <summary>Number of frames to distribute impulses across.</summary>
        public int SoftForceFrames;
        
        public static ExternalForceSettings Default => new ExternalForceSettings
        {
            MaxForceMagnitude = 50f,
            DefaultDecay = 5f,
            ForceResistance = 1.0f,
            SoftForceFrames = 10
        };
    }
    
    /// <summary>
    /// Request to add an external force (e.g., from explosion system).
    /// </summary>
    public struct AddExternalForceRequest : IComponentData, IEnableableComponent
    {
        /// <summary>Force to add.</summary>
        public float3 Force;
        
        /// <summary>Number of frames to distribute force across (0 = instant).</summary>
        public int SoftFrames;
        
        /// <summary>How quickly the force decays.</summary>
        public float Decay;
        
        /// <summary>Unique source ID to prevent duplicate forces.</summary>
        public int SourceId;
    }
    
    /// <summary>
    /// Event raised when entering/exiting a force zone.
    /// </summary>
    public struct ForceZoneEvent : IBufferElementData
    {
        /// <summary>Zone entity.</summary>
        public Entity ZoneEntity;
        
        /// <summary>1 = entered, 0 = exited.</summary>
        public byte Entered;
        
        /// <summary>Tick when event occurred.</summary>
        public uint Tick;
    }
}
