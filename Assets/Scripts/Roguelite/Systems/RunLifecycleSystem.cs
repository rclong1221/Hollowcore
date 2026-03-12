using Unity.Burst;
using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Core state machine. Ticks ElapsedTime, derives ZoneSeed,
    /// validates phase transitions, toggles RunPhaseChangedTag. Time limit enforcement.
    /// Burst-compiled ISystem — runs every frame during active gameplay.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct RunLifecycleSystem : ISystem
    {
        private RunPhase _previousPhase;
        private byte _previousZoneIndex;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunState>();
            state.RequireForUpdate<RunConfigSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var run = SystemAPI.GetSingleton<RunState>();
            ref var config = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;

            bool dirty = false;

            // Tick elapsed time during active gameplay phases
            if (run.Phase == RunPhase.Active || run.Phase == RunPhase.BossEncounter)
            {
                run.ElapsedTime += SystemAPI.Time.DeltaTime;
                dirty = true;

                // Run time limit enforcement
                if (config.RunTimeLimit > 0f && run.ElapsedTime >= config.RunTimeLimit)
                {
                    run.Phase = RunPhase.RunEnd;
                    run.EndReason = RunEndReason.TimedOut;
                }
            }

            // Derive zone seed only when zone index actually changes
            if (run.CurrentZoneIndex != _previousZoneIndex)
            {
                run.ZoneSeed = RunSeedUtility.DeriveZoneSeed(run.Seed, run.CurrentZoneIndex);
                _previousZoneIndex = run.CurrentZoneIndex;
                dirty = true;
            }

            // Detect phase change and toggle tag
            if (run.Phase != _previousPhase)
            {
                state.EntityManager.SetComponentEnabled<RunPhaseChangedTag>(runEntity, true);
                _previousPhase = run.Phase;
                dirty = true;
            }

            // Only write back when something actually changed
            if (dirty)
                SystemAPI.SetSingleton(run);
        }
    }
}
