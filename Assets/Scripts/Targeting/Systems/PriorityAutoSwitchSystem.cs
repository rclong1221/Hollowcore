using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Player.Components;
using DIG.Player.Components;
using DIG.Targeting.Components;
using DIG.Targeting.Core;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16 Task 7: Priority Auto-Switch System
    /// 
    /// Automatically switches to next valid target when current target dies or goes out of range.
    /// Respects priority (boss > elite > normal).
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(global::Player.Systems.CameraLockOnSystem))]
    public partial struct PriorityAutoSwitchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ActiveLockBehavior>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if auto-switch is enabled
            if (!SystemAPI.TryGetSingleton<ActiveLockBehavior>(out var behavior))
                return;
                
            if ((behavior.Features & LockFeatureFlags.PriorityAutoSwitch) == 0)
                return;
            
            var em = state.EntityManager;
            
            // Data-driven config (EPIC 15.16 optimization)
            float maxRange = behavior.MaxLockRange > 0 ? behavior.MaxLockRange : 30f;
            
            foreach (var (lockState, transform, entity) in 
                SystemAPI.Query<RefRW<CameraTargetLockState>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (!lockState.ValueRO.IsLocked) continue;
                
                Entity currentTarget = lockState.ValueRO.TargetEntity;
                bool needsNewTarget = false;
                
                // Check if current target is still valid
                if (currentTarget == Entity.Null)
                {
                    needsNewTarget = true;
                }
                else if (!em.Exists(currentTarget))
                {
                    needsNewTarget = true;
                }
                else if (!em.HasComponent<LockOnTarget>(currentTarget) || 
                         !em.IsComponentEnabled<LockOnTarget>(currentTarget))
                {
                    needsNewTarget = true;
                }
                else
                {
                    // Check range using config value
                    if (em.HasComponent<LocalTransform>(currentTarget))
                    {
                        float3 targetPos = em.GetComponentData<LocalTransform>(currentTarget).Position;
                        float distSq = math.distancesq(transform.ValueRO.Position, targetPos);
                        if (distSq > maxRange * maxRange)
                        {
                            needsNewTarget = true;
                        }
                    }
                }
                
                if (needsNewTarget)
                {
                    Entity newTarget = FindBestTarget(ref state, transform.ValueRO.Position, entity, currentTarget, maxRange);
                    
                    if (newTarget != Entity.Null)
                    {
                        lockState.ValueRW.TargetEntity = newTarget;
                        // Reset to Locking phase since we switched targets
                        lockState.ValueRW.Phase = LockPhase.Locking;
                    }
                    else
                    {
                        // No valid target, unlock
                        lockState.ValueRW.IsLocked = false;
                        lockState.ValueRW.TargetEntity = Entity.Null;
                        lockState.ValueRW.Phase = LockPhase.Unlocked;
                        lockState.ValueRW.JustUnlocked = true;
                    }
                }
            }
        }
        
        private Entity FindBestTarget(ref SystemState state, float3 playerPos, Entity self, Entity exclude, float maxRange)
        {
            Entity bestTarget = Entity.Null;
            float bestDistSq = maxRange * maxRange;
            int bestPriority = int.MinValue;
            
            foreach (var (lockOnTarget, transform, targetEntity) in 
                SystemAPI.Query<RefRO<LockOnTarget>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (targetEntity == self) continue;
                if (targetEntity == exclude) continue;
                
                float3 targetPos = transform.ValueRO.Position;
                float distSq = math.distancesq(playerPos, targetPos);
                
                if (distSq > maxRange * maxRange) continue;
                
                int priority = lockOnTarget.ValueRO.Priority;
                
                // Prefer higher priority, then closer distance
                if (priority > bestPriority || (priority == bestPriority && distSq < bestDistSq))
                {
                    bestTarget = targetEntity;
                    bestDistSq = distSq;
                    bestPriority = priority;
                }
            }
            
            return bestTarget;
        }
    }
}
