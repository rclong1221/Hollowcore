using Unity.Entities;
using UnityEngine;

namespace DIG.Combat.Knockback
{
    /// <summary>
    /// EPIC 16.9: Add to any entity prefab that should be knockback-capable.
    /// Entities without this component ignore all KnockbackRequests targeting them.
    /// </summary>
    public class KnockbackStateAuthoring : MonoBehaviour
    {
        private class Baker : Baker<KnockbackStateAuthoring>
        {
            public override void Bake(KnockbackStateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new KnockbackState());
            }
        }
    }
}
