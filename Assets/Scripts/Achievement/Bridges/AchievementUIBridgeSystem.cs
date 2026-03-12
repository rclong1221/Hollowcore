using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Managed bridge system that dequeues AchievementVisualQueue entries
    /// and pushes toast/panel data to AchievementUIRegistry providers.
    /// Follows CombatUIBridgeSystem / ProgressionUIBridgeSystem pattern.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class AchievementUIBridgeSystem : SystemBase
    {
        private EntityQuery _registryQuery;
        private EntityQuery _childQuery;
        private int _noProviderWarnFrame;
        private AchievementConfigSO _cachedConfig;
        private bool _configLoaded;
        private AchievementLookupMaps _lookupMaps;

        protected override void OnCreate()
        {
            _registryQuery = GetEntityQuery(ComponentType.ReadOnly<AchievementRegistrySingleton>());
            _childQuery = GetEntityQuery(
                ComponentType.ReadOnly<AchievementChildTag>(),
                ComponentType.ReadOnly<AchievementOwner>(),
                ComponentType.ReadOnly<AchievementProgress>(),
                ComponentType.ReadOnly<AchievementDirtyFlags>()
            );
        }

        private AchievementConfigSO GetConfig()
        {
            if (!_configLoaded)
            {
                _cachedConfig = Resources.Load<AchievementConfigSO>("AchievementConfig");
                _configLoaded = true;
            }
            return _cachedConfig;
        }

        protected override void OnUpdate()
        {
            // Warn once if no provider registered
            if (!AchievementUIRegistry.HasProvider)
            {
                _noProviderWarnFrame++;
                if (_noProviderWarnFrame == 120)
                    Debug.LogWarning("[Achievement] No IAchievementUIProvider registered after 120 frames. Toast notifications will not display.");
                return;
            }

            var config = GetConfig();

            // Drain visual queue for toast notifications
            while (AchievementVisualQueue.TryDequeue(out var evt))
            {
                float duration = config != null ? config.ToastDisplayDuration : 5f;
                bool enableToasts = config == null || config.EnableToastNotifications;

                if (!enableToasts) continue;

                // Try to load icon sprite from Resources (rare — only on unlock)
                Sprite icon = null;
                if (_registryQuery.CalculateEntityCount() > 0)
                {
                    var registry = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
                    ref var blob = ref registry.Registry.Value;
                    FetchLookupMaps();
                    int iconIdx = FindDefIndex(ref blob, evt.AchievementId);
                    if (iconIdx >= 0)
                    {
                        string iconPath = blob.Definitions[iconIdx].IconPath.ToString();
                        if (!string.IsNullOrEmpty(iconPath))
                            icon = Resources.Load<Sprite>(iconPath);
                    }
                }

                AchievementUIRegistry.ShowToast(new AchievementToastData
                {
                    AchievementName = evt.AchievementName.ToString(),
                    Description = evt.Description.ToString(),
                    RewardText = $"{evt.RewardType}: {evt.RewardAmount}",
                    Icon = icon,
                    Tier = evt.Tier,
                    DisplayDuration = duration
                });
            }

            // Push full panel data only when dirty
            if (_registryQuery.CalculateEntityCount() == 0) return;
            if (_childQuery.CalculateEntityCount() == 0) return;

            // Check if any child has dirty flags — skip full rebuild if clean
            bool anyDirty = false;
            var dirtyEntities = _childQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < dirtyEntities.Length; i++)
            {
                var flags = EntityManager.GetComponentData<AchievementDirtyFlags>(dirtyEntities[i]);
                if (flags.Flags != 0) { anyDirty = true; break; }
            }
            dirtyEntities.Dispose();

            if (!anyDirty) return;

            var reg = _registryQuery.GetSingleton<AchievementRegistrySingleton>();
            ref var regBlob = ref reg.Registry.Value;
            FetchLookupMaps();

            // Find local player's achievement child
            var childEntities = _childQuery.ToEntityArray(Allocator.Temp);
            if (childEntities.Length == 0) { childEntities.Dispose(); return; }

            // Use first child entity (local player in most cases)
            var childEntity = childEntities[0];
            childEntities.Dispose();

            var progressBuffer = EntityManager.GetBuffer<AchievementProgress>(childEntity, true);

            int totalUnlocked = 0;
            var entries = new AchievementEntryUI[progressBuffer.Length];

            bool enableHidden = config == null || config.EnableHiddenAchievements;

            for (int i = 0; i < progressBuffer.Length; i++)
            {
                var prog = progressBuffer[i];

                // Find definition — O(1) via lookup map
                int defIdx = FindDefIndex(ref regBlob, prog.AchievementId);

                if (defIdx < 0)
                {
                    entries[i] = new AchievementEntryUI { AchievementId = prog.AchievementId, Name = "Unknown" };
                    continue;
                }

                ref var def = ref regBlob.Definitions[defIdx];
                bool isHidden = enableHidden && def.IsHidden && !prog.IsUnlocked && prog.HighestTierUnlocked == 0;

                // Find next threshold
                int nextThreshold = 0;
                for (int t = 0; t < def.Tiers.Length; t++)
                {
                    if (prog.HighestTierUnlocked < (byte)def.Tiers[t].Tier)
                    {
                        nextThreshold = def.Tiers[t].Threshold;
                        break;
                    }
                }

                float progressPct = nextThreshold > 0 ? Mathf.Clamp01((float)prog.CurrentValue / nextThreshold) : 1f;

                // Build tier reward UI
                var tierUIs = new AchievementTierRewardUI[def.Tiers.Length];
                for (int t = 0; t < def.Tiers.Length; t++)
                {
                    tierUIs[t] = new AchievementTierRewardUI
                    {
                        Tier = def.Tiers[t].Tier,
                        Threshold = def.Tiers[t].Threshold,
                        RewardText = def.Tiers[t].RewardDescription.ToString(),
                        IsUnlocked = prog.HighestTierUnlocked >= (byte)def.Tiers[t].Tier
                    };
                }

                if (prog.IsUnlocked || prog.HighestTierUnlocked > 0)
                    totalUnlocked++;

                entries[i] = new AchievementEntryUI
                {
                    AchievementId = prog.AchievementId,
                    Name = isHidden ? "???" : def.Name.ToString(),
                    Description = isHidden ? "Hidden achievement" : def.Description.ToString(),
                    Icon = null, // Icon loaded by UI view from Resources
                    Category = def.Category,
                    HighestTier = (AchievementTier)prog.HighestTierUnlocked,
                    CurrentValue = prog.CurrentValue,
                    NextThreshold = nextThreshold,
                    ProgressPercent = progressPct,
                    IsHidden = isHidden,
                    IsComplete = prog.IsUnlocked,
                    Tiers = tierUIs
                };
            }

            float completionPct = entries.Length > 0 ? (float)totalUnlocked / entries.Length * 100f : 0f;

            AchievementUIRegistry.UpdatePanel(new AchievementPanelData
            {
                Entries = entries,
                TotalUnlocked = totalUnlocked,
                TotalAchievements = entries.Length,
                CompletionPercent = completionPct
            });
        }

        private void FetchLookupMaps()
        {
            if (_lookupMaps != null && _lookupMaps.IdToIndex.IsCreated) return;
            var regEntities = _registryQuery.ToEntityArray(Allocator.Temp);
            if (regEntities.Length > 0 && EntityManager.HasComponent<AchievementLookupMaps>(regEntities[0]))
                _lookupMaps = EntityManager.GetComponentObject<AchievementLookupMaps>(regEntities[0]);
            regEntities.Dispose();
        }

        private int FindDefIndex(ref AchievementRegistryBlob blob, ushort achievementId)
        {
            if (_lookupMaps != null && _lookupMaps.IdToIndex.IsCreated)
            {
                if (_lookupMaps.IdToIndex.TryGetValue(achievementId, out int idx))
                    return idx;
                return -1;
            }
            for (int i = 0; i < blob.TotalAchievements; i++)
            {
                if (blob.Definitions[i].AchievementId == achievementId)
                    return i;
            }
            return -1;
        }
    }
}
