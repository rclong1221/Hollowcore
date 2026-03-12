using UnityEngine;

namespace DIG.Items.Bridges
{
    /// <summary>
    /// Minimal configuration component for weapon prefabs.
    /// Specifies which weapon category this weapon belongs to.
    ///
    /// The actual positioning (offsets, IK targets, holster) is defined on the
    /// character prefab via WeaponParentConfig components on each weapon parent.
    ///
    /// The WieldTargetID maps to ObjectIdentifier IDs on character prefabs:
    /// - 50001001 = AssaultRifleParent
    /// - 50002001 = PistolParent (Right Hand)
    /// - 50002002 = PistolParent (Left Hand)
    /// - 50003001 = ShotgunParent
    /// - 50005001 = SniperParent
    /// - 50022001 = SwordParent
    /// - 50023001 = KnifeParent
    /// - 50024001 = KatanaParent
    /// - 50025001 = ShieldParent
    /// - etc.
    /// </summary>
    public class WeaponAttachmentConfig : MonoBehaviour
    {
        [Header("Weapon Category")]
        [Tooltip("ObjectIdentifier ID of the parent transform on the character (e.g., 50001001 for AssaultRifleParent). " +
                 "Set to 0 for default hand attachment. The character's WeaponParentConfig on this parent " +
                 "defines the actual positioning.")]
        public uint WieldTargetID = 0;
    }
}
