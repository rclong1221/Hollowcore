using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using DIG.Weather;

namespace DIG.Surface
{
    /// <summary>
    /// EPIC 16.10 Phase 4: Applies slip physics when SurfaceMovementModifier.SlipFactor > 0.
    /// Blends between intended velocity (written by movement system) and previous momentum,
    /// causing sliding on ice. Uses system-local NativeHashMap to track previous velocities
    /// without adding fields to the ghost-replicated component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(SurfaceMovementModifierSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct SurfaceSlipSystem : ISystem
    {
        private NativeHashMap<Entity, float3> _previousVelocities;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _previousVelocities = new NativeHashMap<Entity, float3>(16, Allocator.Persistent);
            state.RequireForUpdate<SurfaceMovementModifier>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            if (_previousVelocities.IsCreated)
                _previousVelocities.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // EPIC 16.10 Phase 8: Respect feature toggles
            if (SystemAPI.TryGetSingleton<SurfaceGameplayToggles>(out var toggles) &&
                !toggles.EnableSlipPhysics)
                return;

            // EPIC 17.8: Read weather wetness for rain/snow friction modifier
            float weatherWetness = 0f;
            if (SystemAPI.TryGetSingleton<WeatherWetness>(out var wetness))
                weatherWetness = wetness.Value;

            foreach (var (moveMod, physVel, entity) in
                SystemAPI.Query<RefRO<SurfaceMovementModifier>, RefRW<PhysicsVelocity>>()
                    .WithAll<GroundSurfaceState, Simulate>()
                    .WithEntityAccess())
            {
                float slip = math.max(moveMod.ValueRO.SlipFactor, weatherWetness * 0.5f);
                if (slip < 0.01f)
                {
                    // No slip — remove from tracking if present
                    _previousVelocities.Remove(entity);
                    continue;
                }

                float3 intended = physVel.ValueRO.Linear;
                float3 intendedH = new float3(intended.x, 0f, intended.z);

                // Get previous horizontal velocity (momentum)
                float3 previousH = float3.zero;
                _previousVelocities.TryGetValue(entity, out previousH);

                // Blend: high slip = more momentum carry, less control
                float3 blendedH = math.lerp(intendedH, previousH, slip);

                // Write blended velocity back (preserve vertical)
                physVel.ValueRW.Linear = new float3(blendedH.x, intended.y, blendedH.z);

                // Store blended as previous for next frame
                _previousVelocities[entity] = blendedH;
            }
        }
    }
}
