using Unity.Entities;

namespace DIG.Items
{
    /// <summary>
    /// Baked from WeaponAttachmentConfig - specifies which weapon category this item belongs to.
    /// The actual positioning (offsets, IK) is defined on the character's WeaponParentConfig.
    /// </summary>
    /// <remarks>
    /// The WieldTargetID maps to ObjectIdentifier IDs on character prefabs:
    /// - 50001001 = AssaultRifleParent
    /// - 50002001 = PistolParent (Right Hand)
    /// - 50002002 = PistolParent (Left Hand - dual wield)
    /// - 50003001 = ShotgunParent
    /// - 50005001 = SniperParent
    /// - 50022001 = SwordParent
    /// - 50023001 = KnifeParent
    /// - 50024001 = KatanaParent
    /// - 50025001 = ShieldParent
    /// - etc.
    /// </remarks>
    public struct WeaponCategory : IComponentData
    {
        /// <summary>
        /// ObjectIdentifier ID for the specific parent transform on the character.
        /// If 0, defaults to the hand bone itself (HandAttachPoint).
        /// </summary>
        public uint WieldTargetID;

        /// <summary>
        /// Default weapon category (no specific parent).
        /// </summary>
        public static WeaponCategory Default => new WeaponCategory
        {
            WieldTargetID = 0
        };
    }

    /// <summary>
    /// Buffer element for caching weapon parent transforms on the character.
    /// Populated during character initialization from ObjectIdentifier components.
    /// </summary>
    /// <remarks>
    /// This buffer is added to character entities and stores mappings from
    /// ObjectIdentifier IDs to the actual parent transforms for weapon positioning.
    /// </remarks>
    public struct WeaponParentElement : IBufferElementData
    {
        /// <summary>
        /// The ObjectIdentifier ID from the character prefab.
        /// </summary>
        public uint ObjectIdentifierID;

        /// <summary>
        /// The Transform reference for this parent (used by MonoBehaviour bridge).
        /// In pure ECS, this would be an Entity reference instead.
        /// </summary>
        /// <remarks>
        /// We use a transform index/reference approach here since weapon visual
        /// parenting is currently handled in MonoBehaviour land (WeaponEquipVisualBridge).
        /// </remarks>
        public int TransformInstanceID;
    }

    /// <summary>
    /// Tag component indicating that a character has weapon parents registered.
    /// Used for efficient querying.
    /// </summary>
    public struct HasWeaponParents : IComponentData { }
}
