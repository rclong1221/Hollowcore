using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Physics;
using DIG.Survival.Physics;
using Player.Components;
using Player.Systems;
using Unity.Collections;

namespace Player.Systems.Physics
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))] 
    [UpdateBefore(typeof(CharacterControllerSystem))]
    public partial struct PushMovementSystem : ISystem
    {
        private ComponentLookup<PushableObject> _pushableLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _pushableLookup = state.GetComponentLookup<PushableObject>(isReadOnly: true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _pushableLookup.Update(ref state);
            _transformLookup.Update(ref state);

            foreach (var (pushConstraint, velocity, transform) in 
                SystemAPI.Query<RefRO<ActivePushConstraint>, RefRW<PhysicsVelocity>, RefRW<LocalTransform>>()
                .WithAll<Simulate>())
            {
                if (!pushConstraint.ValueRO.IsPushing || pushConstraint.ValueRO.TargetObject == Entity.Null)
                    continue;

                Entity target = pushConstraint.ValueRO.TargetObject;

                // 1. Get Mass
                float mass = 50f; 
                if (_pushableLookup.HasComponent(target))
                {
                    mass = _pushableLookup[target].Mass;
                }

                // 2. Slow down
                float speedFactor = math.clamp(100f / mass, 0.1f, 0.8f); 
                float maxPushSpeed = 2.0f * speedFactor; 
                
                float3 linear = velocity.ValueRO.Linear;
                float y = linear.y;
                linear.y = 0;
                
                if (math.lengthsq(linear) > maxPushSpeed * maxPushSpeed)
                {
                    linear = math.normalize(linear) * maxPushSpeed;
                }
                
                linear.y = y; 
                velocity.ValueRW.Linear = linear;

                // 3. Lock Rotation
                if (_transformLookup.HasComponent(target))
                {
                    float3 targetPos = _transformLookup[target].Position;
                    float3 direction = targetPos - transform.ValueRO.Position;
                    direction.y = 0; 
                    
                    if (math.lengthsq(direction) > 0.001f)
                    {
                        quaternion lookRot = quaternion.LookRotation(math.normalize(direction), math.up());
                        transform.ValueRW.Rotation = lookRot;
                    }
                }
            }
        }
    }
}
