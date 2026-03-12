using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Updates Downed players.
    /// - Transitions Downed -> Dead after BleedOutDuration.
    /// - Processes ReviveRequests to transition Downed -> Alive.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DamageApplySystem))] // Check rules before new damage
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DownedRulesSystem : ISystem
    {
        public const float BleedOutDuration = 60.0f;
        public const float ReviveHealthAmount = 25.0f; // Epic 4.5.2

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            double currentTime = SystemAPI.Time.ElapsedTime;

            new DownedJob
            {
                CurrentTime = currentTime,
                BleedOutTime = BleedOutDuration,
                ReviveAmount = ReviveHealthAmount
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct DownedJob : IJobEntity
        {
            public double CurrentTime;
            public float BleedOutTime;
            public float ReviveAmount;

            void Execute(
                ref DeathState deathState,
                ref Health health,
                ref DynamicBuffer<ReviveRequest> reviveRequests)
            {
                if (deathState.Phase != DeathPhase.Downed)
                {
                    reviveRequests.Clear();
                    return;
                }

                // Check for Revive
                if (!reviveRequests.IsEmpty)
                {
                    // Revive! (MVP: Accept first validation)
                    deathState.Phase = DeathPhase.Alive;
                    health.Current = ReviveAmount;
                    reviveRequests.Clear();
                    return;
                }

                // Check Bleed Out
                float timeDown = (float)(CurrentTime - deathState.StateStartTime);
                if (timeDown >= BleedOutTime)
                {
                    deathState.Phase = DeathPhase.Dead;
                    deathState.StateStartTime = (float)CurrentTime; // Reset timer for dead phase
                }
            }
        }
    }
}
