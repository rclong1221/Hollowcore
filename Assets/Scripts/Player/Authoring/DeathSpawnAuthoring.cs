using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    public class DeathSpawnAuthoring : MonoBehaviour
    {
        public GameObject[] PrefabsToSpawn;
        public bool ApplyExplosiveForce = false;

        class Baker : Baker<DeathSpawnAuthoring>
        {
            public override void Bake(DeathSpawnAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var buffer = AddBuffer<DeathSpawnElement>(entity);
                
                if (authoring.PrefabsToSpawn != null)
                {
                    foreach (var prefab in authoring.PrefabsToSpawn)
                    {
                        buffer.Add(new DeathSpawnElement
                        {
                            Prefab = GetEntity(prefab, TransformUsageFlags.None),
                            PositionOffset = float3.zero,
                            ApplyExplosiveForce = authoring.ApplyExplosiveForce
                        });
                    }
                }
            }
        }
    }
}
