using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Checks AchievementProgress against tier thresholds, creates
    /// AchievementUnlockEvent transient entities, enqueues to AchievementVisualQueue.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(AchievementTrackingSystem))]
    public partial class AchievementUnlockSystem : SystemBase
    {
        private EntityQuery _childQuery;
        private EntityQuery _registryQuery;
        private EntityQuery _networkTimeQuery;
        private AchievementLookupMaps _lookupMaps;

        protected override void OnCreate()
        {
            _childQuery = GetEntityQuery(
                ComponentType.ReadOnly<AchievementChildTag>(),
                ComponentType.ReadOnly<AchievementOwner>(),
                ComponentType.ReadWrite<AchievementProgress>(),
                ComponentType.ReadWrite<AchievementDirtyFlags>()
            );
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());
            _networkTimeQuery = GetEntityQuery(ComponentType.ReadOnly<NetworkTime>());

            RequireForUpdate(_registryQuery);
            RequireForUpdate(_childQuery);
        }

        protected override void OnUpdate()
        {
            var registry = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            // Fetch lookup maps
            if (_lookupMaps == null || !_lookupMaps.IdToIndex.IsCreated)
            {
                var regEntities = _registryQuery.ToEntityArray(Allocator.Temp);
                if (regEntities.Length > 0 && EntityManager.HasComponent<AchievementLookupMaps>(regEntities[0]))
                    _lookupMaps = EntityManager.GetComponentObject<AchievementLookupMaps>(regEntities[0]);
                regEntities.Dispose();
            }

            uint serverTick = 0;
            if (_networkTimeQuery.CalculateEntityCount() > 0)
            {
                var networkTime = _networkTimeQuery.GetSingleton<NetworkTime>();
                serverTick = networkTime.ServerTick.TickIndexForValidTick;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var childEntities = _childQuery.ToEntityArray(Allocator.Temp);
            var owners = _childQuery.ToComponentDataArray<AchievementOwner>(Allocator.Temp);

            for (int c = 0; c < childEntities.Length; c++)
            {
                var childEntity = childEntities[c];
                var ownerEntity = owners[c].Owner;
                var progressBuffer = EntityManager.GetBuffer<AchievementProgress>(childEntity);
                var dirty = EntityManager.GetComponentData<AchievementDirtyFlags>(childEntity);

                // Only check if progress was marked dirty
                if ((dirty.Flags & 0x1) == 0) continue;

                bool anyUnlock = false;

                for (int i = 0; i < progressBuffer.Length; i++)
                {
                    var entry = progressBuffer[i];
                    if (entry.IsUnlocked) continue;

                    // Find definition — O(1) via lookup map
                    int defIndex = FindDefinitionIndex(ref blob, entry.AchievementId);
                    if (defIndex < 0) continue;

                    ref var def = ref blob.Definitions[defIndex];

                    // Check tiers in ascending order
                    for (int t = 0; t < def.Tiers.Length; t++)
                    {
                        ref var tier = ref def.Tiers[t];
                        byte tierByte = (byte)tier.Tier;

                        if (entry.HighestTierUnlocked >= tierByte) continue;
                        if (entry.CurrentValue < tier.Threshold) break;

                        // Tier unlocked!
                        entry.HighestTierUnlocked = tierByte;
                        entry.UnlockTick = serverTick;

                        // Check if all tiers complete
                        if (t == def.Tiers.Length - 1)
                            entry.IsUnlocked = true;

                        // Create transient unlock event entity
                        var unlockEntity = ecb.CreateEntity();
                        ecb.AddComponent(unlockEntity, new AchievementUnlockEvent
                        {
                            AchievementId = entry.AchievementId,
                            PlayerId = ownerEntity,
                            Tier = tierByte,
                            ServerTick = serverTick
                        });

                        // Enqueue for UI toast — use FixedString directly to avoid managed allocation
                        AchievementVisualQueue.Enqueue(new AchievementUnlockVisualEvent
                        {
                            AchievementId = entry.AchievementId,
                            Tier = tier.Tier,
                            AchievementName = def.Name.ToString(),
                            Description = def.Description.ToString(),
                            RewardType = tier.RewardType,
                            RewardAmount = tier.RewardIntValue
                        });

                        anyUnlock = true;
                    }

                    progressBuffer[i] = entry;
                }

                if (anyUnlock)
                {
                    dirty.Flags |= 0x3;
                    EntityManager.SetComponentData(childEntity, dirty);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            childEntities.Dispose();
            owners.Dispose();
        }

        private int FindDefinitionIndex(ref AchievementRegistryBlob blob, ushort achievementId)
        {
            // O(1) via lookup map
            if (_lookupMaps != null && _lookupMaps.IdToIndex.IsCreated)
            {
                if (_lookupMaps.IdToIndex.TryGetValue(achievementId, out int idx))
                    return idx;
                return -1;
            }
            // Fallback: linear scan
            for (int i = 0; i < blob.TotalAchievements; i++)
            {
                if (blob.Definitions[i].AchievementId == achievementId)
                    return i;
            }
            return -1;
        }
    }
}
