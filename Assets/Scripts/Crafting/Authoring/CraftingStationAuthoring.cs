using Unity.Entities;
using UnityEngine;

namespace DIG.Crafting
{
    /// <summary>
    /// EPIC 16.13: Authoring component for crafting stations.
    /// Place alongside StationAuthoring + InteractableAuthoring on station GameObjects.
    /// Baker adds CraftingStation + all 5 buffers (queue, output, request, collect, cancel).
    /// </summary>
    [AddComponentMenu("DIG/Crafting/Crafting Station")]
    public class CraftingStationAuthoring : MonoBehaviour
    {
        [Header("Station Type")]
        public StationType StationType = StationType.Workbench;

        [Header("Station Tier")]
        [Range(1, 5)]
        public int StationTier = 1;

        [Header("Speed")]
        [Range(0.5f, 3f)]
        [Tooltip("Crafting speed multiplier. Higher = faster crafting.")]
        public float SpeedMultiplier = 1f;

        [Header("Queue")]
        [Range(1, 4)]
        [Tooltip("Maximum number of crafts that can be queued per player.")]
        public int MaxQueueSize = 2;

        public class Baker : Baker<CraftingStationAuthoring>
        {
            public override void Bake(CraftingStationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new CraftingStation
                {
                    StationType = authoring.StationType,
                    StationTier = (byte)authoring.StationTier,
                    SpeedMultiplier = authoring.SpeedMultiplier,
                    MaxQueueSize = (byte)authoring.MaxQueueSize
                });

                // Add all buffers
                AddBuffer<CraftQueueElement>(entity);
                AddBuffer<CraftOutputElement>(entity);
                AddBuffer<CraftRequest>(entity);
                AddBuffer<CollectCraftRequest>(entity);
                AddBuffer<CancelCraftRequest>(entity);
            }
        }
    }
}
