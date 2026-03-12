using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks infection/poison status effects.
    /// Infection spreads over time, causing damage and visual effects.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerInfection : IComponentData
    {
        [GhostField] public float Current;  // 0 = clean, Max = fully infected
        [GhostField] public float Max;
        public float SpreadRate;     // How fast infection grows per second
        public float DamageRate;     // Damage per second when above threshold
        public float CureAmount;     // How much antidote reduces infection
        public float DamageThreshold;  // Infection % at which damage starts (0-1)
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public float HealthPercent => 1f - Percent; // Inverted for UI (healthy = 100%)
        public bool IsInfected => Current > 0;
        public bool IsTakingDamage => Percent >= DamageThreshold;
        public bool IsCritical => Percent >= 0.8f;
        public bool IsSpreading => SpreadRate > 0 && Current > 0 && Current < Max;
        
        public static PlayerInfection Default => new()
        {
            Current = 0f,
            Max = 100f,
            SpreadRate = 0.1f,
            DamageRate = 0.5f,
            CureAmount = 50f,
            DamageThreshold = 0.3f
        };
    }
}
