using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Configuration for magazine-style reload animations.
    /// Add to weapons with detachable magazines where you need
    /// additional logic beyond simple show/hide (transform caching, etc).
    /// For simpler weapons, WeaponVisualActionController alone is sufficient.
    /// </summary>
    public class WeaponReloadAuthoring : MonoBehaviour
    {
        [Header("First Person Magazine")]
        [Tooltip("The magazine mesh transform on the first-person weapon")]
        public Transform FirstPersonMagazineClip;
        
        [Tooltip("Hand bone to parent first-person magazine during reload")]
        public Transform FirstPersonClipAttachment;
        
        [Header("Third Person Magazine")]
        [Tooltip("The magazine mesh transform on the third-person weapon")]
        public Transform ThirdPersonMagazineClip;
        
        [Tooltip("Hand bone to parent third-person magazine during reload")]
        public Transform ThirdPersonClipAttachment;
        
        [Header("Magazine Prefabs")]
        [Tooltip("Physics prefab spawned when dropping the old magazine")]
        public GameObject DropMagazinePrefab;
        
        [Tooltip("Optional prefab for fresh magazine in hand")]
        public GameObject FreshMagazinePrefab;
        
        [Header("Behavior")]
        [Tooltip("Should the magazine be reparented to hand during animation?")]
        public bool DetachAttachClip = true;
        
        [Tooltip("Reset magazine local position/rotation when detaching to hand")]
        public bool ResetClipTransformOnDetach = false;
        
        [Tooltip("Magazine drop prefab type ID (for ECS spawning registry)")]
        public string MagazineDropTypeID = "AssaultRifleMag";
        
        /// <summary>
        /// Get the appropriate magazine clip for the current perspective.
        /// </summary>
        public Transform GetMagazineClip(bool firstPerson)
        {
            return firstPerson ? FirstPersonMagazineClip : ThirdPersonMagazineClip;
        }
        
        /// <summary>
        /// Get the appropriate clip attachment for the current perspective.
        /// </summary>
        public Transform GetClipAttachment(bool firstPerson)
        {
            return firstPerson ? FirstPersonClipAttachment : ThirdPersonClipAttachment;
        }
        
        /// <summary>
        /// Set the clip attachment at runtime (for varying character skeletons).
        /// </summary>
        public void SetClipAttachment(bool firstPerson, Transform handBone)
        {
            if (firstPerson)
                FirstPersonClipAttachment = handBone;
            else
                ThirdPersonClipAttachment = handBone;
        }
        
        /// <summary>
        /// Find and set clip attachment by searching for bone name in hierarchy.
        /// </summary>
        public void SetClipAttachmentByName(bool firstPerson, Transform characterRoot, params string[] boneNames)
        {
            foreach (var boneName in boneNames)
            {
                var bone = FindBoneRecursive(characterRoot, boneName);
                if (bone != null)
                {
                    SetClipAttachment(firstPerson, bone);
                    Debug.Log($"[WeaponReloadAuthoring] Found and set {(firstPerson ? "FP" : "TP")} clip attachment to: {bone.name}");
                    return;
                }
            }
            Debug.LogWarning($"[WeaponReloadAuthoring] Could not find clip attachment bone in {characterRoot.name}. Tried: {string.Join(", ", boneNames)}");
        }
        
        private Transform FindBoneRecursive(Transform parent, string boneName)
        {
            if (parent.name == boneName || parent.name.EndsWith(boneName))
                return parent;
            
            foreach (Transform child in parent)
            {
                var result = FindBoneRecursive(child, boneName);
                if (result != null) return result;
            }
            return null;
        }
    }
}
