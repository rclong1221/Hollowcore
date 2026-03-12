using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Component placed on Weapon Prefabs to define how they should be held.
    /// Stores the Position/Rotation offset relative to a standard "Ghost Hand".
    /// Also defines IK target overrides.
    /// </summary>
    public class ItemGripAuthoring : MonoBehaviour
    {
        [Header("Grip Offset")]
        [Tooltip("Position offset relative to the standard Hand Socket")]
        public Vector3 GripPositionOffset;

        [Tooltip("Rotation offset relative to the standard Hand Socket")]
        public Quaternion GripRotationOffset = Quaternion.identity;

        [Header("IK Configuration")]
        [Tooltip("Optional: Transform for Left Hand IK. If null, Bridge will look for child named 'LeftHandAttach'.")]
        public Transform LeftHandIKOverride;

        [Tooltip("Optional: Transform for Right Hand IK (rarely used).")]
        public Transform RightHandIKOverride;

        /// <summary>
        /// Apply this grip's offset to the weapon transform, relative to the socket.
        /// </summary>
        /// <param name="weaponTransform">The weapon being equipped</param>
        /// <param name="socketTransform">The socket (parent)</param>
        public void ApplyGrip(Transform weaponTransform)
        {
            // Since we are parented to the socket, local position/rotation IS the offset.
            weaponTransform.localPosition = GripPositionOffset;
            weaponTransform.localRotation = GripRotationOffset;
        }

#if UNITY_EDITOR
        public void BakeCurrentTransformAsGrip()
        {
            GripPositionOffset = transform.localPosition;
            GripRotationOffset = transform.localRotation;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
