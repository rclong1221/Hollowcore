using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks player thirst for survival mechanics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerThirst : IComponentData
    {
        [GhostField] public float Current;  // Higher = more thirsty (0 = hydrated, Max = dehydrated)
        [GhostField] public float Max;
        public float IncreaseRate;  // Per second
        public float DecreaseOnDrink;
        public float DehydrationDamage;  // Damage per second when dehydrated
        public float DehydrationThreshold;  // Percentage at which damage starts (0-1)
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public float HydrationPercent => 1f - Percent; // Inverted for UI
        public bool IsDehydrated => Percent >= DehydrationThreshold;
        public bool IsThirsty => Percent >= 0.7f;
        
        public static PlayerThirst Default => new()
        {
            Current = 0f,
            Max = 100f,
            IncreaseRate = 0.15f,
            DecreaseOnDrink = 40f,
            DehydrationDamage = 3f,
            DehydrationThreshold = 0.9f
        };
    }
}
