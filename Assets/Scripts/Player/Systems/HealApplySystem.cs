using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Server-authoritative system that consumes HealEvent buffers and applies healing to Health (13.16.9).
    /// Mirrors the DamageApplySystem pattern for consistency.
    /// Replaces old HealRequest system.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateAfter(typeof(DamageApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct HealApplySystem : ISystem
    {
        private const int MaxEventsPerTick = 8;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            float currentTime = (float)SystemAPI.Time.ElapsedTime;

            new ApplyHealJob
            {
                CurrentTime = currentTime,
                MaxEvents = MaxEventsPerTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ApplyHealJob : IJobEntity
        {
            public float CurrentTime;
            public int MaxEvents;

            void Execute(
                ref Health health,
                ref DynamicBuffer<HealEvent> healBuffer,
                in DeathState deathState,
                [EntityIndexInQuery] int entityIndex)
            {
                // Skip if dead - no healing while dead
                if (deathState.Phase != DeathPhase.Alive)
                {
                    healBuffer.Clear();
                    return;
                }

                // Skip if at max health (optimization)
                if (health.Current >= health.Max)
                {
                    healBuffer.Clear();
                    return;
                }

                // Process heal events (capped to prevent runaway)
                int eventsToProcess = math.min(healBuffer.Length, MaxEvents);
                float totalHeal = 0f;

                for (int i = 0; i < eventsToProcess; i++)
                {
                    var heal = healBuffer[i];

                    // Validate heal amount
                    if (math.isnan(heal.Amount) || math.isinf(heal.Amount))
                        continue;

                    if (heal.Amount <= 0f)
                        continue;

                    totalHeal += heal.Amount;
                }

                // Apply total healing (capped at max health)
                if (totalHeal > 0f)
                {
                    health.Current = math.min(health.Max, health.Current + totalHeal);
                }

                // Clear buffer after processing
                healBuffer.Clear();
            }
        }
    }
}
