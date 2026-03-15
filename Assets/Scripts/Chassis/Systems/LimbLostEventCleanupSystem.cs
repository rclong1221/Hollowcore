using Unity.Entities;

namespace Hollowcore.Chassis.Systems
{
    /// <summary>
    /// Destroys LimbLostEvent entities after one frame so they don't accumulate.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class LimbLostEventCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<LimbLostEvent>>()
                     .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
