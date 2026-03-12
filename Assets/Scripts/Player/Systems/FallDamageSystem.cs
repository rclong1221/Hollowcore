using Unity.Entities;
using Unity.Transforms;
using Player.Components;
using Unity.Mathematics;
using Unity.Collections;

namespace Player.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FallDamageSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Apply fall damage (synchronous)
            foreach (var (healthRef, dmgBuf, entity) in SystemAPI.Query<RefRW<Health>, DynamicBuffer<Damage>>().WithAll<Damage>().WithEntityAccess())
            {
                float total = 0f;
                for (int i = 0; i < dmgBuf.Length; i++) total += dmgBuf[i].Amount;
                if (total > 0f)
                {
                    var h = healthRef.ValueRW;
                    h.Current = math.max(0f, h.Current - total);
                    // write back
                    var ecb = new EntityCommandBuffer(Allocator.Temp);
                    ecb.SetComponent(entity, h);
                    ecb.Playback(EntityManager);
                    ecb.Dispose();
                }
            }

            // Clear damage buffers after applying (main thread)
            foreach (var (dmgBuf, entity) in SystemAPI.Query<DynamicBuffer<Damage>>().WithAll<Damage>().WithEntityAccess())
            {
                if (dmgBuf.Length > 0)
                    dmgBuf.Clear();
            }

            // Note: ApplyDamage events are handled by a dedicated adapter system
        }
    }
}
