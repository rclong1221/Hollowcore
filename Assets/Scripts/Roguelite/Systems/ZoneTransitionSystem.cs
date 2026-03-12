using DIG.Roguelite;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Roguelite.Zones
{
    /// <summary>
    /// Manages IZoneProvider lifecycle: Initialize -> poll IsReady -> Activate -> Deactivate.
    /// Creates ZoneState component. Teleports player. Calls IInteractableHandler on activation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ZoneSequenceResolverSystem))]
    public partial class ZoneTransitionSystem : SystemBase
    {
        private IZoneProvider _zoneProvider;
        private ISpawnPositionProvider _spawnPositionProvider;
        private IInteractableHandler _interactableHandler;
        private ZoneActivationResult _lastActivation;
        private bool _waitingForReady;

        // Cached system references — avoid GetExistingSystemManaged per phase change
        private ZoneSequenceResolverSystem _sequencer;
        private InteractableDirectorSystem _interactableDirector;
        private bool _systemsCached;

        /// <summary>Register the zone provider. Call before run starts.</summary>
        public void SetZoneProvider(IZoneProvider provider) => _zoneProvider = provider;

        /// <summary>Register the spawn position provider for the spawn director.</summary>
        public void SetSpawnPositionProvider(ISpawnPositionProvider provider) => _spawnPositionProvider = provider;

        /// <summary>Register the interactable handler for zone activation.</summary>
        public void SetInteractableHandler(IInteractableHandler handler) => _interactableHandler = handler;

        /// <summary>Last zone activation result. Read by SpawnDirectorSystem for spawn points.</summary>
        public ZoneActivationResult LastActivation => _lastActivation;

        /// <summary>Spawn position provider for the spawn director.</summary>
        public ISpawnPositionProvider SpawnPositionProvider => _spawnPositionProvider;

        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
            RequireForUpdate<RunConfigSingleton>();
        }

        protected override void OnUpdate()
        {
            // Cache system references once
            if (!_systemsCached)
            {
                _sequencer = World.GetExistingSystemManaged<ZoneSequenceResolverSystem>();
                _interactableDirector = World.GetExistingSystemManaged<InteractableDirectorSystem>();
                _systemsCached = true;
            }

            var runEntity = SystemAPI.GetSingletonEntity<RunState>();
            var run = SystemAPI.GetSingleton<RunState>();

            // Poll IsReady during ZoneLoading
            if (_waitingForReady && run.Phase == RunPhase.ZoneLoading)
            {
                if (_zoneProvider != null && _zoneProvider.IsReady)
                {
                    ActivateZone(runEntity, ref run);
                    _waitingForReady = false;
                }
                return;
            }

            if (!EntityManager.IsComponentEnabled<RunPhaseChangedTag>(runEntity))
                return;

            switch (run.Phase)
            {
                case RunPhase.Preparation:
                    // Start loading the first zone
                    StartZoneLoad(runEntity, ref run, 0);
                    break;

                case RunPhase.ZoneTransition:
                    // Deactivate current zone, start loading next
                    DeactivateCurrentZone();
                    int nextZone = run.CurrentZoneIndex + 1;
                    StartZoneLoad(runEntity, ref run, nextZone);
                    break;

                case RunPhase.RunEnd:
                    DeactivateCurrentZone();
                    break;
            }
        }

        private void StartZoneLoad(Entity runEntity, ref RunState run, int zoneIndex)
        {
            if (_zoneProvider == null)
            {
                // No zone provider registered — skip loading, go straight to Active
                // This allows the framework to run without zone orchestration (standalone test, etc)
                run.CurrentZoneIndex = (byte)zoneIndex;
                run.Phase = RunPhase.Active;
                SystemAPI.SetSingleton(run);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ZoneTransition] No IZoneProvider registered. Skipping to Active for zone {zoneIndex}.");
#endif
                return;
            }

            var sequencer = _sequencer;
            ZoneDefinitionSO zoneDef = null;

            if (sequencer != null && sequencer.IsResolved)
            {
                zoneDef = sequencer.GetZoneAtIndex(zoneIndex);

                // If we've exhausted zones, check looping
                if (zoneDef == null && sequencer.ResolvedZoneCount > 0)
                {
                    // Attempt loop extension
                    sequencer.ExtendLoop(run.Seed, run.AscensionLevel);
                    zoneDef = sequencer.GetZoneAtIndex(zoneIndex);
                }
            }

            // Transition to ZoneLoading
            run.CurrentZoneIndex = (byte)zoneIndex;
            run.Phase = RunPhase.ZoneLoading;
            SystemAPI.SetSingleton(run);

            uint zoneSeed = RunSeedUtility.DeriveZoneSeed(run.Seed, (byte)zoneIndex);
            _zoneProvider.Initialize(zoneSeed, zoneIndex, zoneDef);
            _waitingForReady = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string zoneName = zoneDef != null ? zoneDef.DisplayName : "null";
            Debug.Log($"[ZoneTransition] Loading zone {zoneIndex}: '{zoneName}' (seed={zoneSeed})");
#endif
        }

        private void ActivateZone(Entity runEntity, ref RunState run)
        {
            _lastActivation = _zoneProvider.Activate();

            var sequencer = _sequencer;
            var zoneDef = sequencer?.GetZoneAtIndex(run.CurrentZoneIndex);

            // Initialize ZoneState
            float baseDifficulty = 1f;
            ref var configBlob = ref SystemAPI.GetSingleton<RunConfigSingleton>().Config.Value;
            if (run.CurrentZoneIndex < configBlob.DifficultyMultiplierPerZone.Length)
                baseDifficulty = configBlob.DifficultyMultiplierPerZone[run.CurrentZoneIndex];

            float zoneMult = zoneDef != null ? zoneDef.DifficultyMultiplier : 1f;
            float loopMult = 1f;
            if (sequencer != null && sequencer.LoopCount > 0)
                loopMult = math.pow(sequencer.LoopDifficultyMultiplier, sequencer.LoopCount);

            var zoneState = new ZoneState
            {
                ZoneIndex = run.CurrentZoneIndex,
                ZoneId = zoneDef != null ? zoneDef.ZoneId : 0,
                Type = zoneDef != null ? zoneDef.Type : ZoneType.Combat,
                ClearMode = zoneDef != null ? zoneDef.ClearMode : ZoneClearMode.AllEnemiesDead,
                TimeInZone = 0f,
                EffectiveDifficulty = baseDifficulty * zoneMult * loopMult,
                SpawnBudget = zoneDef?.SpawnDirectorConfig != null ? zoneDef.SpawnDirectorConfig.InitialBudget : 0f,
                LoopCount = sequencer?.LoopCount ?? 0,
            };

            if (!EntityManager.HasComponent<ZoneState>(runEntity))
                EntityManager.AddComponentData(runEntity, zoneState);
            else
                EntityManager.SetComponentData(runEntity, zoneState);

            // Reset ZoneExitActivated
            if (EntityManager.HasComponent<ZoneExitActivated>(runEntity))
                EntityManager.SetComponentEnabled<ZoneExitActivated>(runEntity, false);

            // Place interactables
            if (_interactableHandler != null && _lastActivation.InteractableNodes != null
                && zoneDef != null && zoneDef.InteractablePool != null && zoneDef.InteractableBudget > 0)
            {
                _interactableDirector?.PlaceInteractables(
                    _lastActivation.InteractableNodes,
                    zoneDef.InteractablePool,
                    RunSeedUtility.DeriveInteractableSeed(run.ZoneSeed),
                    zoneState.EffectiveDifficulty,
                    zoneDef.InteractableBudget,
                    _interactableHandler);
            }

            // Transition to Active (or BossEncounter for boss zones)
            run.Phase = zoneState.Type == ZoneType.Boss ? RunPhase.BossEncounter : RunPhase.Active;
            SystemAPI.SetSingleton(run);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ZoneTransition] Zone {run.CurrentZoneIndex} activated. " +
                      $"Difficulty={zoneState.EffectiveDifficulty:F2}, Type={zoneState.Type}, Clear={zoneState.ClearMode}");
#endif
        }

        private void DeactivateCurrentZone()
        {
            _interactableHandler?.ClearInteractables();
            _zoneProvider?.Deactivate();
        }

        protected override void OnDestroy()
        {
            _zoneProvider?.Dispose();
        }

    }
}
