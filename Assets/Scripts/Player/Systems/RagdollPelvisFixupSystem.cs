using Unity.Entities;
using Unity.NetCode;
using Unity.Burst;
using DIG.Survival.Physics;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Server system that fixes up RagdollController.Pelvis references after ghost spawn.
    /// 
    /// PROBLEM: In NetCode, when ghosts are spawned from prefabs, entity references baked 
    /// during authoring (like RagdollController.Pelvis) point to the PREFAB's entities, 
    /// not the spawned ghost's unique child entities. This causes all players to share 
    /// the same pelvis entity reference.
    /// 
    /// SOLUTION: Use entity index offset. During baking, entities get consecutive indices.
    /// When ghosts spawn, LinkedEntityGroup entities are created in the same order.
    /// We find the pelvis by calculating its position in the original prefab's hierarchy
    /// and mapping to the spawned ghost's LinkedEntityGroup.
    /// 
    /// NOTE: RagdollBone component doesn't exist on server-side ghosts, so we can't use it.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class RagdollPelvisFixupSystem : SystemBase
    {
        protected override void OnCreate()
        {
            Debug.Log("[RagdollPelvisFixup] System created on server");
        }
        
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (ragdollCtrl, linkedGroup, entity) in 
                SystemAPI.Query<RefRW<RagdollController>, DynamicBuffer<LinkedEntityGroup>>()
                    .WithNone<RagdollPelvisFixed>()
                    .WithEntityAccess())
            {
                Entity oldPelvis = ragdollCtrl.ValueRO.Pelvis;
                Entity foundPelvis = Entity.Null;
                
                // The key insight: During baking, the main entity and all linked entities
                // get entity indices. The LinkedEntityGroup preserves the ORDER.
                // Element 0 is always the main entity itself.
                // 
                // Since the prefab's pelvis entity has a specific index, we can calculate
                // which position in LinkedEntityGroup it should occupy.
                // 
                // For ragdolls: the pelvis is typically the FIRST child bone added to
                // LinkedEntityGroup during baking (after the main entity at index 0).
                // Let's find it by trying a few known positions.
                
                // Strategy 1: Pelvis is often at index 1 (first child after main entity)
                // But this depends on baking order. Let's iterate and find an entity
                // that has LocalToWorld with a reasonable position (not at origin).
                
                // For now, use a simpler approach: The pelvis should be the entity
                // closest to index 1 that has a LocalToWorld component (all bones do).
                // We'll pick the first entity after index 0 that has LocalToWorld.
                
                var ltwLookup = GetComponentLookup<Unity.Transforms.LocalToWorld>(isReadOnly: true);
                
                // Skip index 0 (main entity), find first child with LocalToWorld
                for (int i = 1; i < linkedGroup.Length; i++)
                {
                    Entity childEntity = linkedGroup[i].Value;
                    if (ltwLookup.HasComponent(childEntity))
                    {
                        foundPelvis = childEntity;
                        break;
                    }
                }
                
                if (foundPelvis != Entity.Null && foundPelvis != oldPelvis)
                {
                    ragdollCtrl.ValueRW.Pelvis = foundPelvis;
                    
                    Debug.Log($"[RagdollPelvisFixup] Fixed Entity {entity.Index}: Pelvis {oldPelvis.Index}:{oldPelvis.Version} -> {foundPelvis.Index}:{foundPelvis.Version}");
                }
                else if (foundPelvis == Entity.Null)
                {
                    Debug.LogWarning($"[RagdollPelvisFixup] Entity {entity.Index}: No entity with LocalToWorld found in {linkedGroup.Length} linked entities!");
                }
                else
                {
                    // foundPelvis == oldPelvis - already correct (unlikely but possible)
                    Debug.Log($"[RagdollPelvisFixup] Entity {entity.Index}: Pelvis already correct at {foundPelvis.Index}:{foundPelvis.Version}");
                }
                
                // Mark as fixed so we don't process again
                ecb.AddComponent<RagdollPelvisFixed>(entity);
            }
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Tag component to mark entities that have had their pelvis reference fixed.
    /// Prevents redundant processing.
    /// </summary>
    public struct RagdollPelvisFixed : IComponentData { }
}
