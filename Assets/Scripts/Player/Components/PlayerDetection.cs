using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks enemy awareness/detection of the player.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerDetection : IComponentData
    {
        [GhostField] public float Current;  // How detected (0 = hidden, Max = spotted)
        [GhostField] public float Max;
        public float DecayRate;             // How fast detection fades when hidden
        public float LastDetectionTime;
        
        public float Percent => Max > 0 ? Current / Max : 0f;
        public bool IsSpotted => Current >= Max;
        public bool IsAlerted => Percent >= 0.5f;
        public bool IsHidden => Percent <= 0.1f;
        
        public static PlayerDetection Default => new()
        {
            Current = 0f,
            Max = 100f,
            DecayRate = 15f,
            LastDetectionTime = 0f
        };
    }
}
