using UnityEngine;
using Unity.Entities;
using DIG.Player;

namespace DIG.Player.Authoring
{
    /// <summary>
    /// Adds optional survival mechanics (hunger, thirst, oxygen) to an entity.
    /// Add this to the player prefab for survival game modes.
    /// </summary>
    [AddComponentMenu("DIG/Player/Survival Needs Authoring")]
    public class SurvivalNeedsAuthoring : MonoBehaviour
    {
        [Header("Feature Toggles")]
        [Tooltip("Enable hunger system")]
        public bool EnableHunger = true;
        
        [Tooltip("Enable thirst system")]
        public bool EnableThirst = true;
        
        [Tooltip("Enable oxygen system (for underwater/hazard zones)")]
        public bool EnableOxygen = true;
        
        [Header("Hunger Settings")]
        [Tooltip("Maximum hunger value (0 = full, max = starving)")]
        public float MaxHunger = 100f;
        
        [Tooltip("Starting hunger value")]
        public float StartingHunger = 0f;
        
        [Tooltip("Rate hunger increases per second")]
        public float HungerIncreaseRate = 0.1f;
        
        [Tooltip("Damage per second when fully starving")]
        public float StarvationDamage = 2f;
        
        [Tooltip("Hunger threshold to start taking damage (0-1)")]
        [Range(0f, 1f)]
        public float StarvationThreshold = 0.9f;
        
        [Header("Thirst Settings")]
        [Tooltip("Maximum thirst value (0 = hydrated, max = dehydrated)")]
        public float MaxThirst = 100f;
        
        [Tooltip("Starting thirst value")]
        public float StartingThirst = 0f;
        
        [Tooltip("Rate thirst increases per second")]
        public float ThirstIncreaseRate = 0.15f;
        
        [Tooltip("Damage per second when fully dehydrated")]
        public float DehydrationDamage = 3f;
        
        [Tooltip("Thirst threshold to start taking damage (0-1)")]
        [Range(0f, 1f)]
        public float DehydrationThreshold = 0.9f;
        
        [Header("Oxygen Settings")]
        [Tooltip("Maximum oxygen in seconds")]
        public float MaxOxygen = 60f;
        
        [Tooltip("Starting oxygen (usually full)")]
        public float StartingOxygen = 60f;
        
        [Tooltip("Rate oxygen drains per second when underwater/in hazard")]
        public float OxygenDrainRate = 1f;
        
        [Tooltip("Rate oxygen recovers per second when safe")]
        public float OxygenRecoveryRate = 5f;
        
        [Tooltip("Damage per second when out of oxygen")]
        public float SuffocationDamage = 10f;

        class Baker : Baker<SurvivalNeedsAuthoring>
        {
            public override void Bake(SurvivalNeedsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                if (authoring.EnableHunger)
                {
                    AddComponent(entity, new PlayerHunger
                    {
                        Current = authoring.StartingHunger,
                        Max = authoring.MaxHunger,
                        IncreaseRate = authoring.HungerIncreaseRate,
                        StarvationDamage = authoring.StarvationDamage,
                        StarvationThreshold = authoring.StarvationThreshold
                    });
                }
                
                if (authoring.EnableThirst)
                {
                    AddComponent(entity, new PlayerThirst
                    {
                        Current = authoring.StartingThirst,
                        Max = authoring.MaxThirst,
                        IncreaseRate = authoring.ThirstIncreaseRate,
                        DehydrationDamage = authoring.DehydrationDamage,
                        DehydrationThreshold = authoring.DehydrationThreshold
                    });
                }
                
                if (authoring.EnableOxygen)
                {
                    AddComponent(entity, new PlayerOxygen
                    {
                        Current = authoring.StartingOxygen,
                        Max = authoring.MaxOxygen,
                        DrainRate = authoring.OxygenDrainRate,
                        RecoveryRate = authoring.OxygenRecoveryRate,
                        SuffocationDamage = authoring.SuffocationDamage
                    });
                }
            }
        }
    }
}
