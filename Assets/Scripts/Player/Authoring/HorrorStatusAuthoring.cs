using UnityEngine;
using Unity.Entities;
using DIG.Player;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Adds optional horror mechanics (sanity, infection) to an entity.
    /// Add this to the player prefab for horror game modes.
    /// </summary>
    [AddComponentMenu("DIG/Player/Horror Status Authoring")]
    public class HorrorStatusAuthoring : MonoBehaviour
    {
        [Header("Feature Toggles")]
        [Tooltip("Enable sanity system")]
        public bool EnableSanity = true;
        
        [Tooltip("Enable infection system")]
        public bool EnableInfection = true;
        
        [Header("Sanity Settings")]
        [Tooltip("Maximum sanity value")]
        public float MaxSanity = 100f;
        
        [Tooltip("Starting sanity (usually full)")]
        public float StartingSanity = 100f;
        
        [Tooltip("Base drain rate in darkness per second")]
        public float DarknessDrainRate = 0.5f;
        
        [Tooltip("Drain rate when near horror entities per second")]
        public float HorrorDrainRate = 2f;
        
        [Tooltip("Recovery rate in safe areas per second")]
        public float RecoveryRate = 1f;
        
        [Tooltip("Threshold for visual distortions to begin (0-1)")]
        [Range(0f, 1f)]
        public float DistortionThreshold = 0.5f;
        
        [Tooltip("Threshold for hallucinations to begin (0-1)")]
        [Range(0f, 1f)]
        public float HallucinationThreshold = 0.25f;
        
        [Header("Infection Settings")]
        [Tooltip("Maximum infection value (death at max)")]
        public float MaxInfection = 100f;
        
        [Tooltip("Starting infection (usually 0)")]
        public float StartingInfection = 0f;
        
        [Tooltip("Base spread rate per second once infected")]
        public float SpreadRate = 0.1f;
        
        [Tooltip("Damage per second from infection")]
        public float DamageRate = 0.5f;
        
        [Tooltip("Infection level threshold to start taking damage (0-1)")]
        [Range(0f, 1f)]
        public float DamageThreshold = 0.3f;

        class Baker : Baker<HorrorStatusAuthoring>
        {
            public override void Bake(HorrorStatusAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                if (authoring.EnableSanity)
                {
                    AddComponent(entity, new PlayerSanity
                    {
                        Current = authoring.StartingSanity,
                        Max = authoring.MaxSanity,
                        DarknessDrainRate = authoring.DarknessDrainRate,
                        HorrorDrainRate = authoring.HorrorDrainRate,
                        RecoveryRate = authoring.RecoveryRate,
                        DistortionThreshold = authoring.DistortionThreshold,
                        HallucinationThreshold = authoring.HallucinationThreshold,
                        DistortionIntensity = 0f
                    });
                }
                
                if (authoring.EnableInfection)
                {
                    AddComponent(entity, new PlayerInfection
                    {
                        Current = authoring.StartingInfection,
                        Max = authoring.MaxInfection,
                        SpreadRate = authoring.SpreadRate,
                        DamageRate = authoring.DamageRate,
                        DamageThreshold = authoring.DamageThreshold
                    });
                }
            }
        }
    }
}
