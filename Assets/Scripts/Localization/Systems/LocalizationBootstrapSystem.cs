using Unity.Entities;
using UnityEngine;

namespace DIG.Localization
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation |
                        WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class LocalizationBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var database = Resources.Load<LocalizationDatabase>("LocalizationDatabase");
            if (database == null)
            {
                Debug.LogError("[LocalizationBootstrapSystem] No LocalizationDatabase found at Resources/LocalizationDatabase. Localization disabled.");
                _initialized = true;
                Enabled = false;
                return;
            }

            LocalizationManager.Initialize(database);

            byte currentIdx = 0;
            byte fallbackIdx = 0;
            for (int i = 0; i < database.Locales.Count; i++)
            {
                if (database.Locales[i] == null) continue;
                if (database.Locales[i].LocaleCode == LocalizationManager.CurrentLocaleCode)
                    currentIdx = (byte)i;
                if (database.Locales[i].LocaleCode == database.DefaultLocaleCode)
                    fallbackIdx = (byte)i;
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new LocaleConfig
            {
                CurrentLocaleId = currentIdx,
                FallbackLocaleId = fallbackIdx,
                Reserved = 0
            });

            _initialized = true;
            Enabled = false;
        }
    }
}
