using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace Player.Authoring
{
    /// <summary>
    /// Simple authoring component to mark objects as static DOTS physics colliders.
    /// Used by TraversalObjectCreator editor script.
    /// Creates colliders based on the GameObject's primitive type for better collision.
    /// </summary>
    public class StaticPhysicsObjectAuthoring : MonoBehaviour
    {
        [Tooltip("Collision filter category bits")]
        public uint belongsTo = 1u;
        
        [Tooltip("Collision filter mask - what this collides with")]
        public uint collidesWith = ~0u;
        
        class Baker : Baker<StaticPhysicsObjectAuthoring>
        {
            public override void Bake(StaticPhysicsObjectAuthoring authoring)
            {
                // If the object already has a Unity Collider (BoxCollider, SphereCollider, etc.), 
                // the standard Unity Physics bakers will add a PhysicsCollider.
                // We should skip adding ours to avoid "duplicate component" errors.
                if (GetComponent<UnityEngine.Collider>() != null)
                {
                    return;
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                var filter = new CollisionFilter
                {
                    BelongsTo = authoring.belongsTo,
                    CollidesWith = authoring.collidesWith,
                    GroupIndex = 0
                };
                
                // Get mesh filter to determine primitive type
                var meshFilter = GetComponent<MeshFilter>();
                if (meshFilter == null)
                    return;
                
                BlobAssetReference<Unity.Physics.Collider> collider;
                var scale = authoring.transform.localScale;
                var meshName = meshFilter.sharedMesh?.name;
                
                // Create appropriate collider based on primitive type
                if (meshName != null && meshName.Contains("Cube"))
                {
                    // Box collider for cubes
                    collider = Unity.Physics.BoxCollider.Create(
                        new BoxGeometry
                        {
                            Center = Unity.Mathematics.float3.zero,
                            Orientation = Unity.Mathematics.quaternion.identity,
                            Size = new Unity.Mathematics.float3(scale.x, scale.y, scale.z),
                            BevelRadius = 0.0f
                        },
                        filter
                    );
                }
                else if (meshName != null && meshName.Contains("Cylinder"))
                {
                    // Capsule collider for cylinders (better side collision)
                    float height = scale.y * 2f; // Cylinder primitive is 2 units tall
                    float radius = scale.x * 0.5f; // Cylinder primitive is 1 unit diameter
                    
                    collider = Unity.Physics.CapsuleCollider.Create(
                        new CapsuleGeometry
                        {
                            Vertex0 = new Unity.Mathematics.float3(0, -height * 0.5f, 0),
                            Vertex1 = new Unity.Mathematics.float3(0, height * 0.5f, 0),
                            Radius = radius
                        },
                        filter
                    );
                }
                else if (meshName != null && meshName.Contains("Sphere"))
                {
                    // Sphere collider
                    float radius = scale.x * 0.5f;
                    collider = Unity.Physics.SphereCollider.Create(
                        new SphereGeometry
                        {
                            Center = Unity.Mathematics.float3.zero,
                            Radius = radius
                        },
                        filter
                    );
                }
                else
                {
                    // Default to box collider
                    collider = Unity.Physics.BoxCollider.Create(
                        new BoxGeometry
                        {
                            Center = Unity.Mathematics.float3.zero,
                            Orientation = Unity.Mathematics.quaternion.identity,
                            Size = new Unity.Mathematics.float3(scale.x, scale.y, scale.z),
                            BevelRadius = 0.0f
                        },
                        filter
                    );
                }
                
                // Add the physics collider component
                AddBlobAsset(ref collider, out var hash);
                AddComponent(entity, new PhysicsCollider { Value = collider });
            }
        }
    }
}
