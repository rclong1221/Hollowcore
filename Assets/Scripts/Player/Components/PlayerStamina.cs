using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerStamina : IComponentData
    {
        [GhostField] public float Current;
        [GhostField] public float Max;
        public float DrainRate; // per second when consuming (not used for instant costs)
        public float RegenRate; // per second when not consuming
        
        /// <summary>Time when stamina was last drained (for regen delay)</summary>
        public float LastDrainTime;
        
        /// <summary>Delay in seconds before stamina starts regenerating after drain</summary>
        public float RegenDelay;

        /// <summary>Can sprint? (has stamina remaining)</summary>
        public bool CanSprint => Current > 0;

        public static PlayerStamina Default => new PlayerStamina
        {
            Current = 100f,
            Max = 100f,
            DrainRate = 0f,
            RegenRate = 10f,
            LastDrainTime = 0f,
            RegenDelay = 1f
        };
    }
}
