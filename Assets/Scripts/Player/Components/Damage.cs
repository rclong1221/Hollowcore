using Unity.Entities;

namespace Player.Components
{
    // Buffer element for queued damage amounts
    public struct Damage : IBufferElementData
    {
        public float Amount;
    }
}
