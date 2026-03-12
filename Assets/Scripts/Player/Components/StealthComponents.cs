using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tracks the current noise level emitted by the player.
    /// Used by AI and Stealth mechanics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PlayerNoiseStatus : IComponentData
    {
        [GhostField] public float CurrentNoiseLevel;
        [GhostField] public bool IsEmittingNoise;
        [GhostField] public float3 LastNoisePosition;
    }

    /// <summary>
    /// Configuration for player noise generation logic.
    /// </summary>
    public struct StealthSettings : IComponentData
    {
        public float WalkNoiseRadius;
        public float RunNoiseRadius;
        public float SprintNoiseRadius;
        public float CrouchNoiseMultiplier; // e.g., 0.5 or 0.0 for silent
        public float ProneNoiseMultiplier;  // e.g., 0.2
        public float SpeedThreshold;        // Minimum speed to generate noise
        
        public static StealthSettings Default => new StealthSettings
        {
            WalkNoiseRadius = 5.0f,
            RunNoiseRadius = 10.0f,
            SprintNoiseRadius = 20.0f,
            CrouchNoiseMultiplier = 0.0f, // Silent crouching
            ProneNoiseMultiplier = 0.0f,  // Silent prone
            SpeedThreshold = 0.1f
        };
    }
    
    /// <summary>
    /// Transient tag to indicate a significant noise event occurred this frame.
    /// Useful for triggering one-shot reactions/UI.
    /// </summary>
    public struct NoiseEventTag : IComponentData, IEnableableComponent { }
}
