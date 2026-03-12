using Unity.Entities;

namespace Player.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LandingFlagDecaySystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (lfRef, entity) in SystemAPI.Query<RefRW<Player.Components.LandingFlag>>().WithEntityAccess())
            {
                var lf = lfRef.ValueRW;
                lf.TimeLeft -= dt;
                if (lf.TimeLeft <= 0f)
                {
                    ecb.RemoveComponent<Player.Components.LandingFlag>(entity);
                }
                else
                {
                    ecb.SetComponent(entity, lf);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
