using Unity.Entities;

namespace DIG.Roguelite
{
    /// <summary>
    /// EPIC 23.2: Categories for meta-progression unlocks.
    /// Determines how the unlock's value is interpreted and applied.
    /// </summary>
    public enum MetaUnlockCategory : byte
    {
        /// <summary>Permanent stat boost (FloatValue = amount, IntValue = stat ID).</summary>
        StatBoost = 0,

        /// <summary>Starter item granted at run start (IntValue = item type ID).</summary>
        StarterItem = 1,

        /// <summary>New ability unlocked for runs (IntValue = ability ID).</summary>
        NewAbility = 2,

        /// <summary>Cosmetic unlock (IntValue = cosmetic ID).</summary>
        Cosmetic = 3,

        /// <summary>Run modifier unlocked for selection (IntValue = modifier ID).</summary>
        RunModifier = 4,

        /// <summary>Access to new zone types (IntValue = zone type ID).</summary>
        ZoneAccess = 5,

        /// <summary>Shop upgrade — better prices or new stock (FloatValue = discount %).</summary>
        ShopUpgrade = 6,

        /// <summary>Meta-currency earn rate bonus (FloatValue = multiplier).</summary>
        CurrencyBonus = 7
    }

    /// <summary>
    /// EPIC 23.2: Runtime state for a single meta-unlock.
    /// Buffer on the MetaBank entity. Populated from MetaUnlockTreeSO at bootstrap.
    /// IsUnlocked persisted via MetaProgressionSaveModule.
    /// </summary>
    [InternalBufferCapacity(0)] // Heap-allocated — unlock trees can be large
    public struct MetaUnlockEntry : IBufferElementData
    {
        /// <summary>Unique identifier. Matches MetaUnlockDefinition.UnlockId from the SO.</summary>
        public int UnlockId;

        /// <summary>What kind of unlock this is.</summary>
        public MetaUnlockCategory Category;

        /// <summary>Meta-currency cost to purchase.</summary>
        public int Cost;

        /// <summary>Required prerequisite UnlockId. -1 = no prerequisite.</summary>
        public int PrerequisiteId;

        /// <summary>Whether this unlock has been purchased.</summary>
        public bool IsUnlocked;

        /// <summary>Floating-point value (stat amount, multiplier, discount, etc.).</summary>
        public float FloatValue;

        /// <summary>Integer value (item ID, ability ID, stat ID, etc.).</summary>
        public int IntValue;
    }

    /// <summary>
    /// EPIC 23.2: Request to purchase a meta-unlock.
    /// IEnableableComponent on the MetaBank entity, baked disabled.
    /// UI enables it with the desired UnlockId. MetaUnlockPurchaseSystem consumes it.
    /// </summary>
    public struct MetaUnlockRequest : IComponentData, IEnableableComponent
    {
        /// <summary>UnlockId to purchase.</summary>
        public int UnlockId;
    }
}
