using Unity.Entities;
using UnityEngine;
using DIG.Loot.Components;
using DIG.Loot.Definitions;

namespace DIG.Loot.Authoring
{
    /// <summary>
    /// EPIC 16.6: Assigns a loot table to an enemy prefab.
    /// Add this to enemy GameObjects alongside DamageableAuthoring.
    /// </summary>
    [AddComponentMenu("DIG/Loot/Loot Table")]
    public class LootTableAuthoring : MonoBehaviour
    {
        [Tooltip("The loot table rolled on death.")]
        public LootTableSO LootTable;

        [Tooltip("Multiplier applied to all drop chances (1.0 = normal).")]
        [Min(0f)]
        public float DropChanceMultiplier = 1f;

        [Tooltip("Multiplier applied to all drop quantities (1.0 = normal).")]
        [Min(0f)]
        public float QuantityMultiplier = 1f;

        private class Baker : Baker<LootTableAuthoring>
        {
            public override void Bake(LootTableAuthoring authoring)
            {
                if (authoring.LootTable == null) return;

                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new LootTableRef
                {
                    LootTableId = authoring.LootTable.GetInstanceID(),
                    DropChanceMultiplier = authoring.DropChanceMultiplier,
                    QuantityMultiplier = authoring.QuantityMultiplier,
                    HasDropped = false
                });

                // Add PendingLootSpawn buffer (empty, filled on death by DeathLootSystem)
                AddBuffer<PendingLootSpawn>(entity);
            }
        }
    }
}
