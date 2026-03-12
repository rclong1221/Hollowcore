using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using DIG.Player.Abilities;

namespace DIG.Player.Systems.Abilities
{
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    public partial struct MoveTowardsSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Physics bodies: Dynamic uses PhysicsVelocity, Kinematic uses direct position
            foreach (var (moveTowards, transform, velocity, mass) in
                     SystemAPI.Query<RefRW<MoveTowardsAbility>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<PhysicsMass>>())
            {
                if (!moveTowards.ValueRO.IsMoving) continue;

                float3 targetPos = moveTowards.ValueRO.TargetPosition;
                float3 currentPos = transform.ValueRO.Position;

                float3 diff = targetPos - currentPos;
                diff.y = 0;
                float distSq = math.lengthsq(diff);
                float stopDist = moveTowards.ValueRO.StopDistance;

                bool isKinematic = mass.ValueRO.InverseMass == 0f;

                if (distSq <= stopDist * stopDist)
                {
                    // Arrived
                    moveTowards.ValueRW.IsMoving = false;
                    if (!isKinematic)
                    {
                        velocity.ValueRW.Linear.x = 0;
                        velocity.ValueRW.Linear.z = 0;
                    }
                }
                else
                {
                    float3 dir = math.normalize(diff);
                    float speed = moveTowards.ValueRO.MoveSpeed;

                    if (isKinematic)
                    {
                        // Kinematic: write position directly (velocity not integrated)
                        float3 move = dir * speed * deltaTime;
                        transform.ValueRW.Position += new float3(move.x, 0f, move.z);
                    }
                    else
                    {
                        // Dynamic: set velocity for physics integration
                        velocity.ValueRW.Linear.x = dir.x * speed;
                        velocity.ValueRW.Linear.z = dir.z * speed;
                    }

                    // Face target
                    if (moveTowards.ValueRO.FaceTargetOnArrival || distSq > 0.1f)
                    {
                        var targetRot = quaternion.LookRotation(dir, math.up());
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, 10f * deltaTime);
                    }
                }
            }

            // Kinematic/static bodies: move via direct LocalTransform (AI enemies)
            foreach (var (moveTowards, transform) in
                     SystemAPI.Query<RefRW<MoveTowardsAbility>, RefRW<LocalTransform>>()
                     .WithNone<PhysicsVelocity>())
            {
                if (!moveTowards.ValueRO.IsMoving) continue;

                float3 targetPos = moveTowards.ValueRO.TargetPosition;
                float3 currentPos = transform.ValueRO.Position;

                float3 diff = targetPos - currentPos;
                diff.y = 0;
                float distSq = math.lengthsq(diff);
                float stopDist = moveTowards.ValueRO.StopDistance;

                if (distSq <= stopDist * stopDist)
                {
                    moveTowards.ValueRW.IsMoving = false;
                }
                else
                {
                    float3 dir = math.normalize(diff);
                    float speed = moveTowards.ValueRO.MoveSpeed;

                    // Direct position update for kinematic bodies
                    float3 move = dir * speed * deltaTime;
                    transform.ValueRW.Position += new float3(move.x, 0f, move.z);

                    if (moveTowards.ValueRO.FaceTargetOnArrival || distSq > 0.1f)
                    {
                        var targetRot = quaternion.LookRotation(dir, math.up());
                        transform.ValueRW.Rotation = math.slerp(transform.ValueRO.Rotation, targetRot, 10f * deltaTime);
                    }
                }
            }
        }
    }
}
