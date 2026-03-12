using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19: Per-entity configuration for threat behavior.
    /// Allows different enemy types to have different aggro characteristics.
    /// </summary>
    public struct AggroConfig : IComponentData
    {
        // === Threat Multipliers ===
        
        /// <summary>Multiplier applied to damage for threat calculation. Default 1.0</summary>
        public float DamageThreatMultiplier;
        
        /// <summary>Base threat added when first seeing a target (sight aggro). Default 5.0</summary>
        public float SightThreatValue;
        
        /// <summary>Base threat added when hearing a target (future). Default 3.0</summary>
        public float HearingThreatValue;
        
        // === Decay Settings ===
        
        /// <summary>Threat reduction per second for visible targets. Default 1.0</summary>
        public float VisibleDecayRate;
        
        /// <summary>Threat reduction per second for non-visible targets. Default 5.0</summary>
        public float HiddenDecayRate;
        
        /// <summary>Time before forgetting a hidden target entirely (seconds). Default 30.0</summary>
        public float MemoryDuration;
        
        // === Target Selection ===
        
        /// <summary>Only switch targets if new threat exceeds current by this ratio. Default 1.1 (110%)</summary>
        public float HysteresisRatio;
        
        /// <summary>Maximum number of targets to track. Default 8</summary>
        public int MaxTrackedTargets;
        
        /// <summary>Minimum threat to remain in table. Default 0.1</summary>
        public float MinimumThreat;
        
        // === Leashing & Territory ===
        
        /// <summary>
        /// Maximum distance from spawn/home position before dropping aggro and returning.
        /// Set to 0 for no leash (boss fights, endless chase). Default 50m.
        /// </summary>
        public float LeashDistance;
        
        // === Social Behavior ===
        
        /// <summary>
        /// Radius within which to alert nearby allies when aggroed.
        /// Set to 0 to disable aggro sharing (lone wolves). Default 20m.
        /// </summary>
        public float AggroShareRadius;
        
        /// <summary>
        /// Multiplier applied to detection range when already suspicious/alert.
        /// 1.0 = no change, 1.5 = 50% better detection. Default 1.5.
        /// </summary>
        public float AlertStateMultiplier;

        // === EPIC 15.33: Proximity Threat ===

        /// <summary>
        /// Radius for 360-degree proximity threat (body pull). 0 = disabled.
        /// Entities within this radius generate threat without LOS.
        /// </summary>
        public float ProximityThreatRadius;

        /// <summary>Threat added per second while a target is within ProximityThreatRadius.</summary>
        public float ProximityThreatPerSecond;

        // === EPIC 15.33: Target Selection ===

        /// <summary>How the target selector picks from the threat table. Default HighestThreat.</summary>
        public TargetSelectionMode SelectionMode;

        /// <summary>Weight for distance factor in WeightedScore mode. 0 = ignore distance.</summary>
        public float DistanceWeight;

        /// <summary>Weight for health factor in WeightedScore mode. 0 = ignore health.</summary>
        public float HealthWeight;

        /// <summary>Weight for recency factor in WeightedScore mode. 0 = ignore recency.</summary>
        public float RecencyWeight;

        /// <summary>Minimum seconds between target switches. 0 = no cooldown.</summary>
        public float TargetSwitchCooldown;

        /// <summary>Per-second probability of random target switch. 0 = deterministic.</summary>
        public float RandomSwitchChance;

        /// <summary>Creates default aggro configuration with human-like values.</summary>
        public static AggroConfig Default => new AggroConfig
        {
            DamageThreatMultiplier = 1.0f,
            SightThreatValue = 10.0f,
            HearingThreatValue = 5.0f,
            VisibleDecayRate = 0.5f,
            HiddenDecayRate = 0.5f,
            MemoryDuration = 30.0f,
            HysteresisRatio = 1.1f,
            MaxTrackedTargets = 8,
            MinimumThreat = 0.1f,
            LeashDistance = 50.0f,
            AggroShareRadius = 20.0f,
            AlertStateMultiplier = 1.5f,
            ProximityThreatRadius = 0f,
            ProximityThreatPerSecond = 5.0f,
            SelectionMode = TargetSelectionMode.HighestThreat,
            DistanceWeight = 0f,
            HealthWeight = 0f,
            RecencyWeight = 0f,
            TargetSwitchCooldown = 0f,
            RandomSwitchChance = 0f
        };
    }
}
