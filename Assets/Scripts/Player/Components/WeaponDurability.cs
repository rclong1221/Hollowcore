using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks weapon durability/condition.
    /// Add this to weapon entities, not the player.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct WeaponDurability : IComponentData
    {
        [GhostField] public float Current;
        [GhostField] public float Max;
        public float DegradePerUse;    // How much durability lost per attack/shot
        public float DegradePerBlock;  // How much durability lost per blocked hit
        public float RepairAmount;     // How much repair kits restore
        public float MaxRepairDurability;  // Max durability after repair
        public bool DestroyOnBreak;    // Destroy weapon when broken
        public bool DisableOnBreak;    // Disable weapon when broken (if not destroyed)
        public bool CanRepair;         // Can this weapon be repaired
        [GhostField] public bool IsBroken;  // Currently broken state
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public bool IsLow => Percent <= 0.25f;
        public bool NeedsRepair => Percent <= 0.5f;
        
        public static WeaponDurability Default => new()
        {
            Current = 100f,
            Max = 100f,
            DegradePerUse = 1f,
            DegradePerBlock = 2f,
            RepairAmount = 50f,
            MaxRepairDurability = 100f,
            DestroyOnBreak = false,
            DisableOnBreak = true,
            CanRepair = true,
            IsBroken = false
        };
    }
}
