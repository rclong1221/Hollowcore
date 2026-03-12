using Unity.Entities;
using Unity.Collections;

namespace Traits
{
    /// <summary>
    /// Generic attribute data stored in a DynamicBuffer.
    /// Replaces hardcoded Health, Stamina, Energy components.
    /// </summary>
    [InternalBufferCapacity(8)] // Sufficient for most entities (Health, Stamina, Oxygen, Hunger, Thirst...)
    public struct AttributeData : IBufferElementData
    {
        public FixedString32Bytes NameHash; // e.g. "Health"
        
        public float CurrentValue;
        public float MinValue;
        public float MaxValue;
        
        public float RegenRate; // Amount per second (can be negative for decay)
        public float RegenDelay; // Delay before regen starts after modification
        public float LastChangeTime; // Timestamp of last modification
        
        // Helper to Initialize
        public static AttributeData Create(string name, float max, float start, float regenRate = 0)
        {
            return new AttributeData
            {
                NameHash = new FixedString32Bytes(name),
                CurrentValue = start,
                MinValue = 0,
                MaxValue = max,
                RegenRate = regenRate,
                RegenDelay = 0,
                LastChangeTime = 0
            };
        }
    }
}
