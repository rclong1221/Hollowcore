using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class GodModeAuthoring : MonoBehaviour
    {
        public bool Enabled = false;

        class Baker : Baker<GodModeAuthoring>
        {
            public override void Bake(GodModeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new GodMode { Enabled = authoring.Enabled });
            }
        }
    }
}
