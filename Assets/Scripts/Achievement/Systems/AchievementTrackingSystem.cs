using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Reads all game event sources and increments AchievementProgress counters
    /// on achievement child entities. Runs after all event producers via [UpdateLast].
    /// Uses manual EntityQuery (NEVER SystemAPI.Query) for transient types.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class AchievementTrackingSystem : SystemBase
    {
        private EntityQuery _achievementChildQuery;
        private EntityQuery _registryQuery;
        private EntityQuery _killQuery;
        private EntityQuery _combatResultQuery;
        private EntityQuery _craftOutputQuery;
        private EntityQuery _dialogueQuery;

        private ComponentLookup<AchievementLink> _linkLookup;
        private ComponentLookup<global::Player.Components.DiedEvent> _diedEventLookup;
        private ComponentLookup<DIG.Progression.LevelUpEvent> _levelUpLookup;
        private ComponentLookup<DIG.Combat.Components.CharacterAttributes> _charAttrsLookup;

        private AchievementLookupMaps _lookupMaps;

        protected override void OnCreate()
        {
            _achievementChildQuery = GetEntityQuery(
                ComponentType.ReadOnly<AchievementChildTag>(),
                ComponentType.ReadOnly<AchievementOwner>(),
                ComponentType.ReadWrite<AchievementProgress>(),
                ComponentType.ReadWrite<AchievementCumulativeStats>(),
                ComponentType.ReadWrite<AchievementDirtyFlags>()
            );
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());
            _killQuery = GetEntityQuery(
                ComponentType.ReadOnly<global::Player.Components.KillCredited>(),
                ComponentType.ReadOnly<PlayerTag>()
            );
            _combatResultQuery = GetEntityQuery(ComponentType.ReadOnly<DIG.Combat.Systems.CombatResultEvent>());
            _craftOutputQuery = GetEntityQuery(ComponentType.ReadOnly<DIG.Crafting.CraftOutputElement>());
            _dialogueQuery = GetEntityQuery(ComponentType.ReadOnly<DIG.Dialogue.DialogueSessionState>());

            _linkLookup = GetComponentLookup<AchievementLink>(true);
            _diedEventLookup = GetComponentLookup<global::Player.Components.DiedEvent>(true);
            _levelUpLookup = GetComponentLookup<DIG.Progression.LevelUpEvent>(true);
            _charAttrsLookup = GetComponentLookup<DIG.Combat.Components.CharacterAttributes>(true);

            RequireForUpdate(_registryQuery);
            RequireForUpdate(_achievementChildQuery);
        }

        protected override void OnUpdate()
        {
            // Safety: complete pending jobs from combat/damage systems
            CompleteDependency();

            _linkLookup.Update(this);
            _diedEventLookup.Update(this);
            _levelUpLookup.Update(this);
            _charAttrsLookup.Update(this);

            var registry = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
            ref var blob = ref registry.Registry.Value;

            // Fetch lookup maps (created by bootstrap, stored as managed component on same entity)
            if (_lookupMaps == null || !_lookupMaps.IdToIndex.IsCreated)
            {
                var regEntities = _registryQuery.ToEntityArray(Allocator.Temp);
                if (regEntities.Length > 0 && EntityManager.HasComponent<AchievementLookupMaps>(regEntities[0]))
                    _lookupMaps = EntityManager.GetComponentObject<AchievementLookupMaps>(regEntities[0]);
                regEntities.Dispose();
            }

            // Count events this frame
            int killCount = _killQuery.CalculateEntityCount();
            int combatResultCount = _combatResultQuery.CalculateEntityCount();

            // Pre-process quest events ONCE outside the player loop (Fix #10)
            int questsThisFrame = 0;
            if (DIG.Quest.QuestEventQueue.Count > 0)
            {
                int peekCount = DIG.Quest.QuestEventQueue.Count;
                for (int i = 0; i < peekCount; i++)
                {
                    if (DIG.Quest.QuestEventQueue.TryDequeue(out var questEvt))
                    {
                        if (questEvt.Type == DIG.Quest.QuestUIEventType.QuestCompleted ||
                            questEvt.Type == DIG.Quest.QuestUIEventType.QuestTurnedIn)
                        {
                            questsThisFrame++;
                        }
                        // Re-enqueue so other consumers can still see it
                        DIG.Quest.QuestEventQueue.Enqueue(questEvt);
                    }
                }
            }

            // Hoist combat result array outside player loop (Fix #3)
            NativeArray<DIG.Combat.Systems.CombatResultEvent> combatResults = default;
            if (combatResultCount > 0)
                combatResults = _combatResultQuery.ToComponentDataArray<DIG.Combat.Systems.CombatResultEvent>(Allocator.Temp);

            // Single unified loop — no separate ProcessDeathsAndLevelUps (Fix #5)
            var childEntities = _achievementChildQuery.ToEntityArray(Allocator.Temp);
            var owners = _achievementChildQuery.ToComponentDataArray<AchievementOwner>(Allocator.Temp);

            for (int c = 0; c < childEntities.Length; c++)
            {
                var childEntity = childEntities[c];
                var ownerEntity = owners[c].Owner;

                if (!_linkLookup.HasComponent(ownerEntity)) continue;

                var progressBuffer = EntityManager.GetBuffer<AchievementProgress>(childEntity);
                var stats = EntityManager.GetComponentData<AchievementCumulativeStats>(childEntity);
                var dirty = EntityManager.GetComponentData<AchievementDirtyFlags>(childEntity);
                bool changed = false;

                // --- Enemy Kills ---
                if (killCount > 0 && EntityManager.HasComponent<global::Player.Components.KillCredited>(ownerEntity))
                {
                    stats.TotalKills++;
                    stats.CurrentKillStreak++;
                    if (stats.CurrentKillStreak > stats.HighestKillStreak)
                        stats.HighestKillStreak = stats.CurrentKillStreak;

                    IncrementMatchingProgress(ref progressBuffer, ref blob, AchievementConditionType.EnemyKill, 0, 1);
                    IncrementMatchingProgress(ref progressBuffer, ref blob, AchievementConditionType.KillStreak, 0, 0, stats.HighestKillStreak);
                    changed = true;
                }

                // --- Player Death ---
                if (_diedEventLookup.HasComponent(ownerEntity) &&
                    EntityManager.IsComponentEnabled<global::Player.Components.DiedEvent>(ownerEntity))
                {
                    stats.TotalDeaths++;
                    stats.CurrentKillStreak = 0;
                    IncrementMatchingProgress(ref progressBuffer, ref blob, AchievementConditionType.PlayerDeath, 0, 1);
                    changed = true;
                }

                // --- Level Up ---
                if (_levelUpLookup.HasComponent(ownerEntity) &&
                    EntityManager.IsComponentEnabled<DIG.Progression.LevelUpEvent>(ownerEntity) &&
                    _charAttrsLookup.HasComponent(ownerEntity))
                {
                    int level = _charAttrsLookup[ownerEntity].Level;
                    SetMaxProgress(ref progressBuffer, ref blob, AchievementConditionType.LevelReached, 0, level);
                    changed = true;
                }

                // --- Combat Damage Dealt ---
                if (combatResultCount > 0)
                {
                    bool damageChanged = false;
                    for (int i = 0; i < combatResults.Length; i++)
                    {
                        if (combatResults[i].AttackerEntity == ownerEntity && combatResults[i].DidHit)
                        {
                            stats.TotalDamageDealt += (long)combatResults[i].FinalDamage;
                            damageChanged = true;
                        }
                    }
                    if (damageChanged)
                    {
                        SetMaxProgress(ref progressBuffer, ref blob, AchievementConditionType.DamageDealt, 0, (int)(stats.TotalDamageDealt / 1000));
                        changed = true;
                    }
                }

                // --- Quest Completion (distributed to all players) ---
                if (questsThisFrame > 0)
                {
                    stats.TotalQuestsCompleted += questsThisFrame;
                    IncrementMatchingProgress(ref progressBuffer, ref blob, AchievementConditionType.QuestComplete, 0, questsThisFrame);
                    changed = true;
                }

                if (changed)
                {
                    dirty.Flags |= 0x3; // progress + stats dirty
                    EntityManager.SetComponentData(childEntity, stats);
                    EntityManager.SetComponentData(childEntity, dirty);
                }
            }

            childEntities.Dispose();
            owners.Dispose();
            if (combatResultCount > 0)
                combatResults.Dispose();
        }

        /// <summary>
        /// Increment CurrentValue by delta for all achievements matching conditionType.
        /// Uses O(1) lookup via ConditionToIndices multimap + O(1) IdToIndex for buffer match.
        /// </summary>
        private void IncrementMatchingProgress(
            ref DynamicBuffer<AchievementProgress> buffer,
            ref AchievementRegistryBlob blob,
            AchievementConditionType conditionType,
            int conditionParam,
            int delta)
        {
            if (_lookupMaps != null && _lookupMaps.ConditionToIndices.IsCreated)
            {
                // O(K) where K = number of achievements with this condition type
                if (_lookupMaps.ConditionToIndices.TryGetFirstValue((byte)conditionType, out int defIdx, out var it))
                {
                    do
                    {
                        ref var def = ref blob.Definitions[defIdx];
                        if (conditionParam != 0 && def.ConditionParam != conditionParam) continue;

                        // Find matching buffer entry by AchievementId
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            var entry = buffer[i];
                            if (entry.IsUnlocked) continue;
                            if (entry.AchievementId == def.AchievementId)
                            {
                                entry.CurrentValue += delta;
                                buffer[i] = entry;
                                break;
                            }
                        }
                    } while (_lookupMaps.ConditionToIndices.TryGetNextValue(out defIdx, ref it));
                }
            }
            else
            {
                // Fallback: O(N*M) linear scan
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.IsUnlocked) continue;
                    for (int d = 0; d < blob.TotalAchievements; d++)
                    {
                        ref var def = ref blob.Definitions[d];
                        if (def.AchievementId == entry.AchievementId &&
                            def.ConditionType == conditionType &&
                            (conditionParam == 0 || def.ConditionParam == conditionParam))
                        {
                            entry.CurrentValue += delta;
                            buffer[i] = entry;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set CurrentValue to absolute value if higher (for KillStreak).
        /// Uses O(1) lookup via ConditionToIndices multimap.
        /// </summary>
        private void IncrementMatchingProgress(
            ref DynamicBuffer<AchievementProgress> buffer,
            ref AchievementRegistryBlob blob,
            AchievementConditionType conditionType,
            int conditionParam,
            int delta,
            int absoluteValue)
        {
            if (_lookupMaps != null && _lookupMaps.ConditionToIndices.IsCreated)
            {
                if (_lookupMaps.ConditionToIndices.TryGetFirstValue((byte)conditionType, out int defIdx, out var it))
                {
                    do
                    {
                        ref var def = ref blob.Definitions[defIdx];
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            var entry = buffer[i];
                            if (entry.IsUnlocked) continue;
                            if (entry.AchievementId == def.AchievementId)
                            {
                                if (absoluteValue > entry.CurrentValue)
                                {
                                    entry.CurrentValue = absoluteValue;
                                    buffer[i] = entry;
                                }
                                break;
                            }
                        }
                    } while (_lookupMaps.ConditionToIndices.TryGetNextValue(out defIdx, ref it));
                }
            }
            else
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.IsUnlocked) continue;
                    for (int d = 0; d < blob.TotalAchievements; d++)
                    {
                        ref var def = ref blob.Definitions[d];
                        if (def.AchievementId == entry.AchievementId &&
                            def.ConditionType == conditionType)
                        {
                            if (absoluteValue > entry.CurrentValue)
                            {
                                entry.CurrentValue = absoluteValue;
                                buffer[i] = entry;
                            }
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Set progress to max of current value and new value (for LevelReached, DamageDealt).
        /// Uses O(1) lookup via ConditionToIndices multimap.
        /// </summary>
        private void SetMaxProgress(
            ref DynamicBuffer<AchievementProgress> buffer,
            ref AchievementRegistryBlob blob,
            AchievementConditionType conditionType,
            int conditionParam,
            int newValue)
        {
            if (_lookupMaps != null && _lookupMaps.ConditionToIndices.IsCreated)
            {
                if (_lookupMaps.ConditionToIndices.TryGetFirstValue((byte)conditionType, out int defIdx, out var it))
                {
                    do
                    {
                        ref var def = ref blob.Definitions[defIdx];
                        if (conditionParam != 0 && def.ConditionParam != conditionParam) continue;

                        for (int i = 0; i < buffer.Length; i++)
                        {
                            var entry = buffer[i];
                            if (entry.IsUnlocked) continue;
                            if (entry.AchievementId == def.AchievementId)
                            {
                                if (newValue > entry.CurrentValue)
                                {
                                    entry.CurrentValue = newValue;
                                    buffer[i] = entry;
                                }
                                break;
                            }
                        }
                    } while (_lookupMaps.ConditionToIndices.TryGetNextValue(out defIdx, ref it));
                }
            }
            else
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.IsUnlocked) continue;
                    for (int d = 0; d < blob.TotalAchievements; d++)
                    {
                        ref var def = ref blob.Definitions[d];
                        if (def.AchievementId == entry.AchievementId &&
                            def.ConditionType == conditionType &&
                            (conditionParam == 0 || def.ConditionParam == conditionParam))
                        {
                            if (newValue > entry.CurrentValue)
                            {
                                entry.CurrentValue = newValue;
                                buffer[i] = entry;
                            }
                            break;
                        }
                    }
                }
            }
        }
    }
}
