using Unity.Collections;
using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Burst-readable unlock definition data.
    /// Built once from MetaUnlockTreeSO at bootstrap.
    /// Used by MetaUnlockPurchaseSystem for prerequisite/cost validation.
    /// </summary>
    public struct MetaUnlockBlob
    {
        public BlobArray<MetaUnlockBlobEntry> Entries;
    }

    /// <summary>
    /// EPIC 23.2: Single unlock definition in the blob. Contains everything needed
    /// for Burst-compiled validation (no managed strings).
    /// </summary>
    public struct MetaUnlockBlobEntry
    {
        public int UnlockId;
        public MetaUnlockCategory Category;
        public int Cost;
        public int PrerequisiteId;
        public float FloatValue;
        public int IntValue;
    }

    /// <summary>
    /// EPIC 23.2: Singleton holding the baked unlock tree blob.
    /// Created by MetaBootstrapSystem.
    /// </summary>
    public struct MetaUnlockTreeSingleton : IComponentData
    {
        public BlobAssetReference<MetaUnlockBlob> Tree;
    }

    /// <summary>
    /// EPIC 23.2: Builds MetaUnlockBlob from MetaUnlockTreeSO.
    /// Follows RunConfigBlobBuilder pattern.
    /// </summary>
    public static class MetaUnlockBlobBuilder
    {
        public static BlobAssetReference<MetaUnlockBlob> Build(MetaUnlockTreeSO so)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<MetaUnlockBlob>();

            int count = so.Unlocks != null ? so.Unlocks.Count : 0;
            var entries = builder.Allocate(ref root.Entries, count > 0 ? count : 1);

            if (count == 0)
            {
                // Write a dummy entry so the blob is valid
                entries[0] = default;
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var def = so.Unlocks[i];
                    entries[i] = new MetaUnlockBlobEntry
                    {
                        UnlockId = def.UnlockId,
                        Category = def.Category,
                        Cost = def.Cost,
                        PrerequisiteId = def.PrerequisiteId,
                        FloatValue = def.FloatValue,
                        IntValue = def.IntValue
                    };
                }
            }

            var result = builder.CreateBlobAssetReference<MetaUnlockBlob>(Allocator.Persistent);
            builder.Dispose();
            return result;
        }
    }
}
