using DIG.Roguelite.Zones;
using Unity.Entities;
using UnityEngine;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.1: Loads RunConfigSO from Resources/, builds RunConfigBlob via BlobBuilder,
    /// creates RunConfigSingleton and RunState entities. Follows ProgressionBootstrapSystem pattern.
    /// Runs once at startup, then self-disables.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class RunConfigBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            // Load run config from Resources/
            var configSO = Resources.Load<RunConfigSO>("RunConfig");
            if (configSO == null)
            {
                Debug.LogWarning("[RunConfigBootstrap] No RunConfigSO found at Resources/RunConfig. Using defaults.");
                configSO = ScriptableObject.CreateInstance<RunConfigSO>();
                configSO.ConfigName = "Default";
                configSO.ZoneCount = 5;
            }

            // Build blob
            var blob = RunConfigBlobBuilder.Build(configSO);

            // Create config singleton entity
            var configEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(configEntity, new RunConfigSingleton { Config = blob });
#if UNITY_EDITOR
            EntityManager.SetName(configEntity, "RunConfig");
#endif

            // Create RunState entity with all required components
            var runEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(runEntity, new RunState
            {
                RunId = 0,
                Seed = 0,
                Phase = RunPhase.None,
                EndReason = RunEndReason.None,
                CurrentZoneIndex = 0,
                MaxZones = (byte)configSO.ZoneCount,
                ElapsedTime = 0f,
                Score = 0,
                RunCurrency = 0,
                AscensionLevel = 0,
                ZoneSeed = 0
            });
            EntityManager.AddComponentData(runEntity, default(RunPhaseChangedTag));
            EntityManager.SetComponentEnabled<RunPhaseChangedTag>(runEntity, false);
            EntityManager.AddComponentData(runEntity, default(PermadeathSignal));
            EntityManager.SetComponentEnabled<PermadeathSignal>(runEntity, false);

            // EPIC 23.3: Zone state and exit trigger on RunState entity
            EntityManager.AddComponentData(runEntity, default(ZoneExitActivated));
            EntityManager.SetComponentEnabled<ZoneExitActivated>(runEntity, false);

            // EPIC 23.4: Modifier buffers and acquisition request on RunState entity
            EntityManager.AddBuffer<RunModifierStack>(runEntity);
            EntityManager.AddBuffer<PendingModifierChoice>(runEntity);
            EntityManager.AddComponentData(runEntity, default(ModifierAcquisitionRequest));
            EntityManager.SetComponentEnabled<ModifierAcquisitionRequest>(runEntity, false);
#if UNITY_EDITOR
            EntityManager.SetName(runEntity, "RunState");
#endif

            Debug.Log($"[RunConfigBootstrap] Loaded run config: '{configSO.ConfigName}' (Id={configSO.ConfigId}, Zones={configSO.ZoneCount})");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (!_initialized) return;

            foreach (var config in SystemAPI.Query<RefRO<RunConfigSingleton>>())
            {
                if (config.ValueRO.Config.IsCreated)
                    config.ValueRO.Config.Dispose();
            }
        }
    }
}
