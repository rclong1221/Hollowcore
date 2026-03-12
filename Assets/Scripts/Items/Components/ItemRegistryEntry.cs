namespace DIG.Items
{
    /// <summary>
    /// EPIC 16.6: Blittable subset of item data for Burst-compatible lookups.
    /// Stored in NativeHashMap by ItemRegistryBootstrapSystem.
    /// </summary>
    public struct ItemRegistryEntry
    {
        public int ItemTypeId;
        public ItemCategory Category;
        public ItemRarity Rarity;
        public bool IsStackable;
        public int MaxStack;
        public float Weight;
    }
}
