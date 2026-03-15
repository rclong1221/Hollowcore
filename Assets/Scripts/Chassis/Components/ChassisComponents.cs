using Unity.Entities;
using Unity.NetCode;

namespace Hollowcore.Chassis
{
    public enum ChassisSlot : byte
    {
        Head = 0,
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        LeftLeg = 4,
        RightLeg = 5
    }

    /// <summary>
    /// Link from player entity to chassis child entity.
    /// Follows the TargetingModuleLink pattern from the framework.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct ChassisLink : IComponentData
    {
        [GhostField]
        public Entity ChassisEntity;
    }

    /// <summary>
    /// The modular body state. Lives on a dedicated child entity (not the player).
    /// Each slot holds an Entity reference to the equipped limb (Entity.Null = empty/destroyed).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct ChassisState : IComponentData
    {
        [GhostField] public Entity Head;
        [GhostField] public Entity Torso;
        [GhostField] public Entity LeftArm;
        [GhostField] public Entity RightArm;
        [GhostField] public Entity LeftLeg;
        [GhostField] public Entity RightLeg;

        /// <summary>
        /// Bitmask of which slots are destroyed (not just empty — physically lost).
        /// Bit index matches ChassisSlot enum value.
        /// </summary>
        [GhostField]
        public byte DestroyedSlotsMask;

        public Entity GetSlot(ChassisSlot slot) => slot switch
        {
            ChassisSlot.Head => Head,
            ChassisSlot.Torso => Torso,
            ChassisSlot.LeftArm => LeftArm,
            ChassisSlot.RightArm => RightArm,
            ChassisSlot.LeftLeg => LeftLeg,
            ChassisSlot.RightLeg => RightLeg,
            _ => Entity.Null
        };

        public void SetSlot(ChassisSlot slot, Entity entity)
        {
            switch (slot)
            {
                case ChassisSlot.Head: Head = entity; break;
                case ChassisSlot.Torso: Torso = entity; break;
                case ChassisSlot.LeftArm: LeftArm = entity; break;
                case ChassisSlot.RightArm: RightArm = entity; break;
                case ChassisSlot.LeftLeg: LeftLeg = entity; break;
                case ChassisSlot.RightLeg: RightLeg = entity; break;
            }
        }

        public bool IsSlotDestroyed(ChassisSlot slot) =>
            (DestroyedSlotsMask & (1 << (int)slot)) != 0;

        public void SetSlotDestroyed(ChassisSlot slot) =>
            DestroyedSlotsMask |= (byte)(1 << (int)slot);

        public void ClearSlotDestroyed(ChassisSlot slot) =>
            DestroyedSlotsMask &= (byte)~(1 << (int)slot);

        public int EquippedCount
        {
            get
            {
                int count = 0;
                if (Head != Entity.Null) count++;
                if (Torso != Entity.Null) count++;
                if (LeftArm != Entity.Null) count++;
                if (RightArm != Entity.Null) count++;
                if (LeftLeg != Entity.Null) count++;
                if (RightLeg != Entity.Null) count++;
                return count;
            }
        }
    }
}
