using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace DIG.Ship.LocalSpace
{
    /// <summary>
    /// Updates LocalToWorld for ship interior entities before physics runs.
    /// This ensures collision detection uses current ship positions, not stale ones.
    /// 
    /// Without this system, ship interior colliders (walls, floor) stay at their
    /// baked positions because TransformSystemGroup runs after physics.
    /// </summary>
    [UpdateInGroup(typeof(PhysicsSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(Unity.Physics.Systems.PhysicsInitializeGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ShipInteriorTransformUpdateSystem : ISystem
    {
        private EntityQuery _shipQuery;
        
        public void OnCreate(ref SystemState state)
        {
            _shipQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<ShipRoot>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            state.RequireForUpdate(_shipQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Complete previous jobs that may be reading/writing LocalToWorld
            state.Dependency.Complete();
            
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false);
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
            var shipRootLookup = SystemAPI.GetComponentLookup<ShipRoot>(true);
            
            // First pass: Update all ship roots' LocalToWorld from their LocalTransform
            foreach (var (shipTransform, shipEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                     .WithAll<ShipRoot>()
                     .WithEntityAccess())
            {
                if (!localToWorldLookup.HasComponent(shipEntity))
                    continue;
                    
                var shipWorldMatrix = float4x4.TRS(
                    shipTransform.ValueRO.Position,
                    shipTransform.ValueRO.Rotation,
                    new float3(shipTransform.ValueRO.Scale)
                );
                
                localToWorldLookup[shipEntity] = new LocalToWorld { Value = shipWorldMatrix };
            }
            
            // Second pass: Update all entities that have a Parent component
            // Walk up the parent chain to find if they belong to a ship
            foreach (var (parent, localTransform, entity) in
                     SystemAPI.Query<RefRO<Parent>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
            {
                if (!localToWorldLookup.HasComponent(entity))
                    continue;
                
                // Walk up the parent chain to find the root and check if it's a ship
                Entity current = parent.ValueRO.Value;
                Entity shipRoot = Entity.Null;
                
                // Build a list of parent entities (max 10 levels deep)
                var parentChain = new NativeList<Entity>(10, Allocator.Temp);
                parentChain.Add(entity);
                
                int depth = 0;
                while (current != Entity.Null && depth < 10)
                {
                    parentChain.Add(current);
                    
                    if (shipRootLookup.HasComponent(current))
                    {
                        shipRoot = current;
                        break;
                    }
                    
                    if (parentLookup.HasComponent(current))
                    {
                        current = parentLookup[current].Value;
                    }
                    else
                    {
                        break;
                    }
                    depth++;
                }
                
                // If we found a ship root, compute the full transform chain
                if (shipRoot != Entity.Null && localToWorldLookup.HasComponent(shipRoot))
                {
                    // Start with ship's LocalToWorld (already updated in first pass)
                    var worldMatrix = localToWorldLookup[shipRoot].Value;
                    
                    // Walk down the chain (reverse order, skipping ship root and target entity)
                    for (int i = parentChain.Length - 2; i >= 0; i--)
                    {
                        Entity chainEntity = parentChain[i];
                        if (localTransformLookup.HasComponent(chainEntity))
                        {
                            var lt = localTransformLookup[chainEntity];
                            var localMatrix = float4x4.TRS(
                                lt.Position,
                                lt.Rotation,
                                new float3(lt.Scale)
                            );
                            worldMatrix = math.mul(worldMatrix, localMatrix);
                        }
                        
                        // Update this entity's LocalToWorld
                        if (localToWorldLookup.HasComponent(chainEntity))
                        {
                            localToWorldLookup[chainEntity] = new LocalToWorld { Value = worldMatrix };
                        }
                    }
                }
                
                parentChain.Dispose();
            }
        }
    }
}
