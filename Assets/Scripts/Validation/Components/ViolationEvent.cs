using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Transient entity representing a detected violation.
    /// Created by validation systems, consumed by ViolationAccumulatorSystem.
    /// 20 bytes.
    /// </summary>
    public struct ViolationEvent : IComponentData
    {
        /// <summary>The offending player entity.</summary>
        public Entity PlayerEntity;
        /// <summary>RateLimit(0), Movement(1), Economy(2), Cooldown(3), Generic(4).</summary>
        public byte ViolationType;
        public byte Padding0;
        /// <summary>Sub-type for telemetry (e.g., which RPC type was rate-limited).</summary>
        public ushort DetailCode;
        /// <summary>0.0-1.0, weighted by violation type.</summary>
        public float Severity;
        /// <summary>Server tick when violation occurred.</summary>
        public uint ServerTick;
    }
}
