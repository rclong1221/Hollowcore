using Unity.Collections;
using Unity.Entities;

namespace DIG.Achievement
{
    /// <summary>
    /// EPIC 17.7: Singleton holding BlobAsset reference to all achievement definitions.
    /// Created by AchievementBootstrapSystem from AchievementDatabaseSO.
    /// </summary>
    public struct AchievementRegistrySingleton : IComponentData
    {
        public BlobAssetReference<AchievementRegistryBlob> Registry;
    }

    /// <summary>
    /// EPIC 17.7: O(1) lookup maps built at bootstrap from the blob.
    /// AchievementId -> blob index, ConditionType -> first matching blob index.
    /// Stored as a managed class singleton via AddComponentObject.
    /// </summary>
    public class AchievementLookupMaps : IComponentData
    {
        /// <summary>AchievementId -> index in blob Definitions array.</summary>
        public NativeHashMap<ushort, int> IdToIndex;

        /// <summary>ConditionType -> list of blob definition indices that match.</summary>
        public NativeParallelMultiHashMap<byte, int> ConditionToIndices;

        public void Dispose()
        {
            if (IdToIndex.IsCreated) IdToIndex.Dispose();
            if (ConditionToIndices.IsCreated) ConditionToIndices.Dispose();
        }
    }

    /// <summary>
    /// EPIC 17.7: Root blob structure containing all achievement definitions.
    /// </summary>
    public struct AchievementRegistryBlob
    {
        public int TotalAchievements;
        public BlobArray<AchievementDefinitionBlob> Definitions;
    }

    /// <summary>
    /// EPIC 17.7: Single achievement definition in the blob.
    /// </summary>
    public struct AchievementDefinitionBlob
    {
        public ushort AchievementId;
        public AchievementCategory Category;
        public AchievementConditionType ConditionType;
        public int ConditionParam;
        public bool IsHidden;
        public BlobString Name;
        public BlobString Description;
        public BlobString IconPath;
        public BlobArray<AchievementTierBlob> Tiers;
    }

    /// <summary>
    /// EPIC 17.7: Tier data within an achievement definition blob.
    /// </summary>
    public struct AchievementTierBlob
    {
        public AchievementTier Tier;
        public int Threshold;
        public AchievementRewardType RewardType;
        public int RewardIntValue;
        public float RewardFloatValue;
        public BlobString RewardDescription;
    }
}
