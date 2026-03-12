using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.4: Handles modifier acquisition requests and generates modifier choices
    /// on zone transitions. UI sends ModifierAcquisitionRequest; this system validates
    /// and adds to RunModifierStack. On ZoneTransition, generates deterministic choices
    /// from the modifier pool using the zone seed.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AscensionSetupSystem))]
    [UpdateBefore(typeof(DifficultyScalingSystem))]
    [BurstCompile]
    public partial struct ModifierAcquisitionSystem : ISystem
    {
        private bool _choicesGenerated;
        private RunPhase _lastPhase;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunState>();
            state.RequireForUpdate<ModifierRegistrySingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var run = SystemAPI.GetSingleton<RunState>();

            // Track phase changes for choice generation reset
            if (run.Phase != _lastPhase)
            {
                if (_lastPhase == RunPhase.ZoneTransition)
                    _choicesGenerated = false;
                _lastPhase = run.Phase;
            }

            // Process acquisition request (from UI or reward systems)
            if (SystemAPI.IsComponentEnabled<ModifierAcquisitionRequest>(runEntity))
            {
                ProcessAcquisitionRequest(ref state, runEntity);
            }

            // Generate modifier choices on zone transition
            if (run.Phase == RunPhase.ZoneTransition && !_choicesGenerated)
            {
                _choicesGenerated = true;
                GenerateModifierChoices(ref state, runEntity, run);
            }
        }

        private void ProcessAcquisitionRequest(ref SystemState state, Entity runEntity)
        {
            SystemAPI.SetComponentEnabled<ModifierAcquisitionRequest>(runEntity, false);

            var request = SystemAPI.GetComponent<ModifierAcquisitionRequest>(runEntity);
            ref var registry = ref SystemAPI.GetSingleton<ModifierRegistrySingleton>().Registry.Value;
            var modStack = SystemAPI.GetBuffer<RunModifierStack>(runEntity);

            bool added = ModifierStackUtility.TryAddModifier(ref registry, modStack, request.ModifierId);

            LogAcquisition(request.ModifierId, added);

            // Clear choices after selection
            if (SystemAPI.HasBuffer<PendingModifierChoice>(runEntity))
                SystemAPI.GetBuffer<PendingModifierChoice>(runEntity).Clear();

            // Notify UI
            if (added)
                NotifyUI(request.ModifierId);
        }

        private void GenerateModifierChoices(ref SystemState state, Entity runEntity, RunState run)
        {
            if (!SystemAPI.HasBuffer<PendingModifierChoice>(runEntity))
                return;

            ref var registry = ref SystemAPI.GetSingleton<ModifierRegistrySingleton>().Registry.Value;
            if (registry.Modifiers.Length == 0)
                return;

            var choices = SystemAPI.GetBuffer<PendingModifierChoice>(runEntity);
            choices.Clear();

            var modStack = SystemAPI.GetBuffer<RunModifierStack>(runEntity);

            // Deterministic selection using zone seed
            uint seed = RunSeedUtility.DeriveModifierSeed(run.ZoneSeed);
            var rng = Random.CreateFromIndex(seed);

            const int choiceCount = 3;
            int attempts = 0;
            const int maxAttempts = 30; // Prevent infinite loop if pool is small

            while (choices.Length < choiceCount && attempts < maxAttempts)
            {
                attempts++;
                int idx = rng.NextInt(0, registry.Modifiers.Length);
                ref var def = ref registry.Modifiers[idx];

                // Skip if ascension requirement not met
                if (def.RequiredAscensionLevel > run.AscensionLevel)
                    continue;

                // Skip if already at max stacks
                if (!ModifierStackUtility.CanAddModifier(ref registry, modStack, def.ModifierId))
                    continue;

                // Skip duplicates in current choices
                bool duplicate = false;
                for (int c = 0; c < choices.Length; c++)
                {
                    if (choices[c].ModifierId == def.ModifierId)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate) continue;

                choices.Add(new PendingModifierChoice
                {
                    ModifierId = def.ModifierId,
                    Polarity = def.Polarity,
                    Target = def.Target,
                    StatId = def.StatId,
                    FloatValue = def.FloatValue,
                    IsMultiplicative = def.IsMultiplicative
                });
            }

            LogChoicesGenerated(choices.Length, run.CurrentZoneIndex);
        }

        [BurstDiscard]
        private static void LogAcquisition(int modifierId, bool success)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[ModifierAcquisition] Modifier {modifierId}: {(success ? "Added" : "Failed")}");
#endif
        }

        [BurstDiscard]
        private static void LogChoicesGenerated(int count, byte zoneIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"[ModifierAcquisition] Generated {count} modifier choices for zone {zoneIndex} transition.");
#endif
        }

        [BurstDiscard]
        private static void NotifyUI(int modifierId)
        {
            if (ModifierUIRegistry.HasProvider)
                ModifierUIRegistry.Provider.OnModifierAcquired(modifierId);
        }
    }
}
