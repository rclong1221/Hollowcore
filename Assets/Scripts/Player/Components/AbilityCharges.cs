using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks charges for special abilities (e.g., 3/3 grenades, magic spells).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct AbilityCharges : IComponentData
    {
        [GhostField] public int CurrentCharges;
        [GhostField] public int MaxCharges;
        [GhostField] public float RechargeProgress;  // 0-1 progress to next charge
        public float RechargeTime;                   // Seconds per charge
        public bool ParallelRecharge;                // All charges recharge at once
        public bool AllowOvercharge;                 // Allow exceeding max from buffs
        public int MaxOvercharge;                    // Maximum overcharge amount
        
        public float Percent => MaxCharges > 0 ? (float)CurrentCharges / MaxCharges : 0f;
        public bool HasCharges => CurrentCharges > 0;
        public bool IsFull => CurrentCharges >= MaxCharges;
        public bool IsRecharging => CurrentCharges < MaxCharges;
        public int EffectiveMax => AllowOvercharge ? MaxCharges + MaxOvercharge : MaxCharges;
        
        public static AbilityCharges Default => new()
        {
            CurrentCharges = 3,
            MaxCharges = 3,
            RechargeProgress = 0f,
            RechargeTime = 5f,
            ParallelRecharge = false,
            AllowOvercharge = false,
            MaxOvercharge = 1
        };
    }
}
