using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Tracks the psychological stress of the player.
    /// Stress increases in darkness or danger, decreases in light or safe zones.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerStressState : IComponentData
    {
        [GhostField(Quantization = 1000)]
        public float CurrentStress; // 0 to 100
        
        [GhostField(Quantization = 1000)]
        public float MaxStress;
        
        [GhostField(Quantization = 1000)]
        public float TimeInDarkness; // Accumulates when lights are off in dark zones
        
        public float StressRate; // Config: Gain per second
        public float RecoveryRate; // Config: Loss per second
    }
}
