using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Forces an AI to target a specific entity for a duration.
    /// Baked disabled by AggroAuthoring; enabled at runtime by encounter triggers
    /// or ability systems (e.g., ThreatFixateRandom action).
    ///
    /// While enabled, AggroTargetSelectorSystem always returns FixatedTarget.
    /// </summary>
    public struct ThreatFixate : IComponentData, IEnableableComponent
    {
        public Entity FixatedTarget;
        public float Duration;
        public float Timer;
    }
}
