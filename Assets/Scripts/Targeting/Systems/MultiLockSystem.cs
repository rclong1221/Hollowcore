using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;
using DIG.Targeting.Components;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16 Task 4: Multi-Lock System
    /// 
    /// Allows locking multiple targets simultaneously for missile salvos or chain attacks.
    /// Hold lock button to accumulate targets, release to "fire".
    /// 
    /// NOTE: MultiLockState and LockedTargetElement buffer are on the TargetingModule child entity.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct MultiLockSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if multi-lock is enabled
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if ((behavior.Features & LockFeatureFlags.MultiLock) == 0)
                return;
            
            int maxTargets = behavior.MaxLockedTargets;
            if (maxTargets <= 1) return; // Multi-lock requires > 1 target
            
            float deltaTime = SystemAPI.Time.DeltaTime;
            var em = state.EntityManager;
            
            foreach (var (moduleLink, transform, input, entity) in 
                SystemAPI.Query<RefRO<TargetingModuleLink>, RefRO<LocalTransform>, RefRO<PlayerInput>>()
                .WithEntityAccess())
            {
                // Get the targeting module entity
                Entity moduleEntity = moduleLink.ValueRO.TargetingModule;
                if (moduleEntity == Entity.Null || !SystemAPI.HasComponent<MultiLockState>(moduleEntity))
                    continue;
                
                var multiLock = SystemAPI.GetComponentRW<MultiLockState>(moduleEntity);
                
                bool isLockHeld = input.ValueRO.Grab.IsSet;
                
                // Get or create the locked targets buffer on module entity
                if (!em.HasBuffer<LockedTargetElement>(moduleEntity))
                {
                    em.AddBuffer<LockedTargetElement>(moduleEntity);
                }
                
                var lockedTargets = em.GetBuffer<LockedTargetElement>(moduleEntity);
                
                if (isLockHeld)
                {
                    if (!multiLock.ValueRO.IsAccumulating)
                    {
                        // Just started holding - begin accumulation
                        multiLock.ValueRW.IsAccumulating = true;
                        multiLock.ValueRW.ReadyToFire = false;
                        lockedTargets.Clear();
                        UnityEngine.Debug.Log("[MultiLock] Started accumulating targets");
                    }
                    
                    // Accumulate targets while holding
                    if (multiLock.ValueRO.LockedCount < maxTargets)
                    {
                        Entity newTarget = FindNextTarget(ref state, transform.ValueRO.Position, entity, lockedTargets);
                        
                        if (newTarget != Entity.Null)
                        {
                            lockedTargets.Add(new LockedTargetElement
                            {
                                Target = newTarget,
                                LastPosition = em.GetComponentData<LocalTransform>(newTarget).Position,
                                LockTime = 0f,
                                TargetPartIndex = 0
                            });
                            
                            multiLock.ValueRW.LockedCount = lockedTargets.Length;
                            UnityEngine.Debug.Log($"[MultiLock] Locked target {lockedTargets.Length}/{maxTargets}: Entity {newTarget.Index}");
                        }
                    }
                }
                else if (multiLock.ValueRO.IsAccumulating)
                {
                    // Released lock button - fire!
                    multiLock.ValueRW.IsAccumulating = false;
                    
                    if (multiLock.ValueRO.LockedCount > 0)
                    {
                        multiLock.ValueRW.ReadyToFire = true;
                        UnityEngine.Debug.Log($"[MultiLock] FIRE! Releasing {multiLock.ValueRO.LockedCount} targets");
                        
                        // Dispatch event or handle firing
                        // For now, just log the targets
                        for (int i = 0; i < lockedTargets.Length; i++)
                        {
                            UnityEngine.Debug.Log($"[MultiLock]   Target {i + 1}: Entity {lockedTargets[i].Target.Index}");
                        }
                        
                        // Clear after firing
                        lockedTargets.Clear();
                        multiLock.ValueRW.LockedCount = 0;
                        multiLock.ValueRW.ReadyToFire = false;
                    }
                }
                
                // Update target positions for locked targets
                for (int i = 0; i < lockedTargets.Length; i++)
                {
                    var locked = lockedTargets[i];
                    if (em.Exists(locked.Target) && em.HasComponent<LocalTransform>(locked.Target))
                    {
                        locked.LastPosition = em.GetComponentData<LocalTransform>(locked.Target).Position;
                        locked.LockTime += deltaTime;
                        lockedTargets[i] = locked;
                    }
                }
            }
        }
        
        private Entity FindNextTarget(ref SystemState state, float3 playerPos, Entity self, DynamicBuffer<LockedTargetElement> alreadyLocked)
        {
            float maxRange = 40f;
            Entity bestTarget = Entity.Null;
            float bestDistSq = maxRange * maxRange;
            
            foreach (var (lockOnTarget, transform, targetEntity) in 
                SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (targetEntity == self) continue;
                
                // Skip already locked targets
                bool alreadyTargeted = false;
                for (int i = 0; i < alreadyLocked.Length; i++)
                {
                    if (alreadyLocked[i].Target == targetEntity)
                    {
                        alreadyTargeted = true;
                        break;
                    }
                }
                if (alreadyTargeted) continue;
                
                float3 targetPos = transform.ValueRO.Position;
                float distSq = math.distancesq(playerPos, targetPos);
                
                if (distSq > maxRange * maxRange) continue;
                
                if (distSq < bestDistSq)
                {
                    bestTarget = targetEntity;
                    bestDistSq = distSq;
                }
            }
            
            return bestTarget;
        }
    }
}
