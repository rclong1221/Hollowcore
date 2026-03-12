using Unity.Entities;

namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 0: Extended resource pool on a child entity for AI bosses needing 3-4 resource types.
    /// Linked from parent via ResourcePoolLink. NOT on ghost-replicated player entities.
    /// </summary>
    public struct ResourcePoolExtended : IComponentData
    {
        public ResourceSlot Slot2;
        public ResourceSlot Slot3;
    }

    /// <summary>
    /// Link from parent entity to child entity holding ResourcePoolExtended.
    /// Only present on entities that need more than 2 resource slots.
    /// </summary>
    public struct ResourcePoolLink : IComponentData
    {
        public Entity ExtendedEntity;
    }
}
