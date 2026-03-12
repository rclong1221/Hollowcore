using UnityEngine;

namespace DIG.Items.Bridges
{
    /// <summary>
    /// Configuration component for weapon category positioning on character prefabs.
    /// Place this on each weapon parent transform (AssaultRifleParent, PistolParent, etc.)
    /// to define how weapons of that category should be positioned on this character.
    ///
    /// This is the character-side of the positioning system. Weapons only specify
    /// which category they belong to (WieldTargetID), and the character defines
    /// how to hold each category via this component.
    /// </summary>
    public class WeaponParentConfig : MonoBehaviour
    {
        [Header("Equipped Transform")]
        [Tooltip("Local position offset for weapons in this category")]
        public Vector3 WeaponLocalPosition = Vector3.zero;

        [Tooltip("Local rotation (euler) for weapons in this category")]
        public Vector3 WeaponLocalRotation = Vector3.zero;

        [Tooltip("Local scale for weapons in this category")]
        public Vector3 WeaponLocalScale = Vector3.one;

        [Header("IK Targets")]
        [Tooltip("Left hand IK target for two-handed weapons (should be a child of this transform)")]
        public Transform LeftHandIKTarget;

        [Tooltip("Right hand IK target (optional, should be a child of this transform)")]
        public Transform RightHandIKTarget;

        [Header("Holster")]
        [Tooltip("Holster target transform for this category (if different from default back)")]
        public Transform HolsterTarget;

        [Tooltip("Local position when holstered")]
        public Vector3 HolsterLocalPosition = Vector3.zero;

        [Tooltip("Local rotation (euler) when holstered")]
        public Vector3 HolsterLocalRotation = Vector3.zero;
    }
}
