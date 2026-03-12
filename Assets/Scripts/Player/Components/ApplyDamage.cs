using Unity.Entities;

namespace Player.Components
{
    // Generic damage event component for other health systems to consume
    public struct ApplyDamage : IComponentData
    {
        public float Amount;
    }
}
