using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Loads AchievementDatabaseSO + AchievementConfigSO from Resources/,
    /// builds BlobAssets, creates AchievementRegistrySingleton entity.
    /// Runs once at startup then self-disables. Follows ProgressionBootstrapSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class AchievementBootstrapSystem : SystemBase
    {
        private bool _initialized;

        protected override void OnUpdate()
        {
            if (_initialized) return;

            var databaseSO = Resources.Load<AchievementDatabaseSO>("AchievementDatabase");
            var configSO = Resources.Load<AchievementConfigSO>("AchievementConfig");

            if (databaseSO == null)
            {
                Debug.LogWarning("[Achievement] No AchievementDatabaseSO found at Resources/AchievementDatabase. Achievement system disabled.");
                _initialized = true;
                Enabled = false;
                return;
            }

            if (configSO == null)
            {
                Debug.LogWarning("[Achievement] No AchievementConfigSO found at Resources/AchievementConfig. Using defaults.");
                configSO = ScriptableObject.CreateInstance<AchievementConfigSO>();
            }

            // Build BlobAsset from database
            var blobRef = BuildRegistryBlob(databaseSO);

            // Create singleton entity
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, new AchievementRegistrySingleton
            {
                Registry = blobRef
            });

            // Build O(1) lookup maps from blob
            var lookupMaps = BuildLookupMaps(ref blobRef.Value);
            EntityManager.AddComponentObject(entity, lookupMaps);

#if UNITY_EDITOR
            EntityManager.SetName(entity, "AchievementRegistry");
#endif

            // Initialize visual queue
            AchievementVisualQueue.Initialize();

            int totalTiers = 0;
            for (int i = 0; i < databaseSO.Achievements.Count; i++)
            {
                var def = databaseSO.Achievements[i];
                if (def != null) totalTiers += def.Tiers != null ? def.Tiers.Length : 0;
            }

            Debug.Log($"[Achievement] Registered {databaseSO.Achievements.Count} achievements with {totalTiers} total tiers");

            _initialized = true;
            Enabled = false;
        }

        protected override void OnDestroy()
        {
            if (!_initialized) return;

            var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());
            if (query.CalculateEntityCount() > 0)
            {
                var singleton = query.GetSingleton<AchievementRegistrySingleton>();
                if (singleton.Registry.IsCreated)
                    singleton.Registry.Dispose();

                // Dispose lookup maps
                var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (EntityManager.HasComponent<AchievementLookupMaps>(entities[i]))
                    {
                        var maps = EntityManager.GetComponentObject<AchievementLookupMaps>(entities[i]);
                        maps?.Dispose();
                    }
                }
                entities.Dispose();
            }

            AchievementVisualQueue.Dispose();
        }

        private static AchievementLookupMaps BuildLookupMaps(ref AchievementRegistryBlob blob)
        {
            int count = blob.TotalAchievements;
            var maps = new AchievementLookupMaps
            {
                IdToIndex = new NativeHashMap<ushort, int>(count, Allocator.Persistent),
                ConditionToIndices = new NativeParallelMultiHashMap<byte, int>(count, Allocator.Persistent)
            };

            for (int i = 0; i < count; i++)
            {
                ref var def = ref blob.Definitions[i];
                maps.IdToIndex.TryAdd(def.AchievementId, i);
                maps.ConditionToIndices.Add((byte)def.ConditionType, i);
            }

            return maps;
        }

        private static BlobAssetReference<AchievementRegistryBlob> BuildRegistryBlob(AchievementDatabaseSO database)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<AchievementRegistryBlob>();

            int count = database.Achievements.Count;
            root.TotalAchievements = count;

            var defs = builder.Allocate(ref root.Definitions, count);
            for (int i = 0; i < count; i++)
            {
                var so = database.Achievements[i];
                if (so == null)
                {
                    Debug.LogWarning($"[Achievement] Null entry at index {i} in AchievementDatabaseSO");
                    continue;
                }

                defs[i].AchievementId = so.AchievementId;
                defs[i].Category = so.Category;
                defs[i].ConditionType = so.ConditionType;
                defs[i].ConditionParam = so.ConditionParam;
                defs[i].IsHidden = so.IsHidden;

                builder.AllocateString(ref defs[i].Name, so.AchievementName ?? "");
                builder.AllocateString(ref defs[i].Description, so.Description ?? "");

                string iconPath = so.Icon != null ? so.Icon.name : "";
                builder.AllocateString(ref defs[i].IconPath, iconPath);

                int tierCount = so.Tiers != null ? so.Tiers.Length : 0;
                var tiers = builder.Allocate(ref defs[i].Tiers, tierCount);
                for (int t = 0; t < tierCount; t++)
                {
                    var tierDef = so.Tiers[t];
                    tiers[t].Tier = tierDef.Tier;
                    tiers[t].Threshold = tierDef.Threshold;
                    tiers[t].RewardType = tierDef.RewardType;
                    tiers[t].RewardIntValue = tierDef.RewardIntValue;
                    tiers[t].RewardFloatValue = tierDef.RewardFloatValue;
                    builder.AllocateString(ref tiers[t].RewardDescription, tierDef.RewardDescription ?? "");
                }
            }

            var result = builder.CreateBlobAssetReference<AchievementRegistryBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
