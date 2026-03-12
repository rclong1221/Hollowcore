using Unity.Entities;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.23: Singleton config for the EnemySeparationSystem.
    /// Tunable via EnemySeparationConfigAuthoring in the subscene Inspector.
    /// </summary>
    public struct EnemySeparationConfig : IComponentData
    {
        /// <summary>Distance within which enemies push apart (meters).</summary>
        public float SeparationRadius;

        /// <summary>Strength of the separation push force.</summary>
        public float SeparationWeight;

        /// <summary>Maximum separation displacement per second (m/s). Prevents teleporting.</summary>
        public float MaxSeparationSpeed;

        /// <summary>Run separation every N frames (must be power of 2). Higher = cheaper but less responsive.</summary>
        public int FrameInterval;
    }
}
