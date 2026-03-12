using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Loads TradeConfigSO from Resources/, creates TradeConfig singleton + TradeAuditLog buffer.
    /// Runs once at startup, then self-disables. Follows PartyBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TradeBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var configSO = Resources.Load<TradeConfigSO>("TradeConfig");
            if (configSO == null)
            {
                Debug.LogWarning("[TradeBootstrap] No TradeConfigSO found at Resources/TradeConfig. Using defaults.");
                configSO = ScriptableObject.CreateInstance<TradeConfigSO>();
            }

            // Read tick rate from NetCode singleton, fall back to 30Hz
            int tickRate = 30;
            if (SystemAPI.HasSingleton<ClientServerTickRate>())
                tickRate = SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;

            var singleton = new TradeConfig
            {
                MaxItemsPerOffer = Mathf.Clamp(configSO.MaxItemsPerOffer, 1, 16),
                MaxCurrencyPerOffer = Mathf.Clamp(configSO.MaxCurrencyPerOffer, 0, 3),
                ProximityRange = configSO.ProximityRange,
                TimeoutTicks = (uint)Mathf.RoundToInt(configSO.TimeoutSeconds * tickRate),
                CooldownTicks = (uint)Mathf.RoundToInt(configSO.CooldownSeconds * tickRate),
                MaxActiveTradesPerPlayer = configSO.MaxActiveTradesPerPlayer,
                AllowPremiumCurrencyTrade = configSO.AllowPremiumCurrencyTrade
            };

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, singleton);
            EntityManager.AddBuffer<TradeAuditLog>(entity);
            EntityManager.AddComponentData(entity, new TradeAuditState());
#if UNITY_EDITOR
            EntityManager.SetName(entity, "TradeConfig");
#endif

            // Initialize visual queue
            TradeVisualQueue.Initialize();

            Debug.Log($"[TradeBootstrap] Loaded config: MaxItems={singleton.MaxItemsPerOffer}, Proximity={singleton.ProximityRange}m, Timeout={singleton.TimeoutTicks}t");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            TradeVisualQueue.Dispose();
        }
    }
}
