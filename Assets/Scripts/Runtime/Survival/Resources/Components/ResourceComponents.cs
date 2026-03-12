using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Shared;

namespace DIG.Survival.Resources
{
    // ResourceType moved to DIG.Shared


    /// <summary>
    /// Types of interactions available.
    /// </summary>
    public enum InteractionType : byte
    {
        None = 0,
        Collect = 1,
        Use = 2,
        Examine = 3
    }

    // InventoryItem, InventoryCapacity, and ResourceWeights moved to DIG.Shared
    // to prevent circular dependencies with DIG.Interaction.

    /// <summary>
    /// Component for resource nodes in the world.
    /// Can be collected by players.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ResourceNode : IComponentData
    {
        /// <summary>
        /// Type of resource this node yields.
        /// </summary>
        [GhostField]
        public ResourceType ResourceType;

        /// <summary>
        /// Remaining quantity available.
        /// </summary>
        [GhostField]
        public int Amount;

        /// <summary>
        /// Initial amount for respawn.
        /// </summary>
        public int MaxAmount;

        /// <summary>
        /// If true, requires drill tool to collect.
        /// </summary>
        public bool RequiresDrill;

        /// <summary>
        /// Seconds to fully collect (0 = instant).
        /// </summary>
        public float CollectionTime;

        /// <summary>
        /// Seconds to respawn after depleted (0 = no respawn).
        /// </summary>
        public float RespawnTime;

        /// <summary>
        /// Amount collected per collection action.
        /// </summary>
        public int AmountPerCollection;
    }

    /// <summary>
    /// Tag added when resource node is depleted.
    /// Used for respawn timing.
    /// </summary>
    public struct ResourceNodeDepleted : IComponentData
    {
        /// <summary>
        /// Time since depletion for respawn tracking.
        /// </summary>
        public float TimeSinceDepletion;
    }

    // NOTE: Interactable, InteractionTarget, CollectionProgress, and InteractionDisplayState
    // have been moved to DIG.Interaction namespace (EPIC 13.8/13.9).
    // Use DIG.Interaction.Interactable + DIG.Interaction.ResourceInteractable for resource nodes.

    /// <summary>
    /// Request to collect resources from a node.
    /// Processed by server.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    [InternalBufferCapacity(2)]
    public struct CollectResourceRequest : IBufferElementData
    {
        /// <summary>
        /// Resource node to collect from.
        /// </summary>
        public Entity NodeEntity;

        /// <summary>
        /// Amount to collect (0 = default).
        /// </summary>
        public int Amount;
    }
}
