using UnityEngine;
using DIG.Shared;

namespace DIG.Items.Definitions
{
    /// <summary>
    /// EPIC 16.6: Per-item ScriptableObject containing all metadata.
    /// The authoritative source of truth for item properties.
    /// Referenced by ItemDatabaseSO for registry lookups.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Items/Item Entry", order = 0)]
    public class ItemEntrySO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique integer ID for this item type. Must be unique across the entire database.")]
        public int ItemTypeId;

        [Tooltip("Display name shown in UI.")]
        public string DisplayName;

        [TextArea(2, 4)]
        [Tooltip("Item description for tooltips.")]
        public string Description;

        [Tooltip("Icon sprite for inventory/HUD display.")]
        public Sprite Icon;

        [Header("Prefabs")]
        [Tooltip("Prefab spawned in the world as a pickup (must have ItemPickup component).")]
        public GameObject WorldPrefab;

        [Tooltip("Prefab used when equipped (first/third person visual).")]
        public GameObject EquipPrefab;

        [Header("Classification")]
        public ItemCategory Category;
        public ItemRarity Rarity;

        [Header("Stacking")]
        public bool IsStackable;

        [Min(1)]
        public int MaxStack = 1;

        [Header("Weight & Economy")]
        [Min(0f)]
        public float Weight;

        [Min(0f)]
        public float EquipDuration = 0.3f;

        [Tooltip("If this item represents a resource, set the type here. None = not a resource.")]
        public ResourceType ResourceType = ResourceType.None;

        [Min(0)]
        public int SellValue;

        [Min(0)]
        public int BuyValue;

        [Header("Equipment (Optional)")]
        [Tooltip("Weapon category definition for weapons/tools.")]
        public WeaponCategoryDefinition WeaponCategory;

        [Tooltip("Affix pool for rolled stats on this item type.")]
        public ScriptableObject PossibleAffixes; // AffixPoolSO, typed as SO to avoid circular dependency before Phase 4
    }
}
