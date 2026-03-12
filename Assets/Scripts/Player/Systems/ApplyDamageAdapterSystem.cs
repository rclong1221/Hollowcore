using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Player.Components;

namespace Player.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ApplyDamageAdapterSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Apply ApplyDamage directly to Health components (only on simulation/authoritative world)
            // Skip entities that are currently invulnerable from a dodge roll
            foreach (var (evtRef, healthRef, entity) in SystemAPI.Query<RefRO<ApplyDamage>, RefRW<Health>>().WithAll<Simulate>().WithNone<DodgeRollInvuln>().WithEntityAccess())
            {
                var evt = evtRef.ValueRO;
                var h = healthRef.ValueRW;
                h.Current = math.max(0f, h.Current - evt.Amount);
                ecb.RemoveComponent<ApplyDamage>(entity);
            }

            // Append ApplyDamage amounts to Damage buffers when present (only on simulation/authoritative world)
            foreach (var (evtRef, buf, entity) in SystemAPI.Query<RefRO<ApplyDamage>, DynamicBuffer<Damage>>().WithAll<Simulate, Damage>().WithNone<DodgeRollInvuln>().WithEntityAccess())
            {
                buf.Add(new Damage { Amount = evtRef.ValueRO.Amount });
                ecb.RemoveComponent<ApplyDamage>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
