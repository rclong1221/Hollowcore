using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms; // Added
using DIG.Survival.Physics; // Components
using Player.Components;

namespace Player.Systems.Physics
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(PushMovementSystem))]
    public partial struct PushInteractionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (input, pushConstraint, transform, entity) in 
                SystemAPI.Query<RefRO<PlayerInput>, RefRW<ActivePushConstraint>, RefRO<LocalTransform>>()
                .WithAll<GhostOwnerIsLocal>() 
                .WithEntityAccess())
            {
                bool isGrabbing = input.ValueRO.Grab.IsSet;
                
                if (pushConstraint.ValueRO.IsPushing)
                {
                    // Handle Release
                    if (!isGrabbing)
                    {
                        // Destroy joint
                        if (pushConstraint.ValueRO.PhysicsJoint != Entity.Null)
                        {
                            ecb.DestroyEntity(pushConstraint.ValueRO.PhysicsJoint);
                        }
                        
                        pushConstraint.ValueRW.IsPushing = false;
                        pushConstraint.ValueRW.TargetObject = Entity.Null;
                        pushConstraint.ValueRW.PhysicsJoint = Entity.Null;
                    }
                }
                else
                {
                    // Handle Grab
                    if (isGrabbing) 
                    {
                        // Raycast forward
                        float3 forward = transform.ValueRO.Forward();
                        float3 start = transform.ValueRO.Position + math.up() * 1.0f; // Center mass roughly
                        float3 end = start + forward * 2.0f; // 2m reach
                        
                        RaycastInput rayInput = new RaycastInput
                        {
                            Start = start,
                            End = end,
                            Filter = CollisionFilter.Default
                        };
                        
                        if (physicsWorld.CastRay(rayInput, out RaycastHit hit))
                        {
                            if (SystemAPI.HasComponent<PushableObject>(hit.Entity))
                            {
                                // Attach!
                                CreateJoint(ecb, entity, hit.Entity, hit.Position, hit.SurfaceNormal, ref pushConstraint.ValueRW);
                            }
                        }
                    }
                }
            }
        }

        private void CreateJoint(EntityCommandBuffer ecb, Entity player, Entity target, float3 hitPos, float3 hitNormal, ref ActivePushConstraint constraint)
        {
            Entity jointEntity = ecb.CreateEntity();
            
            // Fixed Joint (Player <-> Crate)
            PhysicsJoint joint = PhysicsJoint.CreateFixed(
                BodyFrame.Identity, 
                BodyFrame.Identity  
            );
            
            ecb.AddComponent(jointEntity, new PhysicsConstrainedBodyPair(player, target, false));
            ecb.AddComponent(jointEntity, joint);
            
            constraint.IsPushing = true;
            constraint.TargetObject = target;
            constraint.PhysicsJoint = jointEntity;
            constraint.LocalGripPoint = float3.zero; 
        }
    }
}
