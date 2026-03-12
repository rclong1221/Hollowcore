using Unity.Entities;

namespace DIG.Localization
{
    /// <summary>
    /// Detects LocaleConfig changes via change filter.
    /// When a change is detected, creates a transient entity with LocaleChangedTag.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class LocaleChangeSystem : SystemBase
    {
        private byte _lastLocaleId = 255;

        protected override void OnCreate()
        {
            RequireForUpdate<LocaleConfig>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<LocaleConfig>();
            if (config.CurrentLocaleId == _lastLocaleId) return;

            if (_lastLocaleId != 255)
            {
                var tagEntity = EntityManager.CreateEntity();
                EntityManager.AddComponent<LocaleChangedTag>(tagEntity);
            }

            _lastLocaleId = config.CurrentLocaleId;
        }
    }
}
