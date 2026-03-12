using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// Authoring component for resource nodes.
    /// </summary>
    [RequireComponent(typeof(InteractableAuthoring))]
    public class ResourceAuthoring : MonoBehaviour
    {
        [Header("Resource Settings")]
        [Tooltip("Type of resource (matches DIG.Shared.ResourceType)")]
        public DIG.Shared.ResourceType ResourceType = DIG.Shared.ResourceType.Stone;

        [Tooltip("Initial/max amount available")]
        public int MaxAmount = 10;

        [Tooltip("Amount collected per action")]
        public int AmountPerCollection = 1;

        [Header("Collection")]
        [Tooltip("Seconds to collect one unit")]
        public float CollectionTime = 1f;

        [Tooltip("Requires a specific tool")]
        public bool RequiresTool = false;

        [Header("Respawn")]
        [Tooltip("Seconds to respawn after depleted (0 = no respawn)")]
        public float RespawnTime = 0f;

        public class Baker : Baker<ResourceAuthoring>
        {
            public override void Bake(ResourceAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new ResourceInteractable
                {
                    ResourceTypeId = (byte)authoring.ResourceType,
                    CurrentAmount = authoring.MaxAmount,
                    MaxAmount = authoring.MaxAmount,
                    CollectionTime = authoring.CollectionTime,
                    AmountPerCollection = authoring.AmountPerCollection,
                    RequiresTool = authoring.RequiresTool,
                    RespawnTime = authoring.RespawnTime
                });

                // Ensure InteractableAuthoring sets Type = Timed for resources
            }
        }
    }
}
