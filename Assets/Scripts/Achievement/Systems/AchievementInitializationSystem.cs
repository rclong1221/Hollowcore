using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Detects new players with AchievementLink but empty AchievementProgress buffer.
    /// Populates buffer with one entry per achievement from AchievementRegistrySingleton.
    /// Also detects newly added achievements (database expanded) and appends missing entries.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class AchievementInitializationSystem : SystemBase
    {
        private EntityQuery _childQuery;
        private EntityQuery _registryQuery;

        protected override void OnCreate()
        {
            _childQuery = GetEntityQuery(
                ComponentType.ReadOnly<AchievementChildTag>(),
                ComponentType.ReadOnly<AchievementOwner>(),
                ComponentType.ReadWrite<AchievementProgress>()
            );
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());

            RequireForUpdate(_registryQuery);
            RequireForUpdate(_childQuery);
        }

        protected override void OnUpdate()
        {
            var registry = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            var entities = _childQuery.ToEntityArray(Allocator.Temp);
            for (int e = 0; e < entities.Length; e++)
            {
                var buffer = EntityManager.GetBuffer<AchievementProgress>(entities[e]);

                if (buffer.Length == 0)
                {
                    // New player: populate all achievements
                    for (int i = 0; i < blob.TotalAchievements; i++)
                    {
                        buffer.Add(new AchievementProgress
                        {
                            AchievementId = blob.Definitions[i].AchievementId,
                            CurrentValue = 0,
                            IsUnlocked = false,
                            HighestTierUnlocked = 0,
                            UnlockTick = 0
                        });
                    }
                }
                else if (buffer.Length < blob.TotalAchievements)
                {
                    // Database expanded: append missing achievements
                    for (int i = 0; i < blob.TotalAchievements; i++)
                    {
                        ushort id = blob.Definitions[i].AchievementId;
                        bool found = false;
                        for (int j = 0; j < buffer.Length; j++)
                        {
                            if (buffer[j].AchievementId == id)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            buffer.Add(new AchievementProgress
                            {
                                AchievementId = id,
                                CurrentValue = 0,
                                IsUnlocked = false,
                                HighestTierUnlocked = 0,
                                UnlockTick = 0
                            });
                        }
                    }
                }
            }
            entities.Dispose();
        }
    }
}
