using Unity.Entities;
using DIG.Targeting.Components;

namespace DIG.Targeting.Systems
{
    /// <summary>
    /// EPIC 15.16: Syncs managed TargetLockSettingsManager to ECS TargetLockSettings singleton.
    /// Runs every frame in InitializationSystemGroup to ensure settings are fresh.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TargetLockSettingsSyncSystem : SystemBase
    {
        private int _lastVersion;
        
        protected override void OnCreate()
        {
            // Create singleton if it doesn't exist
            if (!SystemAPI.HasSingleton<TargetLockSettings>())
            {
                var entity = EntityManager.CreateEntity();
                EntityManager.AddComponentData(entity, new TargetLockSettings
                {
                    AllowTargetLock = true,
                    AllowAimAssist = true,
                    ShowIndicator = true,
                    Version = 0
                });
            }
            
            // Subscribe to managed settings changes
            TargetLockSettingsManager.Instance.OnSettingsChanged += OnManagedSettingsChanged;
            
            // Initial sync
            SyncFromManager();
        }
        
        protected override void OnDestroy()
        {
            TargetLockSettingsManager.Instance.OnSettingsChanged -= OnManagedSettingsChanged;
        }
        
        protected override void OnUpdate()
        {
            // Sync is event-driven via OnManagedSettingsChanged
            // But we check on update too in case event was missed
            var manager = TargetLockSettingsManager.Instance;
            
            if (SystemAPI.TryGetSingletonRW<TargetLockSettings>(out var settings))
            {
                bool needsUpdate = 
                    settings.ValueRO.AllowTargetLock != manager.AllowTargetLock ||
                    settings.ValueRO.AllowAimAssist != manager.AllowAimAssist ||
                    settings.ValueRO.ShowIndicator != manager.ShowIndicator;
                
                if (needsUpdate)
                {
                    settings.ValueRW.AllowTargetLock = manager.AllowTargetLock;
                    settings.ValueRW.AllowAimAssist = manager.AllowAimAssist;
                    settings.ValueRW.ShowIndicator = manager.ShowIndicator;
                    settings.ValueRW.Version++;
                }
            }
        }
        
        private void OnManagedSettingsChanged()
        {
            SyncFromManager();
        }
        
        private void SyncFromManager()
        {
            if (!SystemAPI.HasSingleton<TargetLockSettings>()) return;
            
            var manager = TargetLockSettingsManager.Instance;
            var settingsRW = SystemAPI.GetSingletonRW<TargetLockSettings>();
            
            settingsRW.ValueRW.AllowTargetLock = manager.AllowTargetLock;
            settingsRW.ValueRW.AllowAimAssist = manager.AllowAimAssist;
            settingsRW.ValueRW.ShowIndicator = manager.ShowIndicator;
            settingsRW.ValueRW.Version++;
        }
    }
}
