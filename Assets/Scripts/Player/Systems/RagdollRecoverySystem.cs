using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Player.Components;
using DIG.Survival.Physics;

namespace Player.Systems
{
    /// <summary>
    /// Restores player physics when revived from ragdoll state.
    /// Triggers when DeathPhase.Alive AND RagdollController.IsRagdolled == true.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RagdollTransitionSystem))]
    public partial struct RagdollRecoverySystem : ISystem
    {
        private ComponentLookup<RagdollBone> _ragdollBoneLookup;
        private ComponentLookup<LocalToWorld> _ltwLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            _ragdollBoneLookup = state.GetComponentLookup<RagdollBone>(isReadOnly: true);
            _ltwLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ragdollBoneLookup.Update(ref state);
            _ltwLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new RagdollRecoveryJob
            {
                Ecb = ecb,
                RagdollBoneLookup = _ragdollBoneLookup,
                LtwLookup = _ltwLookup
            }.ScheduleParallel();
        }

        // [BurstCompile] // TEMP: Disabled for Debug.Log
        public partial struct RagdollRecoveryJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<RagdollBone> RagdollBoneLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;

            void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref RagdollController ragdollCtrl, in DeathState deathState, in DynamicBuffer<LinkedEntityGroup> linkedGroup)
            {
                // DEBUG: Log every entity we're checking
                if (RagdollSettleClientSystem.DiagnosticsEnabled)
                    UnityEngine.Debug.Log($"[RagdollRecovery] Checking {entity}: Phase={deathState.Phase}, IsRagdolled={ragdollCtrl.IsRagdolled}");
                
                // Trigger only if Alive but still flagged as ragdolled (needs recovery)
                if (deathState.Phase == DeathPhase.Alive && ragdollCtrl.IsRagdolled)
                {
                    if (RagdollSettleClientSystem.DiagnosticsEnabled)
                        UnityEngine.Debug.Log($"[RagdollRecovery] Recovering player {entity}");
                    
                    ragdollCtrl.IsRagdolled = false;

                    // 1. Restore dynamic mass for the player root body
                    // Use standard player mass (80kg) with sphere approximation for inertia
                    Ecb.SetComponent(sortKey, entity, PhysicsMass.CreateDynamic(
                        MassProperties.UnitSphere, 
                        80f // 80kg player mass
                    ));

                    // 2. (Bones will be deactivated below)
                    for (int i = 0; i < linkedGroup.Length; i++)
                    {
                        Entity child = linkedGroup[i].Value;

                        if (RagdollBoneLookup.HasComponent(child))
                        {
                            var bone = RagdollBoneLookup[child];
                            
                            // A. Re-parent to original parent if we stored it
                            if (bone.OriginalParent != Entity.Null)
                            {
                                Ecb.AddComponent(sortKey, child, new Parent { Value = bone.OriginalParent });
                            }

                            // B. Reset to kinematic (infinite mass = kinematic)
                            Ecb.SetComponent(sortKey, child, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));

                            // C. Zero velocity
                            Ecb.SetComponent(sortKey, child, new PhysicsVelocity());

                            // D. Mark bone as inactive
                            Ecb.SetComponent(sortKey, child, new RagdollBone 
                            { 
                                IsActive = false,
                                OriginalParent = bone.OriginalParent 
                            });
                        }
                    }
                }
            }
        }
    }
}
