using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Voxel.Components;
using Player.Components;
using DIG.Items;

namespace DIG.Voxel.Systems.Interaction
{
    /// <summary>
    /// EPIC 15.10: Handles remote detonator input.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    // [BurstCompile] // Disabled for debugging
    public partial struct RemoteDetonatorSystem : ISystem
    {
        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Debug: Confirm system is running (throttled)
            if (UnityEngine.Time.frameCount % 120 == 0)
                 UnityEngine.Debug.Log("[RemoteDetonatorSystem] System is RUNNING.");

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            // Update Cooldowns
            foreach (var stateData in SystemAPI.Query<RefRW<ExplosivePlacementState>>())
            {
                if (stateData.ValueRO.CooldownTimer > 0)
                    stateData.ValueRW.CooldownTimer -= dt;
            }

            // DEBUG PROBE: Check if ANY RemoteDetonator exists
            if (UnityEngine.Time.frameCount % 120 == 0)
            {
                int count = 0;
                foreach (var (detonator, entity) in SystemAPI.Query<RefRO<RemoteDetonator>>().WithEntityAccess())
                {
                    count++;
                    bool hasState = SystemAPI.HasComponent<ExplosivePlacementState>(entity);
                    bool hasItem = SystemAPI.HasComponent<DIG.Items.CharacterItem>(entity);
                    bool hasInput = SystemAPI.HasComponent<PlayerInput>(entity); // Should be on Player, not Item
                    UnityEngine.Debug.Log($"[RemoteDetonator] PROBE: Entity {entity.Index} has RemoteDetonator. HasState={hasState} HasItem={hasItem} HasInput={hasInput}");
                }
                if (count == 0) UnityEngine.Debug.LogWarning("[RemoteDetonator] PROBE: NO entities with RemoteDetonator component found!");
            }

            var playerInputLookup = SystemAPI.GetComponentLookup<PlayerInput>(true);
            var explosiveOwnerLookup = SystemAPI.GetComponentLookup<EntityOwner>(true);

            // Process Input
            foreach (var (detonator, stateData, charItem, entity) in
                     SystemAPI.Query<RefRO<RemoteDetonator>, RefRW<ExplosivePlacementState>, RefRO<DIG.Items.CharacterItem>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                Entity owner = charItem.ValueRO.OwnerEntity;
                if (owner == Entity.Null || !playerInputLookup.TryGetComponent(owner, out var input))
                    continue;

                // FIX: Only allow detonation if the tool is actually equipped (held)
                if (charItem.ValueRO.State != ItemState.Equipped)
                {
                    if (input.Use.IsSet) // Only spam log if trying to use it
                         UnityEngine.Debug.Log($"[RemoteDetonator] Item not equipped. State: {charItem.ValueRO.State}");
                    continue;
                }

                if (input.Use.IsSet && stateData.ValueRO.CooldownTimer <= 0)
                {
                    UnityEngine.Debug.Log($"[VoxelTool] Input DETECTED! Scanning for explosives owned by Entity {owner.Index}...");
                    int triggeredCount = 0;
                    
                    foreach (var (explosiveTransform, explosiveEntity) in 
                             SystemAPI.Query<RefRO<LocalTransform>>()
                             .WithAll<RemoteExplosive>()
                             .WithEntityAccess())
                    {
                        // Filter by owner
                        if (explosiveOwnerLookup.TryGetComponent(explosiveEntity, out var expOwner))
                        {
                            if (expOwner.OwnerEntity != owner)
                            {
                                UnityEngine.Debug.Log($"[VoxelTool] Found explosive {explosiveEntity.Index}, but owner mismatch (ExplosiveOwner: {expOwner.OwnerEntity.Index} vs Player: {owner.Index})");
                                continue;
                            }
                        }
                        else
                        {
                             UnityEngine.Debug.Log($"[VoxelTool] Found explosive {explosiveEntity.Index} but it has NO EntityOwner component!");
                             continue;
                        }

                        // Add Detonation Request
                        ecb.AddComponent<VoxelDetonationRequest>(explosiveEntity);
                        triggeredCount++;
                        UnityEngine.Debug.Log($"[VoxelTool] TRIGGERED explosive {explosiveEntity.Index}");
                    }
                    
                    if (triggeredCount > 0)
                    {
                        stateData.ValueRW.CooldownTimer = detonator.ValueRO.Cooldown;
                        UnityEngine.Debug.Log($"[VoxelTool] Successfully triggered {triggeredCount} explosives.");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("[VoxelTool] No valid explosives found to trigger.");
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
