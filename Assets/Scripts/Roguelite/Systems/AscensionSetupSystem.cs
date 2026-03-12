using Unity.Burst;
using Unity.Entities;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
#endif

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: On RunPhase.Preparation, clears the modifier stack and force-applies
    /// all ascension-tier forced modifiers (cumulative up to current AscensionLevel).
    /// Runs once per run start, resets when phase leaves Preparation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RunInitSystem))]
    [BurstCompile]
    public partial struct AscensionSetupSystem : ISystem
    {
        private bool _applied;
        private RunPhase _lastPhase;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunState>();
            state.RequireForUpdate<AscensionSingleton>();
            state.RequireForUpdate<ModifierRegistrySingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var run = SystemAPI.GetSingleton<RunState>();

            // Reset when leaving Preparation so next run triggers again
            if (run.Phase != _lastPhase)
            {
                if (_lastPhase == RunPhase.Preparation)
                    _applied = false;
                _lastPhase = run.Phase;
            }

            if (run.Phase != RunPhase.Preparation || _applied)
                return;

            _applied = true;

            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            if (!SystemAPI.HasBuffer<RunModifierStack>(runEntity))
                return;

            ref var ascension = ref SystemAPI.GetSingleton<AscensionSingleton>().Ascension.Value;
            ref var registry = ref SystemAPI.GetSingleton<ModifierRegistrySingleton>().Registry.Value;

            // Clear modifier stack for fresh run
            var modStack = SystemAPI.GetBuffer<RunModifierStack>(runEntity);
            modStack.Clear();

            // Apply forced modifiers from all tiers up to current ascension level (cumulative)
            for (int t = 0; t < ascension.Tiers.Length; t++)
            {
                ref var tier = ref ascension.Tiers[t];
                if (tier.Level > run.AscensionLevel)
                    continue;

                for (int m = 0; m < tier.ForcedModifierIds.Length; m++)
                {
                    int modId = tier.ForcedModifierIds[m];
                    ModifierStackUtility.TryAddModifier(ref registry, modStack, modId);
                }
            }

            LogAscensionSetup(run.AscensionLevel, modStack.Length);
        }

        [BurstDiscard]
        private static void LogAscensionSetup(byte level, int modCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AscensionSetup] Level {level}: {modCount} forced modifiers applied.");
#endif
        }
    }
}
