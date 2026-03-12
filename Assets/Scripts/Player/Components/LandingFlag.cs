using Unity.Entities;

namespace Player.Components
{
    /// <summary>
    /// Non-transient flag that indicates a recent landing happened. Read-only consumers
    /// (MonoBehaviour adapters) can inspect this without causing structural changes.
    /// The ECS systems will set this component (Add/Set) and decay/remove it over time.
    /// </summary>
    public struct LandingFlag : IComponentData
    {
        public float TimeLeft;
        public float Intensity;
    }
}
