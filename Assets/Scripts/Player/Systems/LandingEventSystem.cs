using Unity.Entities;
using Unity.Mathematics;

namespace Player.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LandingEventSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (leRef, entity) in SystemAPI.Query<RefRO<Player.Components.LandingEvent>>().WithEntityAccess())
            {
                var le = leRef.ValueRO;
                // Default duration
                float duration = 0.5f;
                // If entity has FallDamageSettings, use its LandingFlagDuration
                if (SystemAPI.HasComponent<Player.Components.FallDamageSettings>(entity))
                {
                    var s = SystemAPI.GetComponent<Player.Components.FallDamageSettings>(entity);
                    duration = s.LandingFlagDuration;
                }

                var flag = new Player.Components.LandingFlag { TimeLeft = duration, Intensity = le.Intensity };

                if (!SystemAPI.HasComponent<Player.Components.LandingFlag>(entity))
                {
                    ecb.AddComponent(entity, flag);
                }
                else
                {
                    ecb.SetComponent(entity, flag);
                }

                // Remove the one-shot LandingEvent
                ecb.RemoveComponent<Player.Components.LandingEvent>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
