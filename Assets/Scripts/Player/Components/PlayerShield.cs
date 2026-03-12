using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// ECS component for player shield - energy barrier that absorbs damage.
    /// Recharges after a delay when not taking damage.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerShield : IComponentData
    {
        /// <summary>Current shield amount.</summary>
        [GhostField(Quantization = 100)] public float Current;
        
        /// <summary>Maximum shield capacity.</summary>
        [GhostField(Quantization = 100)] public float Max;
        
        /// <summary>Time since last damage taken (for recharge delay).</summary>
        [GhostField(Quantization = 100)] public float RechargeTimer;
        
        /// <summary>Delay before shield starts recharging after taking damage.</summary>
        public float RechargeDelay;
        
        /// <summary>Rate at which shield recharges per second.</summary>
        public float RechargeRate;
        
        /// <summary>Whether shield is currently broken (at 0).</summary>
        [GhostField] public bool IsBroken;
        
        /// <summary>Whether shield can break completely requiring full recharge.</summary>
        public bool CanBreak;
        
        /// <summary>Extra delay when shield breaks completely.</summary>
        public float BreakPenaltyDelay;
        
        /// <summary>Percentage of damage absorbed by shield (0-1).</summary>
        public float AbsorptionRatio;
        
        /// <summary>Current shield as percentage (0-1).</summary>
        public readonly float Percent => Max > 0 ? Current / Max : 0f;
        
        /// <summary>Whether shield can currently recharge.</summary>
        public readonly bool CanRecharge => RechargeTimer <= 0 && Current < Max;
        
        /// <summary>Whether shield is currently active (not broken).</summary>
        public readonly bool IsActive => !IsBroken && Current > 0;
        
        /// <summary>Create with default values.</summary>
        public static PlayerShield Default => new()
        {
            Current = 100f,
            Max = 100f,
            RechargeTimer = 0f,
            RechargeDelay = 3f,
            RechargeRate = 25f,
            IsBroken = false,
            CanBreak = true,
            BreakPenaltyDelay = 2f,
            AbsorptionRatio = 1f
        };
    }
}
