using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Aggro.Components;
using DIG.Combat.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Emits SoundEventRequest entities for explosions.
    /// Reads ModifierExplosionRequest (AOE explosions from modifiers/abilities)
    /// and creates loud, long-range sound events that alert nearby AI.
    ///
    /// OnUpdate is not Burst-compiled because ECB.Playback is a managed call.
    /// Struct ISystem still avoids GC allocation and virtual dispatch vs SystemBase.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SoundDistributionSystem))]
    [BurstCompile]
    public partial struct ExplosionSoundEmitterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModifierExplosionRequest>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (explosion, entity) in
                SystemAPI.Query<RefRO<ModifierExplosionRequest>>()
                .WithEntityAccess())
            {
                var req = explosion.ValueRO;

                // Scale loudness and range based on explosion radius
                float loudness = math.clamp(req.Radius * 0.5f, 1.5f, 5.0f);
                float range = math.max(req.Radius * 4f, 120f);

                var requestEntity = ecb.CreateEntity();
                ecb.AddComponent(requestEntity, new SoundEventRequest
                {
                    Position = req.Position,
                    SourceEntity = req.SourceEntity,
                    Loudness = loudness,
                    MaxRange = range,
                    Category = SoundCategory.Explosion
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
