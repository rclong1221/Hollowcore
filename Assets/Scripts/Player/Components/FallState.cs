using Unity.Entities;
using Unity.Mathematics;

namespace Player.Components
{
    // Tracks airborne/fall-related state for a player
    public struct FallState : IComponentData
    {
        public bool IsFalling;
        public bool IsInFreeFall;
        public float FallStartHeight;
        public float FallDistance;
    }
}
