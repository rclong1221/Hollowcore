using Unity.Entities;

namespace DIG.Combat.Resources
{
    /// <summary>
    /// EPIC 16.8 Phase 3: Stores base (pre-equipment) Max and RegenRate for each slot.
    /// Set once at bake time. ResourceModifierApplySystem reads this + equipment bonuses
    /// to compute effective ResourcePool values each frame.
    /// 16 bytes on player entity.
    /// </summary>
    public struct ResourcePoolBase : IComponentData
    {
        public float Slot0BaseMax;
        public float Slot0BaseRegen;
        public float Slot1BaseMax;
        public float Slot1BaseRegen;
    }
}
