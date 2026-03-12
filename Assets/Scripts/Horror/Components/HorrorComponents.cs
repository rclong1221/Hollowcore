using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;

namespace Horror.Components
{
    /// <summary>
    /// Types of horror events that can occur.
    /// </summary>
    public enum HorrorEventType : byte
    {
        /// <summary>Lights flicker off briefly (visible to all)</summary>
        LightFlicker = 0,
        
        /// <summary>Phantom footsteps behind player (private hallucination)</summary>
        PhantomFootsteps = 1,
        
        /// <summary>Whispers from nowhere (private hallucination)</summary>
        Whispers = 2,
        
        /// <summary>Fake radar blip (private hallucination)</summary>
        RadarGhost = 3,
        
        /// <summary>Steam/vent burst effect (visible to all)</summary>
        VentBurst = 4,
        
        /// <summary>Brief visual distortion (private hallucination)</summary>
        VisualDistortion = 5
    }

    /// <summary>
    /// Singleton that controls horror event frequency and global tension.
    /// Exists on server and is replicated to clients.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.Server)]
    public struct HorrorDirector : IComponentData
    {
        /// <summary>
        /// Global tension level (0-1). Increases over mission time.
        /// Higher tension = more frequent events.
        /// </summary>
        [GhostField(Quantization = 1000)]
        public float GlobalTension;
        
        /// <summary>
        /// Time since last global horror event.
        /// </summary>
        public float TimeSinceLastEvent;
        
        /// <summary>
        /// Mission elapsed time in seconds.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float MissionTime;
        
        /// <summary>
        /// Configuration: How quickly tension builds (per second).
        /// </summary>
        public float TensionBuildRate;
        
        /// <summary>
        /// Configuration: Minimum time between global events.
        /// </summary>
        public float MinEventCooldown;
        
        /// <summary>
        /// Configuration: Maximum time between global events (at max tension).
        /// </summary>
        public float MaxEventCooldown;
        
        /// <summary>
        /// RNG seed for deterministic random events (for NetCode prediction).
        /// </summary>
        [GhostField]
        public uint RandomSeed;
    }

    /// <summary>
    /// Request for a horror event. Created by systems, processed by presentation layer.
    /// </summary>
    public struct HorrorEventRequest : IComponentData
    {
        /// <summary>Type of horror event</summary>
        public HorrorEventType EventType;
        
        /// <summary>Intensity of the event (0-1)</summary>
        public float Intensity;
        
        /// <summary>Duration in seconds (0 = instant)</summary>
        public float Duration;
        
        /// <summary>For spatial events, the position</summary>
        public Unity.Mathematics.float3 Position;
        
        /// <summary>For player-specific events, the target player</summary>
        public Entity TargetPlayer;
        
        /// <summary>Is this a private hallucination (only target sees it)?</summary>
        public bool IsPrivate;
    }

    /// <summary>
    /// Tracks hallucination state for a player.
    /// Only exists on clients for the local player.
    /// </summary>
    public struct PlayerHallucinationState : IComponentData
    {
        /// <summary>Time since last private hallucination</summary>
        public float TimeSinceLastHallucination;
        
        /// <summary>Current hallucination intensity (builds with stress)</summary>
        public float HallucinationIntensity;
        
        /// <summary>Is player currently experiencing a hallucination?</summary>
        public bool IsHallucinating;
        
        /// <summary>Type of current hallucination (if active)</summary>
        public HorrorEventType CurrentHallucinationType;
        
        /// <summary>Time remaining on current hallucination</summary>
        public float HallucinationTimeRemaining;
    }

    /// <summary>
    /// Configuration singleton for horror system parameters.
    /// </summary>
    public struct HorrorSettings : IComponentData
    {
        /// <summary>Stress threshold to start hallucinations (0-1)</summary>
        public float HallucinationThreshold;
        
        /// <summary>Minimum time between hallucinations (seconds)</summary>
        public float MinHallucinationCooldown;
        
        /// <summary>Maximum hallucination duration (seconds)</summary>
        public float MaxHallucinationDuration;
        
        /// <summary>Base probability of hallucination per second at max stress</summary>
        public float HallucinationProbabilityPerSecond;
        
        /// <summary>Light flicker duration range (min)</summary>
        public float FlickerDurationMin;
        
        /// <summary>Light flicker duration range (max)</summary>
        public float FlickerDurationMax;
        
        public static HorrorSettings Default => new HorrorSettings
        {
            HallucinationThreshold = 0.5f,
            MinHallucinationCooldown = 10f,
            MaxHallucinationDuration = 3f,
            HallucinationProbabilityPerSecond = 0.15f,
            FlickerDurationMin = 0.1f,
            FlickerDurationMax = 0.5f
        };
    }
}
