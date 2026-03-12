using UnityEngine;
using Unity.Entities;
using DIG.Shared;

namespace DIG.Ship.Cargo.Authoring
{
    /// <summary>
    /// Authoring component for ship cargo capacity.
    /// Add to ship root to enable cargo storage.
    /// </summary>
    [AddComponentMenu("DIG/Ship/Ship Cargo Authoring")]
    public class ShipCargoAuthoring : MonoBehaviour
    {
        [Header("Cargo Capacity")]
        [Tooltip("Maximum cargo weight in kg")]
        public float MaxWeight = 1000f;

        [Header("Initial Cargo (optional)")]
        [Tooltip("Starting cargo items for testing")]
        public InitialCargoItem[] InitialCargo;

        [System.Serializable]
        public struct InitialCargoItem
        {
            public ResourceType ResourceType;
            public int Quantity;
        }
    }

    /// <summary>
    /// Baker for ShipCargoAuthoring.
    /// </summary>
    public class ShipCargoBaker : Baker<ShipCargoAuthoring>
    {
        public override void Bake(ShipCargoAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Add cargo capacity
            AddComponent(entity, new ShipCargoCapacity
            {
                MaxWeight = authoring.MaxWeight,
                CurrentWeight = 0f,
                IsOverCapacity = false
            });

            // Add cargo buffer
            var buffer = AddBuffer<ShipCargoItem>(entity);

            // Add initial cargo if specified
            if (authoring.InitialCargo != null)
            {
                foreach (var item in authoring.InitialCargo)
                {
                    if (item.Quantity > 0)
                    {
                        buffer.Add(new ShipCargoItem
                        {
                            ResourceType = item.ResourceType,
                            Quantity = item.Quantity
                        });
                    }
                }
            }
        }
    }
}
