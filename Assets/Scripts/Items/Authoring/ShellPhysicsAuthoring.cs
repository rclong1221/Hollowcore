using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Alias to avoid ambiguity with UnityEngine types
using PhysicsMass = Unity.Physics.PhysicsMass;
using PhysicsVelocity = Unity.Physics.PhysicsVelocity;
using PhysicsDamping = Unity.Physics.PhysicsDamping;
using PhysicsGravityFactor = Unity.Physics.PhysicsGravityFactor;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Minimal physics authoring for shell casings.
    /// Adds dynamic physics properties. Use with a standard Unity Collider 
    /// (BoxCollider, CapsuleCollider, etc.) which Unity will automatically bake.
    /// </summary>
    public class ShellPhysicsAuthoring : MonoBehaviour
    {
        [Header("Physics Body")]
        [Tooltip("Mass in kg. Shells are typically very light (0.01 - 0.05)")]
        public float Mass = 0.02f;
        
        [Tooltip("Linear damping (air resistance). Higher = slows down faster.")]
        public float LinearDamping = 0.1f;
        
        [Tooltip("Angular damping (spin resistance). Higher = stops spinning faster.")]
        public float AngularDamping = 0.1f;
        
        public class Baker : Baker<ShellPhysicsAuthoring>
        {
            public override void Bake(ShellPhysicsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Add physics velocity (starts at zero, ShellSpawnSystem will set it)
                AddComponent(entity, new PhysicsVelocity());
                
                // Add physics mass (dynamic body)
                // Use a default unit sphere mass properties - actual shape comes from collider
                var massProperties = Unity.Physics.MassProperties.UnitSphere;
                AddComponent(entity, PhysicsMass.CreateDynamic(massProperties, authoring.Mass));
                
                // Add physics damping
                AddComponent(entity, new PhysicsDamping
                {
                    Linear = authoring.LinearDamping,
                    Angular = authoring.AngularDamping
                });
                
                // Add gravity factor (1 = normal gravity)
                AddComponent(entity, new PhysicsGravityFactor { Value = 1f });
            }
        }
    }
}
