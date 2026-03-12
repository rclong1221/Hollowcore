using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 0: Core resource component with 2 fixed slots (64 bytes).
    /// Added to player/AI entities that use combat resources.
    /// Entities without this component treat all abilities as free.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ResourcePool : IComponentData
    {
        [GhostField] public ResourceSlot Slot0;
        [GhostField] public ResourceSlot Slot1;

        public readonly float GetCurrent(ResourceType type)
        {
            if (Slot0.Type == type) return Slot0.Current;
            if (Slot1.Type == type) return Slot1.Current;
            return 0f;
        }

        public readonly float GetMax(ResourceType type)
        {
            if (Slot0.Type == type) return Slot0.Max;
            if (Slot1.Type == type) return Slot1.Max;
            return 0f;
        }

        /// <summary>
        /// Returns true if entity has enough of a resource. Returns true if type is None (free ability).
        /// </summary>
        public readonly bool HasResource(ResourceType type, float amount)
        {
            if (type == ResourceType.None) return true;
            return GetCurrent(type) >= amount;
        }

        /// <summary>
        /// Try to deduct resource. Returns true if successful.
        /// </summary>
        public bool TryDeduct(ResourceType type, float amount, float currentTime)
        {
            if (type == ResourceType.None) return true;
            if (Slot0.Type == type)
            {
                if (Slot0.Current < amount) return false;
                Slot0.Current -= amount;
                Slot0.LastDrainTime = currentTime;
                return true;
            }
            if (Slot1.Type == type)
            {
                if (Slot1.Current < amount) return false;
                Slot1.Current -= amount;
                Slot1.LastDrainTime = currentTime;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add resource (generation, regen). Respects Max unless CanOverflow.
        /// </summary>
        public void Add(ResourceType type, float amount)
        {
            if (type == ResourceType.None) return;
            if (Slot0.Type == type)
            {
                float cap = (Slot0.Flags & ResourceFlags.CanOverflow) != 0
                    ? float.MaxValue : Slot0.Max;
                Slot0.Current = math.min(Slot0.Current + amount, cap);
                return;
            }
            if (Slot1.Type == type)
            {
                float cap = (Slot1.Flags & ResourceFlags.CanOverflow) != 0
                    ? float.MaxValue : Slot1.Max;
                Slot1.Current = math.min(Slot1.Current + amount, cap);
            }
        }

        /// <summary>
        /// Returns the index (0 or 1) of the slot matching the type, or -1 if not found.
        /// </summary>
        public readonly int GetSlotIndex(ResourceType type)
        {
            if (Slot0.Type == type) return 0;
            if (Slot1.Type == type) return 1;
            return -1;
        }

        public static ResourcePool Default => new ResourcePool
        {
            Slot0 = new ResourceSlot { Type = ResourceType.None },
            Slot1 = new ResourceSlot { Type = ResourceType.None }
        };
    }
}
