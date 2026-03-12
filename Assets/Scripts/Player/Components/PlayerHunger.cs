using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks player hunger for survival mechanics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerHunger : IComponentData
    {
        [GhostField] public float Current;  // Higher = more hungry (0 = full, Max = starving)
        [GhostField] public float Max;
        public float IncreaseRate;  // Per second
        public float DecreaseOnEat; // How much eating reduces hunger
        public float StarvationDamage;  // Damage per second when starving
        public float StarvationThreshold;  // Percentage at which damage starts (0-1)
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public float SatietyPercent => 1f - Percent; // Inverted for UI (full = 100%)
        public bool IsStarving => Percent >= StarvationThreshold;
        public bool IsHungry => Percent >= 0.7f;
        
        public static PlayerHunger Default => new()
        {
            Current = 0f,
            Max = 100f,
            IncreaseRate = 0.1f,
            DecreaseOnEat = 50f,
            StarvationDamage = 2f,
            StarvationThreshold = 0.9f
        };
    }
}
