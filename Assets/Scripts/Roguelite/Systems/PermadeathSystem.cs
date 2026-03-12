using Unity.Burst;
using Unity.Entities;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#endif

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Watches for PermadeathSignal (enabled by game-side death bridge).
    /// Sets RunPhase = RunEnd, EndReason = PlayerDeath.
    /// Burst-compiled ISystem — checks 1 entity, early-exits most frames.
    ///
    /// Game integration: The game's Assembly-CSharp code must provide a bridge system
    /// that queries players with Health.IsDepleted / DeathState.Phase == Dead and enables
    /// PermadeathSignal on the RunState entity. Example:
    ///
    ///   [UpdateInGroup(typeof(SimulationSystemGroup))]
    ///   [UpdateBefore(typeof(PermadeathSystem))]
    ///   public partial class PermadeathBridgeSystem : SystemBase
    ///   {
    ///       protected override void OnUpdate()
    ///       {
    ///           var run = SystemAPI.GetSingleton&lt;RunState&gt;();
    ///           if (run.Phase != RunPhase.Active &amp;&amp; run.Phase != RunPhase.BossEncounter)
    ///               return;
    ///           foreach (var (death, _) in SystemAPI.Query&lt;RefRO&lt;DeathState&gt;, RefRO&lt;PlayerTag&gt;&gt;())
    ///           {
    ///               if (death.ValueRO.Phase == DeathPhase.Dead)
    ///               {
    ///                   var runEntity = SystemAPI.GetSingletonEntity&lt;RunState&gt;();
    ///                   EntityManager.SetComponentEnabled&lt;PermadeathSignal&gt;(runEntity, true);
    ///                   return;
    ///               }
    ///           }
    ///       }
    ///   }
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunLifecycleSystem))]
    [BurstCompile]
    public partial struct PermadeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var run = SystemAPI.GetSingleton<RunState>();

            // Only trigger during active gameplay
            if (run.Phase != RunPhase.Active && run.Phase != RunPhase.BossEncounter)
                return;

            // Check for PermadeathSignal from game-side bridge
            if (!state.EntityManager.IsComponentEnabled<PermadeathSignal>(runEntity))
                return;

            // Transition to RunEnd
            run.Phase = RunPhase.RunEnd;
            run.EndReason = RunEndReason.PlayerDeath;
            SystemAPI.SetSingleton(run);

            // Consume signal
            state.EntityManager.SetComponentEnabled<PermadeathSignal>(runEntity, false);

            LogDeath(run.RunId, run.CurrentZoneIndex);
        }

        [BurstDiscard]
        private static void LogDeath(uint runId, byte zoneIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Permadeath] Player death detected. Run {runId} ended at zone {zoneIndex}.");
#endif
        }
    }
}
