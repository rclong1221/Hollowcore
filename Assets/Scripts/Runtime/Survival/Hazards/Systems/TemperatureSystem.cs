using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Survival.Hazards
{
    /// <summary>
    /// Updates body temperature based on environment zone.
    /// Temperature changes over time toward zone temperature.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(EnvironmentZoneDetectionSystem))]
    [RequireMatchingQueriesForUpdate]
    public partial struct TemperatureSystem : ISystem
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
            if (!networkTime.IsFirstTimeFullyPredictingTick)
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (bodyTemp, inZone) in
                     SystemAPI.Query<RefRW<BodyTemperature>, RefRO<InEnvironmentZone>>()
                     .WithAll<Simulate, TemperatureSusceptible>())
            {
                ref var temp = ref bodyTemp.ValueRW;
                var zone = inZone.ValueRO;

                // Set target temperature from zone
                temp.TargetTemp = zone.ZoneTemperature;

                // Move current temperature toward target
                float diff = temp.TargetTemp - temp.Current;
                float change = math.sign(diff) * math.min(math.abs(diff), temp.ChangeRatePerSecond * deltaTime);
                temp.Current += change;

                // Clamp to reasonable bounds
                temp.Current = math.clamp(temp.Current, -273f, 100f);
            }
        }
    }

    /// <summary>
    /// Applies damage when body temperature is outside safe range.
    /// Creates damage events for bridge system.
    /// Server-authoritative.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TemperatureSystem))]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct TemperatureDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (bodyTemp, entity) in
                     SystemAPI.Query<RefRO<BodyTemperature>>()
                     .WithAll<TemperatureSusceptible>()
                     .WithEntityAccess())
            {
                var temp = bodyTemp.ValueRO;

                if (!temp.IsTakingDamage)
                    continue;

                // Calculate damage based on severity
                float damage = temp.DamagePerSecond * temp.Severity * deltaTime;

                if (damage > 0.001f)
                {
                    // Create damage event for bridge system
                    var damageEventEntity = ecb.CreateEntity();
                    ecb.AddComponent(damageEventEntity, new TemperatureDamageEvent
                    {
                        TargetEntity = entity,
                        Damage = damage,
                        IsCold = temp.IsCold
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Cleans up temperature damage events after processing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [RequireMatchingQueriesForUpdate]
    public partial struct TemperatureDamageEventCleanupSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<TemperatureDamageEvent>>()
                     .WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
