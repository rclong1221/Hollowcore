using System.Collections.Generic;
using Unity.Entities;

namespace DIG.Persistence
{
    /// <summary>
    /// EPIC 16.15: Managed singleton holding registered ISaveModule implementations
    /// and runtime persistence state. Created by PersistenceBootstrapSystem.
    /// </summary>
    public class SaveManagerSingleton : IComponentData
    {
        public List<ISaveModule> RegisteredModules = new();
        public Dictionary<int, ISaveModule> ModuleByTypeId = new();
        public string SaveDirectory;
        public SaveConfig Config;
        public float ElapsedPlaytime;
        public float TimeSinceLastSave;
        public float TimeSinceLastCheckpoint;
        public bool IsInitialized;
    }
}
