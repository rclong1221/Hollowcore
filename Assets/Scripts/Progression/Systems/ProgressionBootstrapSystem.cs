using Unity.Entities;
using UnityEngine;

namespace DIG.Progression
{
    /// <summary>
    /// EPIC 16.14: Loads progression ScriptableObjects from Resources/ and builds BlobAssets.
    /// Creates ProgressionConfigSingleton entity. Follows SurfaceGameplayConfigSystem pattern.
    /// Runs once at startup, then self-disables.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class ProgressionBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var curveSO = Resources.Load<ProgressionCurveSO>("ProgressionCurve");
            var statScalingSO = Resources.Load<LevelStatScalingSO>("LevelStatScaling");
            var rewardsSO = Resources.Load<LevelRewardsSO>("LevelRewards");

            if (curveSO == null)
            {
                Debug.LogWarning("[ProgressionBootstrap] No ProgressionCurveSO found at Resources/ProgressionCurve. Using defaults.");
                curveSO = ScriptableObject.CreateInstance<ProgressionCurveSO>();
            }

            if (statScalingSO == null)
            {
                Debug.LogWarning("[ProgressionBootstrap] No LevelStatScalingSO found at Resources/LevelStatScaling. Using defaults.");
                statScalingSO = ScriptableObject.CreateInstance<LevelStatScalingSO>();
            }

            var singleton = new ProgressionConfigSingleton
            {
                Curve = BuildCurveBlob(curveSO),
                StatScaling = BuildStatScalingBlob(curveSO.MaxLevel, statScalingSO),
                Rewards = BuildRewardsBlob(rewardsSO)
            };

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, singleton);
#if UNITY_EDITOR
            EntityManager.SetName(entity, "ProgressionConfig");
#endif

            int count = curveSO.MaxLevel;
            Debug.Log($"[ProgressionBootstrap] Loaded progression config: MaxLevel={count}, StatPointsPerLevel={curveSO.StatPointsPerLevel}");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            // Dispose BlobAssets if singleton exists
            if (!_initialized) return;

            foreach (var config in SystemAPI.Query<RefRO<ProgressionConfigSingleton>>())
            {
                if (config.ValueRO.Curve.IsCreated) config.ValueRO.Curve.Dispose();
                if (config.ValueRO.StatScaling.IsCreated) config.ValueRO.StatScaling.Dispose();
                if (config.ValueRO.Rewards.IsCreated) config.ValueRO.Rewards.Dispose();
            }
        }

        private static BlobAssetReference<ProgressionBlob> BuildCurveBlob(ProgressionCurveSO so)
        {
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ProgressionBlob>();

            root.MaxLevel = so.MaxLevel;
            root.StatPointsPerLevel = so.StatPointsPerLevel;
            root.BaseKillXP = so.BaseKillXP;
            root.KillXPPerEnemyLevel = so.KillXPPerEnemyLevel;
            root.DiminishStartDelta = so.DiminishStartDelta;
            root.DiminishFactorPerLevel = so.DiminishFactorPerLevel;
            root.DiminishFloor = so.DiminishFloor;
            root.QuestXPBase = so.QuestXPBase;
            root.CraftXPBase = so.CraftXPBase;
            root.ExplorationXPBase = so.ExplorationXPBase;
            root.InteractionXPBase = so.InteractionXPBase;
            root.RestedXPMultiplier = so.RestedXPMultiplier;
            root.RestedXPAccumRatePerHour = so.RestedXPAccumRatePerHour;
            root.RestedXPMaxDays = so.RestedXPMaxDays;

            var xpArray = builder.Allocate(ref root.XPPerLevel, so.MaxLevel);
            for (int i = 0; i < so.MaxLevel; i++)
                xpArray[i] = so.GetXPForLevel(i + 1);

            var result = builder.CreateBlobAssetReference<ProgressionBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        private static BlobAssetReference<LevelStatScalingBlob> BuildStatScalingBlob(int maxLevel, LevelStatScalingSO so)
        {
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LevelStatScalingBlob>();

            var statsArray = builder.Allocate(ref root.StatsPerLevel, maxLevel);
            for (int i = 0; i < maxLevel; i++)
            {
                var data = so.GetStatsForLevel(i + 1);
                statsArray[i] = new LevelStatEntry
                {
                    MaxHealth = data.MaxHealth,
                    AttackPower = data.AttackPower,
                    SpellPower = data.SpellPower,
                    Defense = data.Defense,
                    Armor = data.Armor,
                    MaxMana = data.MaxMana,
                    ManaRegen = data.ManaRegen,
                    MaxStamina = data.MaxStamina,
                    StaminaRegen = data.StaminaRegen
                };
            }

            var result = builder.CreateBlobAssetReference<LevelStatScalingBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();
            return result;
        }

        private static BlobAssetReference<LevelRewardsBlob> BuildRewardsBlob(LevelRewardsSO so)
        {
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<LevelRewardsBlob>();

            int count = so != null && so.Rewards != null ? so.Rewards.Length : 0;
            var rewardsArray = builder.Allocate(ref root.Rewards, count);
            for (int i = 0; i < count; i++)
            {
                var entry = so.Rewards[i];
                rewardsArray[i] = new LevelRewardEntry
                {
                    Level = entry.Level,
                    RewardType = entry.RewardType,
                    IntValue = entry.IntValue,
                    FloatValue = entry.FloatValue
                };
            }

            var result = builder.CreateBlobAssetReference<LevelRewardsBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
