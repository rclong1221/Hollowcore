using Unity.Entities;

namespace Player.Components
{
    // Tracks landing recovery / stun
    public struct LandingState : IComponentData
    {
        public bool IsRecovering;
        public float RecoveryDuration;
        public float RecoveryTimer;
    }
}
