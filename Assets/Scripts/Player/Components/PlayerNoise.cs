using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks how much noise the player is making for stealth mechanics.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerNoise : IComponentData
    {
        [GhostField] public float Current;  // Current noise level
        [GhostField] public float Max;      // Maximum noise threshold
        public float DecayRate;             // How fast noise fades
        public float LastNoiseTime;
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public bool IsLoud => Percent >= 0.7f;
        public bool IsQuiet => Percent <= 0.2f;
        
        public static PlayerNoise Default => new()
        {
            Current = 0f,
            Max = 100f,
            DecayRate = 30f,
            LastNoiseTime = 0f
        };
    }
}
