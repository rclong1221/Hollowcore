using Unity.Burst;
using Unity.Entities;
using DIG.Roguelite.Analytics;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#endif

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: On RunEnd (frame 1), converts RunCurrency to MetaCurrency using
    /// the config's conversion rate. Updates MetaBank lifetime stats and records
    /// a RunHistoryEntry. Uses a flag to ensure single execution per run end.
    ///
    /// Timing: Runs after RunEndSystem which calculates the score on frame 1 and
    /// transitions to MetaScreen on frame 2. This system reads RunPhase.RunEnd
    /// on frame 1 with the finalized score.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunEndSystem))]
    [BurstCompile]
    public partial struct MetaCurrencyConversionSystem : ISystem
    {
        private bool _converted;
        private RunPhase _lastPhase;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunState>();
            state.RequireForUpdate<RunConfigSingleton>();
            state.RequireForUpdate<MetaBank>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var run = SystemAPI.GetSingleton<RunState>();

            // Reset conversion flag when phase leaves RunEnd
            if (run.Phase != _lastPhase)
            {
                if (_lastPhase == RunPhase.RunEnd)
                    _converted = false;
                _lastPhase = run.Phase;
            }

            // Only process on RunEnd, once per run
            if (run.Phase != RunPhase.RunEnd || _converted)
                return;

            _converted = true;

            ref var config = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;

            // Calculate meta-currency earned
            int metaEarned = (int)(run.RunCurrency * config.MetaCurrencyConversionRate);

            // Update MetaBank
            var bankEntity = SystemAPI.GetSingletonEntity<MetaBank>();
            var bank = SystemAPI.GetSingleton<MetaBank>();

            bank.MetaCurrency += metaEarned;
            bank.LifetimeMetaEarned += metaEarned;
            bank.TotalRunsAttempted++;
            bank.TotalPlaytime += run.ElapsedTime;

            if (run.EndReason == RunEndReason.BossDefeated)
                bank.TotalRunsWon++;

            if (run.Score > bank.BestScore)
                bank.BestScore = run.Score;

            if (run.CurrentZoneIndex > bank.BestZoneReached)
                bank.BestZoneReached = run.CurrentZoneIndex;

            SystemAPI.SetSingleton(bank);

            // Read run statistics if available (EPIC 23.6)
            int totalKills = 0;
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            if (state.EntityManager.HasComponent<RunStatistics>(runEntity))
                totalKills = state.EntityManager.GetComponentData<RunStatistics>(runEntity).TotalKills;

            // Record run history entry
            var historyBuffer = SystemAPI.GetBuffer<RunHistoryEntry>(bankEntity);
            historyBuffer.Add(new RunHistoryEntry
            {
                RunId = run.RunId,
                Seed = run.Seed,
                AscensionLevel = run.AscensionLevel,
                EndReason = run.EndReason,
                ZonesCleared = run.CurrentZoneIndex,
                Score = run.Score,
                Duration = run.ElapsedTime,
                MetaCurrencyEarned = metaEarned,
                TotalKills = totalKills,
                Timestamp = GetUnixTimestamp()
            });

            // Cap history buffer at 50 entries (remove oldest in one pass)
            const int maxHistory = 50;
            int excess = historyBuffer.Length - maxHistory;
            if (excess > 0)
                historyBuffer.RemoveRange(0, excess);

            LogConversion(run.RunId, run.RunCurrency, metaEarned, config.MetaCurrencyConversionRate, bank.MetaCurrency);
        }

        [BurstDiscard]
        private static void LogConversion(uint runId, int runCurrency, int metaEarned, float rate, int totalMeta)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MetaConversion] Run {runId}: {runCurrency} run currency × {rate:F2} = {metaEarned} meta-currency. Total: {totalMeta}");
#endif
        }

        /// <summary>
        /// Returns current Unix timestamp in seconds. Burst-compatible via long math.
        /// </summary>
        private static long GetUnixTimestamp()
        {
            // Burst can't call System.DateTimeOffset, use fallback
            long timestamp = 0;
            GetManagedTimestamp(ref timestamp);
            return timestamp;
        }

        [BurstDiscard]
        private static void GetManagedTimestamp(ref long timestamp)
        {
            timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
