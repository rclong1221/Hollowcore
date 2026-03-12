using Unity.Entities;

namespace Player.Components
{
    /// <summary>
    /// Per-entity timer used by FootstepSystem to track last step time.
    /// </summary>
    public struct FootstepTimer : IComponentData
    {
        public double LastStepTime;
    }
}
