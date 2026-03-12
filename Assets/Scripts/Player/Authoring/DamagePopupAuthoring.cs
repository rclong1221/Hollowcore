using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class DamagePopupAuthoring : MonoBehaviour
    {
        public GameObject PopupPrefab;
        public float SpawnHeightOffset = 2.0f;
        public float RandomJitter = 0.5f;

        class Baker : Baker<DamagePopupAuthoring>
        {
            public override void Bake(DamagePopupAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new DamagePopupConfig
                {
                    PopupPrefab = GetEntity(authoring.PopupPrefab, TransformUsageFlags.None),
                    SpawnHeightOffset = authoring.SpawnHeightOffset,
                    RandomJitter = authoring.RandomJitter
                });
            }
        }
    }
}
