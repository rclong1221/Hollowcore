using Unity.Entities;
using UnityEngine;

namespace DIG.Widgets
{
    /// <summary>
    /// EPIC 15.26 Phase 6: Tag component for entities that should show a boss plate.
    /// Add to boss prefabs via BossPlateTagAuthoring. The WidgetProjectionSystem
    /// reads this tag to set WidgetFlags.BossPlate on the entity's active flags.
    /// </summary>
    public struct BossPlateTag : IComponentData { }
}

namespace DIG.Widgets.Authoring
{
    /// <summary>
    /// Baker for BossPlateTag. Add to boss enemy prefab roots.
    /// </summary>
    [AddComponentMenu("DIG/Widgets/Boss Plate Tag Authoring")]
    public class BossPlateTagAuthoring : MonoBehaviour
    {
        public class Baker : Baker<BossPlateTagAuthoring>
        {
            public override void Bake(BossPlateTagAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<BossPlateTag>(entity);
            }
        }
    }
}
