using Unity.Entities;
using UnityEngine;

namespace DIG.Items.Authoring
{
    /// <summary>
    /// Authoring component for world item pickups.
    /// Configures pickup behavior and contents.
    /// </summary>
    public class ItemPickupAuthoring : MonoBehaviour
    {
        [Header("Pickup Contents")]
        [Tooltip("Item type ID this pickup contains")]
        public int ItemTypeId;

        [Tooltip("Quantity for stackable items")]
        public int Quantity = 1;

        [Header("Pickup Behavior")]
        [Tooltip("Auto-pickup radius (meters)")]
        public float PickupRadius = 1.5f;

        [Tooltip("If true, requires interaction key. If false, auto-pickup on proximity")]
        public bool RequiresInteraction = false;

        public class Baker : Baker<ItemPickupAuthoring>
        {
            public override void Bake(ItemPickupAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new ItemPickup
                {
                    ItemTypeId = authoring.ItemTypeId,
                    Quantity = authoring.Quantity,
                    PickupRadius = authoring.PickupRadius,
                    RequiresInteraction = authoring.RequiresInteraction
                });
            }
        }
    }
}
