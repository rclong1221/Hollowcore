using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;

namespace DIG.Survival.EVA
{
    /// <summary>
    /// Applies gravity override when magnetic boots are attached.
    /// Pulls player toward the attached surface (allows walking on walls/ceilings).
    /// Also applies attach force to keep player firmly planted.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MagneticBootDetachSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct MagneticBootGravitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (bootState, velocity) in
                     SystemAPI.Query<RefRO<MagneticBootState>, RefRW<PhysicsVelocity>>()
                     .WithAll<Simulate>())
            {
                var boots = bootState.ValueRO;
                ref var vel = ref velocity.ValueRW;

                // Only apply when actively attached
                if (!boots.IsActivelyAttached)
                {
                    continue;
                }

                // Apply attach force toward surface (opposite of surface normal)
                float3 gravityDirection = -boots.AttachedNormal;
                float3 attachAcceleration = gravityDirection * boots.AttachForce * deltaTime;
                vel.Linear += attachAcceleration;

                // Clamp velocity component along surface normal to prevent bouncing
                float normalVelocity = math.dot(vel.Linear, boots.AttachedNormal);
                if (normalVelocity < 0)
                {
                    // Moving toward surface - cap at a small value to prevent penetration
                    vel.Linear -= boots.AttachedNormal * math.min(normalVelocity, -0.1f);
                }
            }
        }
    }
}
