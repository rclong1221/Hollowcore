using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using DIG.Items.Definitions;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component for ItemAnimationConfig.
    /// Allows designers to configure weapon animation behavior on prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class ItemAnimationConfigAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("The weapon category definition (replaces WeaponType enum).")]
        public WeaponCategoryDefinition Category;

        [Header("Animation Mapping")]
        [Tooltip("Value to set 'Slot0ItemID' (or 'Slot1ItemID') in Animator. e.g. 1=Rifle, 61=Magic.")]
        public int AnimatorItemID;

        [Tooltip("Value to set 'MovementSetID' in Animator. 0=Combat/Gun, 1=Melee, 2=Bow.")]
        public int MovementSetID;

        [Header("Melee Settings")]
        [Tooltip("Number of combo steps for this weapon (0 for non-melee).")]
        public int ComboCount;

        [Header("Use Mechanics")]
        [Tooltip("Duration of the 'Use' animation before returning to idle. Replaces hardcoded values.")]
        public float UseDuration;

        [Tooltip("If true, finding input will check 'IsChanneled' logic (e.g. streaming beam, bow draw).")]
        public bool IsChanneled;

        [Tooltip("If true, requires aiming (Right Mouse) before firing (Left Mouse) is allowed.")]
        public bool RequireAimToFire;

        [Header("Movement Restrictions")]
        [Tooltip("If true, character locomotion is locked while in the 'Use' state.")]
        public bool LockMovementDuringUse;

        [Tooltip("If true, movement input will cancel the current action.")]
        public bool CancelUseOnMove;

        [Header("Grip Settings")]
        [Tooltip("If true, this weapon uses both hands (e.g. Rifle, Greatsword). Off-hand visuals will be suppressed.")]
        public bool IsTwoHanded;

        public class Baker : Baker<ItemAnimationConfigAuthoring>
        {
            public override void Bake(ItemAnimationConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Get CategoryID from Category asset, fallback to "Gun"
                string categoryId = authoring.Category != null ? authoring.Category.CategoryID : "Gun";

                var config = new ItemAnimationConfig
                {
                    AnimatorItemID = authoring.AnimatorItemID,
                    MovementSetID = authoring.MovementSetID,
                    CategoryID = new FixedString32Bytes(categoryId),
                    ComboCount = authoring.ComboCount,
                    UseDuration = authoring.UseDuration,
                    IsChanneled = authoring.IsChanneled,
                    RequireAimToFire = authoring.RequireAimToFire,
                    LockMovementDuringUse = authoring.LockMovementDuringUse,
                    CancelUseOnMove = authoring.CancelUseOnMove,
                    IsTwoHanded = authoring.IsTwoHanded
                };

                AddComponent(entity, config);
            }
        }
    }
}
