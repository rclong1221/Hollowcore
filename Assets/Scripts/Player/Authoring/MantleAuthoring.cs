using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for mantle and vault settings.
    /// Add to player prefab to configure traversal mechanics.
    /// </summary>
    public class MantleAuthoring : MonoBehaviour
    {
        [Header("Height Limits")]
        [Tooltip("Maximum height player can mantle while standing (meters)")]
        public float MaxMantleHeightStanding = 2.0f;
        
        [Tooltip("Maximum height player can mantle while crouching (meters)")]
        public float MaxMantleHeightCrouching = 1.0f;
        
        [Tooltip("Maximum height for automatic vaulting (meters)")]
        public float MaxVaultHeight = 1.2f;
        
        [Header("Detection")]
        [Tooltip("Forward reach distance to detect ledges (meters)")]
        public float MantleReachDistance = 0.5f;
        
        [Tooltip("Minimum ledge width required for safe mantling (meters)")]
        public float MinLedgeWidth = 0.3f;
        
        [Header("Timing")]
        [Tooltip("Duration of mantle animation/interpolation (seconds)")]
        public float MantleDuration = 0.5f;
        
        [Tooltip("Duration of vault animation/interpolation (seconds)")]
        public float VaultDuration = 0.4f;
        
        [Header("Stamina & Cooldown")]
        [Tooltip("Stamina cost for mantling")]
        public float MantleStaminaCost = 15f;
        
        [Tooltip("Stamina cost for vaulting")]
        public float VaultStaminaCost = 10f;
        
        [Tooltip("Cooldown between mantle attempts (seconds)")]
        public float MantleCooldown = 0.5f;

        class Baker : Baker<MantleAuthoring>
        {
            public override void Bake(MantleAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new MantleSettings
                {
                    MaxMantleHeightStanding = authoring.MaxMantleHeightStanding,
                    MaxMantleHeightCrouching = authoring.MaxMantleHeightCrouching,
                    MaxVaultHeight = authoring.MaxVaultHeight,
                    MantleReachDistance = authoring.MantleReachDistance,
                    MinLedgeWidth = authoring.MinLedgeWidth,
                    MantleDuration = authoring.MantleDuration,
                    VaultDuration = authoring.VaultDuration,
                    MantleStaminaCost = authoring.MantleStaminaCost,
                    VaultStaminaCost = authoring.VaultStaminaCost,
                    MantleCooldown = authoring.MantleCooldown
                });
                
                AddComponent(entity, new MantleState
                {
                    IsActive = 0,
                    Progress = 0f,
                    Elapsed = 0f,
                    Duration = authoring.MantleDuration,
                    CooldownRemaining = 0f
                });
            }
        }
    }
}
