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
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct RagdollTransitionSystem : ISystem
    {
        private ComponentLookup<RagdollBone> _ragdollBoneLookup;
        private ComponentLookup<PhysicsCollider> _colliderLookup;
        private ComponentLookup<LocalToWorld> _ltwLookup;
        private ComponentLookup<Parent> _parentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            
            _ragdollBoneLookup = state.GetComponentLookup<RagdollBone>(isReadOnly: true);
            _colliderLookup = state.GetComponentLookup<PhysicsCollider>(isReadOnly: true);
            _ltwLookup = state.GetComponentLookup<LocalToWorld>(isReadOnly: true);
            _parentLookup = state.GetComponentLookup<Parent>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ragdollBoneLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _ltwLookup.Update(ref state);
            _parentLookup.Update(ref state);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            new RagdollTransitionJob
            {
                Ecb = ecb,
                RagdollBoneLookup = _ragdollBoneLookup,
                ColliderLookup = _colliderLookup,
                LtwLookup = _ltwLookup,
                ParentLookup = _parentLookup
            }.ScheduleParallel();
        }

        // [BurstCompile] // TEMP: Disabled for Debug.Log
        public partial struct RagdollTransitionJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<RagdollBone> RagdollBoneLookup;
            [ReadOnly] public ComponentLookup<PhysicsCollider> ColliderLookup;
            [ReadOnly] public ComponentLookup<LocalToWorld> LtwLookup;
            [ReadOnly] public ComponentLookup<Parent> ParentLookup;

            void Execute(Entity entity, [EntityIndexInQuery] int sortKey, ref RagdollController ragdollCtrl, in DeathState deathState, in PhysicsVelocity velocity, in PhysicsCollider rootCollider, in DynamicBuffer<LinkedEntityGroup> linkedGroup)
            {
                // Trigger only if Dead/Downed and not yet ragdolled
                if ((deathState.Phase == DeathPhase.Dead || deathState.Phase == DeathPhase.Downed) && !ragdollCtrl.IsRagdolled)
                {
                    if (RagdollSettleClientSystem.DiagnosticsEnabled)
                        UnityEngine.Debug.Log($"[RagdollTransition] Player {entity} entering ragdoll, collider.IsCreated={rootCollider.Value.IsCreated}");
                    ragdollCtrl.IsRagdolled = true;

                    // 1. Make root body kinematic - it keeps its collider but won't move
                    // Zero velocity to stop any movement
                    Ecb.SetComponent(sortKey, entity, new PhysicsVelocity());
                    
                    // Set to kinematic mass so the root body doesn't fall through world
                    // The ragdoll bones will be dynamic and handle the physics
                    Ecb.SetComponent(sortKey, entity, PhysicsMass.CreateKinematic(MassProperties.UnitSphere));
                    
                    if (RagdollSettleClientSystem.DiagnosticsEnabled)
                        UnityEngine.Debug.Log($"[RagdollTransition] Set root {entity} to Kinematic Mass and Zero Velocity");

                    // 3. Activate Bones
                    for (int i = 0; i < linkedGroup.Length; i++)
                    {
                        Entity child = linkedGroup[i].Value;

                        // Check if this child is a Ragdoll Bone
                        if (RagdollBoneLookup.HasComponent(child))
                        {
                            // A. Store original parent and unparent
                            if (ParentLookup.HasComponent(child))
                            {
                                var originalParent = ParentLookup[child].Value;
                                var childLtw = LtwLookup[child];
                                
                                // Update RagdollBone with original parent for recovery
                                Ecb.SetComponent(sortKey, child, new RagdollBone 
                                { 
                                    IsActive = true,
                                    OriginalParent = originalParent
                                });
                                
                                Ecb.RemoveComponent<Parent>(sortKey, child);
                                Ecb.SetComponent(sortKey, child, LocalTransform.FromPositionRotation(childLtw.Position, childLtw.Rotation));
                            }

                            // B. Set Dynamic Mass
                            if (ColliderLookup.HasComponent(child))
                            {
                                var collider = ColliderLookup[child];
                                if (collider.Value.IsCreated)
                                {
                                    var massProps = collider.Value.Value.MassProperties;
                                    var dynamicMass = PhysicsMass.CreateDynamic(massProps, 15.0f); // 15kg per limb
                                    Ecb.SetComponent(sortKey, child, dynamicMass);
                                }
                            }

                            // C. Inherit Velocity (Heavily Dampened)
                            // BUGFIX: Previous 50% + 5m/s cap was too high, causing "car hit" effect
                            PhysicsVelocity inheritedVel = velocity;
                            
                            float linearMag = math.length(inheritedVel.Linear);
                            
                            if (RagdollSettleClientSystem.DiagnosticsEnabled && linearMag > 0.01f)
                            {
                                UnityEngine.Debug.Log($"[RagdollTransition] Child {child} inherited velocity {inheritedVel.Linear} (mag={linearMag:F2})");
                            }

                            // Only apply if noticeable, otherwise zero it
                            if (linearMag > 0.1f)
                            {
                                // Dampen velocity to 10% - ragdoll should gently flop, not fly
                                inheritedVel.Linear *= 0.1f; 
                                inheritedVel.Angular *= 0.1f;
                                
                                // Cap max velocity to 1 m/s (gentle fall/slide)
                                float newMag = math.length(inheritedVel.Linear);
                                if (newMag > 1.0f)
                                {
                                    inheritedVel.Linear = math.normalize(inheritedVel.Linear) * 1.0f;
                                }
                                Ecb.SetComponent(sortKey, child, inheritedVel);
                            }
                            else
                            {
                                // Zero velocity if negligible
                                Ecb.SetComponent(sortKey, child, new PhysicsVelocity());
                            }
                        }
                    }
                }
            }
        }
    }
}
