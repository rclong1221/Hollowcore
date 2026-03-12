using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using DIG.Survival.Explosives;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Bridge system that converts ExplosionDamageEvent into DamageEvent.
    /// Integrates explosion damage into the unified damage pipeline.
    /// </summary>
    /// <remarks>
    /// Design goals (EPIC 4.1):
    /// - All damage goes through DamageEvent, including explosions
    /// - Server-only: runs in SimulationSystemGroup
    /// - Runs before DamageApplySystem (in DamageSystemGroup order)
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct ExplosionDamageBridgeSystem : ISystem
    {
        private BufferLookup<DamageEvent> _damageEventLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _damageEventLookup = state.GetBufferLookup<DamageEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _damageEventLookup.Update(ref state);
            
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            
            // Early out if server tick is not yet valid (early in game startup)
            if (!networkTime.ServerTick.IsValid)
                return;
                
            uint serverTick = networkTime.ServerTick.TickIndexForValidTick;

            foreach (var (explosionDamage, entity) in
                     SystemAPI.Query<RefRO<ExplosionDamageEvent>>()
                     .WithEntityAccess())
            {
                var targetEntity = explosionDamage.ValueRO.TargetEntity;
                
                // Only add damage if target has the DamageEvent buffer
                if (_damageEventLookup.HasBuffer(targetEntity))
                {
                    var damageBuffer = _damageEventLookup[targetEntity];
                    damageBuffer.Add(new DamageEvent
                    {
                        Amount = explosionDamage.ValueRO.Damage,
                        Type = DamageType.Explosion,
                        SourceEntity = explosionDamage.ValueRO.SourceEntity,
                        HitPosition = Unity.Mathematics.float3.zero, // Could use explosion center
                        ServerTick = serverTick
                    });
                }
            }
        }
    }

    /// <summary>
    /// Bridge system that converts SurvivalDamageEvent into DamageEvent.
    /// Integrates suffocation, radiation, temperature damage into the unified pipeline.
    /// </summary>
    /// <remarks>
    /// This replaces the existing SurvivalDamageAdapterSystem's direct health modification.
    /// Server-only: damage application must happen on the server for authoritative health.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DamageSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct SurvivalDamageBridgeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            
            // Early out if server tick is not yet valid (early in game startup)
            if (!networkTime.ServerTick.IsValid)
                return;
                
            uint serverTick = networkTime.ServerTick.TickIndexForValidTick;

            new ConvertSurvivalDamageJob
            {
                ServerTick = serverTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ConvertSurvivalDamageJob : IJobEntity
        {
            public uint ServerTick;

            void Execute(
                ref DynamicBuffer<DamageEvent> damageBuffer,
                ref DIG.Survival.Core.SurvivalDamageEvent survivalDamage)
            {
                // Skip if no damage pending
                if (survivalDamage.PendingDamage <= 0f)
                    return;

                // Convert survival damage source to DamageType
                DamageType damageType = survivalDamage.Source switch
                {
                    DIG.Survival.Core.SurvivalDamageSource.Suffocation => DamageType.Suffocation,
                    DIG.Survival.Core.SurvivalDamageSource.Radiation => DamageType.Radiation,
                    DIG.Survival.Core.SurvivalDamageSource.Hypothermia => DamageType.Heat,
                    DIG.Survival.Core.SurvivalDamageSource.Hyperthermia => DamageType.Heat,
                    DIG.Survival.Core.SurvivalDamageSource.Toxic => DamageType.Toxic,
                    _ => DamageType.Physical
                };

                // Add to damage buffer
                damageBuffer.Add(new DamageEvent
                {
                    Amount = survivalDamage.PendingDamage,
                    Type = damageType,
                    SourceEntity = Entity.Null,
                    HitPosition = Unity.Mathematics.float3.zero,
                    ServerTick = ServerTick
                });

                // Reset pending damage (consumed)
                survivalDamage.PendingDamage = 0f;
                survivalDamage.Source = DIG.Survival.Core.SurvivalDamageSource.None;
            }
        }
    }
}
