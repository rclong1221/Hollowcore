using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks player oxygen/breath for underwater or hazardous areas.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerOxygen : IComponentData
    {
        [GhostField] public float Current;
        [GhostField] public float Max;
        public float DrainRate;      // Per second while underwater/in hazard
        public float RecoveryRate;   // Per second when safe
        public float RecoveryDelay;  // Seconds before recovery starts
        public float LastDrainTime;
        public float SuffocationDamage;  // Damage per second when out of oxygen
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public bool IsSuffocating => Current <= 0;
        public bool IsLow => Percent <= 0.3f;
        
        public static PlayerOxygen Default => new()
        {
            Current = 60f,
            Max = 60f,
            DrainRate = 1f,
            RecoveryRate = 5f,
            RecoveryDelay = 0.5f,
            LastDrainTime = 0f,
            SuffocationDamage = 10f
        };
    }
}
