using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Combat.Resolvers;

namespace DIG.Combat.Authoring
{
    /// <summary>
    /// Authoring component for entities that can take damage.
    /// Adds HealthComponent during baking.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthAuthoring : MonoBehaviour
    {
        [Header("Health Settings")]
        [Tooltip("Maximum health points")]
        public float MaxHealth = 100f;
        
        [Tooltip("Starting health (defaults to max if 0)")]
        public float StartingHealth = 0f;
        
        public class Baker : Baker<HealthAuthoring>
        {
            public override void Bake(HealthAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                float startHealth = authoring.StartingHealth > 0 
                    ? authoring.StartingHealth 
                    : authoring.MaxHealth;
                
                AddComponent(entity, new Systems.HealthComponent
                {
                    MaxHealth = authoring.MaxHealth,
                    CurrentHealth = startHealth
                });
            }
        }
    }
}
