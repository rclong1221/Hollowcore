using Unity.Entities;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Ring buffer element for trade audit logging.
    /// Lives on the TradeConfig singleton entity. InternalBufferCapacity=0
    /// because the ring buffer is sized dynamically (256 entries).
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TradeAuditLog : IBufferElementData
    {
        /// <summary>Ghost ID of initiator (Player A).</summary>
        public int InitiatorGhostId;
        /// <summary>Ghost ID of target (Player B).</summary>
        public int TargetGhostId;
        /// <summary>NetworkTick at trade completion/failure.</summary>
        public uint Timestamp;
        /// <summary>Total items exchanged (both sides combined).</summary>
        public byte ItemCount;
        /// <summary>Net gold change from A's perspective (+received, -given).</summary>
        public int GoldDelta;
        /// <summary>Net premium change from A's perspective.</summary>
        public int PremiumDelta;
        /// <summary>Net crafting currency change from A's perspective.</summary>
        public int CraftingDelta;
        /// <summary>0 = success, 1+ = specific failure reason.</summary>
        public byte ResultCode;
    }

    /// <summary>
    /// EPIC 17.3: Tracks the write cursor for the audit ring buffer.
    /// Lives on the same entity as TradeAuditLog. Avoids O(N) RemoveAt(0).
    /// </summary>
    public struct TradeAuditState : IComponentData
    {
        /// <summary>Next write position (mod MaxAuditEntries). Wraps around.</summary>
        public int NextWriteIndex;
        /// <summary>Total entries written (for count = min(Total, Max)).</summary>
        public int TotalWritten;
    }
}
