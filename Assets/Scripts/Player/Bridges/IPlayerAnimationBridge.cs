using Player.Systems;

namespace Player.Bridges
{
    /// <summary>
    /// Adapter interface for presentation bridges that apply animation state and handle animation events.
    /// Implement this on small per-move adapters or on the existing `AnimatorRigBridge` to allow the
    /// `PlayerAnimatorBridgeSystem` to prefer adapters when present.
    /// </summary>
    public interface IPlayerAnimationBridge
    {
        void ApplyAnimationState(PlayerAnimationState state, float deltaTime);
        void TriggerLanding();
    }

}
