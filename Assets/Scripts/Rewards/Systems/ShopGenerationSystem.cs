using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Roguelite.Rewards
{
    /// <summary>
    /// EPIC 23.5: On Shop zone ZoneTransition, rolls shop inventory from reward pool.
    /// Applies price scaling from zone index. RuntimeDifficultyScale.CurrencyMultiplier when present.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ShopGenerationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<RunState>();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.HasSingleton<RewardRegistryManaged>())
                return;

            var registry = SystemAPI.ManagedAPI.GetSingleton<RewardRegistryManaged>();
            var pool = registry.DefaultRewardPool;
            if (pool == null || pool.Entries == null || pool.Entries.Count == 0)
                return;

            foreach (var (runState, shopBuffer, entity) in
                     SystemAPI.Query<RefRO<RunState>, DynamicBuffer<ShopInventoryEntry>>()
                     .WithEntityAccess())
            {
                var run = runState.ValueRO;
                if (run.Phase != RunPhase.ZoneTransition)
                    continue;

                // Check if this is a shop zone (ZoneContext or default: every other zone)
                bool isShopZone = false;
                if (SystemAPI.HasSingleton<ZoneContextSingleton>())
                {
                    isShopZone = SystemAPI.GetSingleton<ZoneContextSingleton>().CurrentType == ZoneTransitionType.Shop;
                }
                else
                {
                    // Default: zones 1, 4, 7... are shop (zoneIndex % 3 == 1)
                    isShopZone = (run.CurrentZoneIndex % 3) == 1;
                }

                if (!isShopZone)
                    continue;

                if (shopBuffer.Length > 0)
                    continue;

                byte zoneIndex = run.CurrentZoneIndex;
                byte ascension = run.AscensionLevel;
                uint zoneSeed = run.ZoneSeed;

                float currencyMultiplier = 1f;
                if (SystemAPI.HasSingleton<RuntimeDifficultyScale>())
                {
                    var scale = SystemAPI.GetSingleton<RuntimeDifficultyScale>();
                    if (scale.CurrencyMultiplier > 0f)
                        currencyMultiplier = 1f / scale.CurrencyMultiplier;
                }

                var validEntries = new System.Collections.Generic.List<RewardPoolEntry>();
                foreach (var e in pool.Entries)
                {
                    if (e.Reward == null) continue;
                    if (e.Reward.MinZoneIndex > 0 && zoneIndex < e.Reward.MinZoneIndex) continue;
                    if (e.Reward.MaxZoneIndex > 0 && zoneIndex > e.Reward.MaxZoneIndex) continue;
                    if (e.Reward.RequiredAscensionLevel > 0 && ascension < e.Reward.RequiredAscensionLevel) continue;
                    validEntries.Add(e);
                }

                int shopSize = math.min(6, validEntries.Count);
                var rng = new Unity.Mathematics.Random(RunSeedUtility.DeriveShopSeed(zoneSeed));

                float zoneMultiplier = 1f + (zoneIndex * 0.15f);

                for (int i = 0; i < shopSize; i++)
                {
                    int pick = rng.NextInt(validEntries.Count);
                    var chosen = validEntries[pick];
                    if (!pool.AllowDuplicates)
                        validEntries.RemoveAt(pick);

                    int basePrice = chosen.Reward.IntValue;
                    if (basePrice <= 0) basePrice = 10;

                    int finalPrice = (int)math.ceil(basePrice * zoneMultiplier * currencyMultiplier);
                    finalPrice = math.max(1, finalPrice);

                    shopBuffer.Add(new ShopInventoryEntry
                    {
                        RewardId = chosen.Reward.RewardId,
                        Type = chosen.Reward.Type,
                        Rarity = chosen.Reward.Rarity,
                        IntValue = chosen.Reward.IntValue,
                        FloatValue = chosen.Reward.FloatValue,
                        Price = finalPrice,
                        IsSoldOut = false
                    });
                }
            }
        }
    }
}
