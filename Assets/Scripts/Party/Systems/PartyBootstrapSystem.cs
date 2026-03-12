using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Party
{
    /// <summary>
    /// EPIC 17.2: Loads PartyConfigSO from Resources/, creates PartyConfigSingleton.
    /// Runs once at startup, then self-disables. Follows ProgressionBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PartyBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var configSO = Resources.Load<PartyConfigSO>("PartyConfig");
            if (configSO == null)
            {
                Debug.LogWarning("[PartyBootstrap] No PartyConfigSO found at Resources/PartyConfig. Using defaults.");
                configSO = ScriptableObject.CreateInstance<PartyConfigSO>();
            }

            // Read tick rate from NetCode singleton, fall back to 30Hz
            int TickRate = 30;
            if (SystemAPI.HasSingleton<ClientServerTickRate>())
                TickRate = SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;

            var singleton = new PartyConfigSingleton
            {
                MaxPartySize = (byte)Mathf.Clamp(configSO.MaxPartySize, 2, 6),
                InviteTimeoutTicks = Mathf.RoundToInt(configSO.InviteTimeoutSeconds * TickRate),
                XPShareRange = configSO.XPShareRange,
                XPShareBonusPerMember = configSO.XPShareBonusPerMember,
                LootRange = configSO.LootRange,
                KillCreditRange = configSO.KillCreditRange,
                LootDesignationTimeoutTicks = Mathf.RoundToInt(configSO.LootDesignationTimeoutSeconds * TickRate),
                NeedGreedVoteTimeoutTicks = Mathf.RoundToInt(configSO.NeedGreedVoteTimeoutSeconds * TickRate),
                LootGoldSplitPercent = configSO.LootGoldSplitPercent,
                AllowLootModeVote = configSO.AllowLootModeVote,
                DefaultLootMode = configSO.DefaultLootMode
            };

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, singleton);
#if UNITY_EDITOR
            EntityManager.SetName(entity, "PartyConfig");
#endif

            // Initialize visual queue
            PartyVisualQueue.Initialize();

            Debug.Log($"[PartyBootstrap] Loaded config: MaxSize={singleton.MaxPartySize}, XPRange={singleton.XPShareRange}, LootRange={singleton.LootRange}");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            PartyVisualQueue.Dispose();
        }
    }
}
