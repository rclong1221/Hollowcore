using Unity.Entities;
using Unity.Physics;
using Unity.Collections;
using Player.Components;
using DIG.Player.Components;

namespace Player.Systems 
{
    // Tag component to mark entities that have been processed
    public struct PlayerChildrenLayersEnforced : IComponentData {}

    /// <summary>
    /// Ensures all child entities of the player (like visual meshes with colliders) 
    /// are assigned to the Player collision layer.
    /// This prevents them from being detected as obstructions by the climbing system,
    /// which expects Default layer objects to be valid walls/obstacles.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayerChildLayerEnforcementSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<PlayerTag>();
        }

        protected override void OnUpdate()
        {
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

            foreach (var (linkedGroup, entity) in SystemAPI.Query<DynamicBuffer<LinkedEntityGroup>>()
                .WithAll<PlayerTag>()
                .WithNone<PlayerChildrenLayersEnforced>()
                .WithEntityAccess())
            {
                // Iterate through all linked entities (children)
                for (int i = 0; i < linkedGroup.Length; i++)
                {
                    var childEntity = linkedGroup[i].Value;
                    
                    // Skip the root entity itself (it typically has the correct layer from Authoring)
                    if (childEntity == entity) continue;

                    if (SystemAPI.HasComponent<PhysicsCollider>(childEntity))
                    {
                        var collider = SystemAPI.GetComponent<PhysicsCollider>(childEntity);
                        
                        // Check if collider is valid
                        if (!collider.IsValid) continue;

                        var filter = collider.Value.Value.GetCollisionFilter();
                        
                        // STRICT check: Must be ONLY Player layer.
                        if (filter.BelongsTo != CollisionLayers.Player)
                        {
                            uint oldBelongsTo = filter.BelongsTo;
                            
                            // FORCE to Player layer only.
                            filter.BelongsTo = CollisionLayers.Player;
                            
                            // Clone and set new filter
                            var uniqueColliderBlob = collider.Value.Value.Clone();
                            uniqueColliderBlob.Value.SetCollisionFilter(filter);
                            SystemAPI.SetComponent(childEntity, collider);
                            
                            // UnityEngine.Debug.Log($"[PlayerChildLayerEnforcement] FIXED child {childEntity} (Index {i}) of Player {entity}. Layer changed from {oldBelongsTo:X8} to {filter.BelongsTo:X8} (Player).");
                        }
                    }
                }
                
                // UnityEngine.Debug.Log($"[PlayerChildLayerEnforcement] Processed Player {entity} with {linkedGroup.Length} linked entities.");
                
                // Mark as processed so we don't check this player again
                ecb.AddComponent<PlayerChildrenLayersEnforced>(entity);
            }
        }
    }
}
