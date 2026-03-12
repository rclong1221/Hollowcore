using Unity.Entities;
using Unity.NetCode;

namespace DIG.Player
{
    /// <summary>
    /// Tracks progress of hold-to-interact actions (looting, lockpicking, etc.)
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct InteractionProgress : IComponentData
    {
        [GhostField] public float Current;
        [GhostField] public float Required;  // Time required to complete
        [GhostField] public bool IsActive;   // Currently interacting
        
        public float Percent => Required > 0 ? Current / Required : 0f;
        public bool IsComplete => Current >= Required;
        
        public static InteractionProgress Default => new()
        {
            Current = 0f,
            Required = 1f,
            IsActive = false
        };
    }
}
