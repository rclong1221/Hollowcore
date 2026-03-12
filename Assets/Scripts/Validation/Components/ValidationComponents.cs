using Unity.Entities;
using Unity.Mathematics;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Marker tag on validation child entity.
    /// </summary>
    public struct ValidationChildTag : IComponentData { }

    /// <summary>
    /// EPIC 17.11: Back-reference from validation child to owning player entity.
    /// </summary>
    public struct ValidationOwner : IComponentData
    {
        public Entity Owner;
    }

    /// <summary>
    /// EPIC 17.11: Per-player violation tracking state. Lives on validation child entity.
    /// 24 bytes.
    /// </summary>
    public struct PlayerValidationState : IComponentData
    {
        /// <summary>Weighted violation accumulator (decays over time).</summary>
        public float ViolationScore;
        /// <summary>Server tick of last warning issued.</summary>
        public uint LastWarningTick;
        /// <summary>Current penalty level: None(0), Warn(1), Kick(2), TempBan(3), PermaBan(4).</summary>
        public byte PenaltyLevel;
        /// <summary>Escalation counter (resets after clean session).</summary>
        public byte ConsecutiveKicks;
        /// <summary>Warnings issued this session.</summary>
        public byte WarningCount;
        public byte Padding;
        /// <summary>Tick when player connected (for session duration checks).</summary>
        public uint SessionStartTick;
        /// <summary>Tick of most recent violation (for decay timing).</summary>
        public uint LastViolationTick;
    }

    /// <summary>
    /// EPIC 17.11: Per-RPC-type token bucket for rate limiting.
    /// Buffer on validation child entity. InternalBufferCapacity=8 (covers 9 RPC types).
    /// 12 bytes per element.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct RateLimitEntry : IBufferElementData
    {
        /// <summary>Stable identifier per RPC type (from RpcTypeIds).</summary>
        public ushort RpcTypeId;
        /// <summary>Current available tokens (refilled over time).</summary>
        public float TokenCount;
        /// <summary>Server tick of last refill.</summary>
        public uint LastRefillTick;
        /// <summary>Tokens consumed this frame (reset per frame by RateLimitRefillSystem).</summary>
        public ushort BurstConsumed;
    }

    /// <summary>
    /// EPIC 17.11: Server-side movement validation state. Lives on validation child entity.
    /// 24 bytes.
    /// </summary>
    public struct MovementValidationState : IComponentData
    {
        /// <summary>Last server-accepted position.</summary>
        public float3 LastValidatedPosition;
        /// <summary>Server tick of last validation.</summary>
        public uint LastValidatedTick;
        /// <summary>Cumulative position error (for gradual drift detection).</summary>
        public float AccumulatedError;
        /// <summary>Server-granted teleport immunity expiry tick.</summary>
        public uint TeleportCooldownTick;
    }

    /// <summary>
    /// EPIC 17.11: Economy transaction audit trail entry.
    /// Buffer on validation child entity. InternalBufferCapacity=16.
    /// 20 bytes per element.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct EconomyAuditEntry : IBufferElementData
    {
        /// <summary>CurrencyType (Gold=0, Premium=1, Crafting=2).</summary>
        public byte TransactionType;
        /// <summary>Which system initiated (from TransactionSourceSystem enum).</summary>
        public byte SourceSystem;
        public short Padding;
        /// <summary>Signed delta (+income, -expense).</summary>
        public int Amount;
        /// <summary>Balance before this transaction.</summary>
        public int BalanceBefore;
        /// <summary>Balance after this transaction.</summary>
        public int BalanceAfter;
        /// <summary>Server tick when this transaction occurred.</summary>
        public uint ServerTick;
    }
}
