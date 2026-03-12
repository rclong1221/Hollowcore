using Unity.Entities;
using UnityEngine;
using DIG.Items.Bridges;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component that bakes WeaponAttachmentConfig into the WeaponCategory ECS component.
    /// The weapon only specifies which category it belongs to (WieldTargetID).
    /// The actual positioning is defined on the character's WeaponParentConfig.
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponCategoryAuthoring : MonoBehaviour
    {
        [Header("Configuration Source")]
        [Tooltip("WeaponAttachmentConfig to read from. Auto-found on same GameObject if not set.")]
        public WeaponAttachmentConfig AttachmentConfig;

        public class Baker : Baker<WeaponCategoryAuthoring>
        {
            public override void Bake(WeaponCategoryAuthoring authoring)
            {
                // Try to find WeaponAttachmentConfig
                var config = authoring.AttachmentConfig;
                if (config == null)
                {
                    config = authoring.GetComponent<WeaponAttachmentConfig>();
                }

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                if (config == null)
                {
                    // No config found, add default component
                    AddComponent(entity, WeaponCategory.Default);
                    return;
                }

                // Bake weapon category (just the WieldTargetID)
                AddComponent(entity, new WeaponCategory
                {
                    WieldTargetID = config.WieldTargetID
                });
            }
        }
    }
}
