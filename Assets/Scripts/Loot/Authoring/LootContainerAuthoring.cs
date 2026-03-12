using Unity.Entities;
using UnityEngine;
using DIG.Loot.Components;
using DIG.Loot.Definitions;

namespace DIG.Loot.Authoring
{
    /// <summary>
    /// EPIC 16.6: Authoring component for loot containers (chests, crates, barrels).
    /// </summary>
    [AddComponentMenu("DIG/Loot/Loot Container")]
    public class LootContainerAuthoring : MonoBehaviour
    {
        [Header("Container Settings")]
        public ContainerType ContainerType = ContainerType.Chest;

        [Tooltip("Loot table rolled when container is opened.")]
        public LootTableSO LootTable;

        [Tooltip("Time to play opening animation before loot spawns.")]
        [Min(0f)]
        public float OpenDuration = 1f;

        [Header("Respawn")]
        [Tooltip("If true, container reseals after RespawnTime and can be opened again.")]
        public bool IsReusable;

        [Tooltip("Time before reusable container reseals (seconds).")]
        [Min(0f)]
        public float RespawnTime = 300f;

        [Header("Key Requirement")]
        [Tooltip("If true, player must have a specific key item to open.")]
        public bool RequiresKey;

        [Tooltip("ItemTypeId of the required key.")]
        public int RequiredKeyItemId;

        private class Baker : Baker<LootContainerAuthoring>
        {
            public override void Bake(LootContainerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                int tableId = authoring.LootTable != null ? authoring.LootTable.GetInstanceID() : 0;

                AddComponent(entity, new LootContainerState
                {
                    Type = authoring.ContainerType,
                    Phase = LootContainerPhase.Sealed,
                    LootTableId = tableId,
                    OpenDuration = authoring.OpenDuration,
                    IsReusable = authoring.IsReusable,
                    RespawnTime = authoring.RespawnTime,
                    LastOpenedTime = 0f,
                    RequiresKey = authoring.RequiresKey,
                    RequiredKeyItemId = authoring.RequiredKeyItemId
                });

                // Add PendingLootSpawn buffer for loot results
                AddBuffer<PendingLootSpawn>(entity);
            }
        }
    }
}
