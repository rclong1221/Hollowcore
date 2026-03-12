using Unity.Entities;

namespace DIG.PvP
{
    /// <summary>
    /// EPIC 17.10: Stores the player's original stats before normalization.
    /// Used to restore stats when leaving PvP mode. IEnableableComponent --
    /// enabled only when normalization is active.
    /// 20 bytes.
    /// </summary>
    public struct PvPStatOverride : IComponentData, IEnableableComponent
    {
        public float OriginalMaxHealth;
        public float OriginalAttackPower;
        public float OriginalSpellPower;
        public float OriginalDefense;
        public float OriginalArmor;
    }
}
