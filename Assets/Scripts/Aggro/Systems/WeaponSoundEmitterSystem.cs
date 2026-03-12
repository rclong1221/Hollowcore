using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using DIG.Weapons;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Emits SoundEventRequest entities when weapons fire.
    /// Detects rising edge of WeaponFireState.IsFiring to emit one sound per shot.
    ///
    /// OnUpdate is not Burst-compiled because ECB.Playback is a managed call.
    /// Struct ISystem still avoids GC allocation and virtual dispatch vs SystemBase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SoundDistributionSystem))]
    [BurstCompile]
    public partial struct WeaponSoundEmitterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WeaponFireState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (fireState, transform, entity) in
                SystemAPI.Query<RefRO<WeaponFireState>, RefRO<LocalToWorld>>()
                .WithEntityAccess())
            {
                if (!fireState.ValueRO.IsFiring)
                    continue;

                // Only emit on the frame the shot was taken (TimeSinceLastShot near zero)
                if (fireState.ValueRO.TimeSinceLastShot > dt * 1.5f)
                    continue;

                float3 position = transform.ValueRO.Position;

                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new SoundEventRequest
                {
                    Position = position,
                    SourceEntity = entity,
                    Loudness = 1.5f,
                    MaxRange = 80f,
                    Category = SoundCategory.Gunfire
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
