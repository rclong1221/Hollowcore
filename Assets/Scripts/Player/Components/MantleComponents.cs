using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Configuration for mantling and vaulting mechanics.
    /// Defines height limits, reach distances, and validation parameters.
    /// </summary>
    public struct MantleSettings : IComponentData
    {
        /// <summary>Maximum height player can mantle while standing (meters)</summary>
        public float MaxMantleHeightStanding;
        
        /// <summary>Maximum height player can mantle while crouching (meters)</summary>
        public float MaxMantleHeightCrouching;
        
        /// <summary>Maximum height for automatic vaulting (meters)</summary>
        public float MaxVaultHeight;
        
        /// <summary>Forward reach distance to detect ledges (meters)</summary>
        public float MantleReachDistance;
        
        /// <summary>Minimum ledge width required for safe mantling (meters)</summary>
        public float MinLedgeWidth;
        
        /// <summary>Duration of mantle animation/interpolation (seconds)</summary>
        public float MantleDuration;
        
        /// <summary>Duration of vault animation/interpolation (seconds)</summary>
        public float VaultDuration;
        
        /// <summary>Stamina cost for mantling</summary>
        public float MantleStaminaCost;
        
        /// <summary>Stamina cost for vaulting</summary>
        public float VaultStaminaCost;
        
        /// <summary>Cooldown between mantle attempts (seconds)</summary>
        public float MantleCooldown;
        
        public static MantleSettings Default => new MantleSettings
        {
            MaxMantleHeightStanding = 2.0f,
            MaxMantleHeightCrouching = 1.0f,
            MaxVaultHeight = 1.2f,
            MantleReachDistance = 0.5f,
            MinLedgeWidth = 0.3f,
            MantleDuration = 0.5f,
            VaultDuration = 0.4f,
            MantleStaminaCost = 15f,
            VaultStaminaCost = 10f,
            MantleCooldown = 0.5f
        };
    }
    
    /// <summary>
    /// Runtime state for an active mantle or vault.
    /// Tracks progress, positions, and whether action is currently executing.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct MantleState : IComponentData
    {
        /// <summary>Is player currently mantling or vaulting (0 = no, 1 = mantling, 2 = vaulting)</summary>
        [GhostField] public byte IsActive;
        
        /// <summary>Progress through mantle/vault animation [0-1]</summary>
        [GhostField] public float Progress;
        
        /// <summary>Time elapsed since mantle/vault started (seconds)</summary>
        [GhostField] public float Elapsed;
        
        /// <summary>Total duration for this mantle/vault (seconds)</summary>
        [GhostField] public float Duration;
        
        /// <summary>World position where mantle/vault started</summary>
        [GhostField] public float3 StartPosition;
        
        /// <summary>World position where mantle/vault will end (ledge top)</summary>
        [GhostField] public float3 EndPosition;
        
        /// <summary>Forward direction for vault movement</summary>
        [GhostField] public float3 VaultDirection;
        
        /// <summary>Height of the obstacle being mantled/vaulted</summary>
        [GhostField] public float ObstacleHeight;
        
        /// <summary>Cooldown remaining before next mantle allowed (seconds)</summary>
        [GhostField] public float CooldownRemaining;
        
        /// <summary>Network tick when mantle started (for prediction)</summary>
        [GhostField] public uint StartTick;
    }
    
    /// <summary>
    /// Tag component marking a valid detected mantle opportunity.
    /// Added by MantleDetectionSystem, consumed by MantleExecutionSystem.
    /// </summary>
    public struct MantleCandidate : IComponentData
    {
        public float3 LedgePosition;
        public float3 LedgeNormal;
        public float LedgeHeight;
        public float LedgeWidth;
        public bool IsVault; // true if this should be a vault instead of mantle
    }
}
