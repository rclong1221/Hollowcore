using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Traits
{
    /// <summary>
    /// Helper methods for interacting with Generic Attributes.
    /// </summary>
    public static class AttributeHelper
    {
        /// <summary>
        /// Modifies an attribute value by a delta.
        /// Returns true if attribute was found.
        /// </summary>
        public static bool ModifyAttribute(ref DynamicBuffer<AttributeData> buffer, FixedString32Bytes name, float delta, double currentTime)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].NameHash == name)
                {
                    var attr = buffer[i];
                    attr.CurrentValue = math.clamp(attr.CurrentValue + delta, attr.MinValue, attr.MaxValue);
                    attr.LastChangeTime = (float)currentTime;
                    buffer[i] = attr;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the current value of an attribute. Returns -1 if not found.
        /// </summary>
        public static float GetAttributeValue(DynamicBuffer<AttributeData> buffer, FixedString32Bytes name)
        {
             for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].NameHash == name)
                {
                    return buffer[i].CurrentValue;
                }
            }
            return -1f;
        }
    }
}
