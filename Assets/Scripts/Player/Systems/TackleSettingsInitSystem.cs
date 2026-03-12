using Unity.Entities;
using Unity.NetCode;
using DIG.Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Epic 7.4.2: Initializes global tackle settings singleton on world creation.
    /// Runs once when the world is created (Server, Client, or Thin Client worlds).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial struct TackleSettingsInitSystem : ISystem
    {
        private bool _initialized;
        
        public void OnCreate(ref SystemState state)
        {
            _initialized = false;
        }
        
        public void OnUpdate(ref SystemState state)
        {
            // Only initialize once
            if (_initialized)
            {
                state.Enabled = false;
                return;
            }
            
            // Check if singleton already exists (another system may have created it)
            if (SystemAPI.HasSingleton<TackleSettings>())
            {
                _initialized = true;
                state.Enabled = false;
                return;
            }
            
            // Create singleton entity with default tackle settings
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, TackleSettings.Default);
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            state.EntityManager.SetName(entity, "TackleSettings (Singleton)");
            #endif
            
            _initialized = true;
            state.Enabled = false; // Only run once
        }
    }
}
