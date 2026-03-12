using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Registers all ISaveModule implementations, creates SaveManagerSingleton,
    /// ensures save directory exists. Runs once at startup.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PersistenceBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;
            _initialized = true;

            var config = Resources.Load<SaveConfig>("SaveConfig");
            if (config == null)
            {
                Debug.LogWarning("[Persistence] No SaveConfig found at Resources/SaveConfig. Using defaults.");
                config = ScriptableObject.CreateInstance<SaveConfig>();
            }

            string saveDir = System.IO.Path.Combine(Application.persistentDataPath, config.SaveDirectory);
            if (!System.IO.Directory.Exists(saveDir))
                System.IO.Directory.CreateDirectory(saveDir);

            var manager = new SaveManagerSingleton
            {
                Config = config,
                SaveDirectory = saveDir,
                IsInitialized = true,
                ElapsedPlaytime = 0f,
                TimeSinceLastSave = 0f,
                TimeSinceLastCheckpoint = 0f
            };

            // Register all built-in modules
            RegisterModule(manager, new PlayerStatsSaveModule());
            RegisterModule(manager, new InventorySaveModule());
            RegisterModule(manager, new EquipmentSaveModule());
            RegisterModule(manager, new QuestSaveModule());
            RegisterModule(manager, new CraftingSaveModule());
            RegisterModule(manager, new WorldSaveModule(config.CompressWorldData));
            RegisterModule(manager, new SettingsSaveModule());
            RegisterModule(manager, new StatusEffectsSaveModule());
            RegisterModule(manager, new SurvivalSaveModule());
            RegisterModule(manager, new ProgressionSaveModule());
            RegisterModule(manager, new TalentSaveModule());
            RegisterModule(manager, new PartySaveModule());
            RegisterModule(manager, new MapSaveModule());
            RegisterModule(manager, new AchievementSaveModule());
            RegisterModule(manager, new MetaProgressionSaveModule());
            RegisterModule(manager, new RunHistorySaveModule());

            // Create singleton entity
            var singletonEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(singletonEntity, manager);

            // Start background writer thread
            SaveFileWriter.Start();

            Debug.Log($"[Persistence] Initialized with {manager.RegisteredModules.Count} modules. Save dir: {saveDir}");
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            SaveFileWriter.Stop();
        }

        private static void RegisterModule(SaveManagerSingleton manager, ISaveModule module)
        {
            manager.RegisteredModules.Add(module);
            manager.ModuleByTypeId[module.TypeId] = module;
        }
    }
}
