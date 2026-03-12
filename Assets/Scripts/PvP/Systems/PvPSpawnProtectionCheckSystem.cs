using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using Player.Systems;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Disables expired spawn protection and clears DamageEvent
    /// buffers on protected entities before DamageApplySystem runs.
    /// Runs in DamageSystemGroup (PredictedFixedStepSimulationSystemGroup)
    /// so protection applies within each prediction tick.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateBefore(typeof(DamageApplySystem))]
    public partial class PvPSpawnProtectionCheckSystem : SystemBase
    {
        private EntityQuery _protectedQuery;

        protected override void OnCreate()
        {
            _protectedQuery = GetEntityQuery(
                ComponentType.ReadWrite<PvPSpawnProtection>(),
                ComponentType.ReadWrite<DamageEvent>());
            RequireForUpdate(_protectedQuery);
            RequireForUpdate<PvPMatchState>();
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            uint currentTick = networkTime.ServerTick.TickIndexForValidTick;

            var entities = _protectedQuery.ToEntityArray(Allocator.Temp);
            var protections = _protectedQuery.ToComponentDataArray<PvPSpawnProtection>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                if (!EntityManager.IsComponentEnabled<PvPSpawnProtection>(entities[i]))
                    continue;

                // Check expiration
                if (currentTick >= protections[i].ExpirationTick)
                {
                    EntityManager.SetComponentEnabled<PvPSpawnProtection>(entities[i], false);
                    continue;
                }

                // While protected, clear DamageEvent buffer to prevent damage
                var buffer = EntityManager.GetBuffer<DamageEvent>(entities[i]);
                if (buffer.Length > 0)
                    buffer.Clear();
            }

            entities.Dispose();
            protections.Dispose();
        }
    }
}
