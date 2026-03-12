using Unity.Entities;

namespace DIG.Trading
{
    /// <summary>
    /// EPIC 17.3: Trade session state. Lives on a separate trade session entity (NOT on player).
    /// Server-only — clients receive state via notification RPCs.
    /// </summary>
    public struct TradeSessionState : IComponentData
    {
        /// <summary>Player A (trade requester).</summary>
        public Entity InitiatorEntity;
        /// <summary>Player B (trade recipient).</summary>
        public Entity TargetEntity;
        /// <summary>Current session state.</summary>
        public TradeState State;
        /// <summary>NetworkTick when session was created.</summary>
        public uint CreationTick;
        /// <summary>NetworkTick of last offer change.</summary>
        public uint LastModifiedTick;
        /// <summary>Connection entity for Player A (for sending RPCs).</summary>
        public Entity InitiatorConnection;
        /// <summary>Connection entity for Player B (for sending RPCs).</summary>
        public Entity TargetConnection;
    }

    /// <summary>
    /// EPIC 17.3: Trade session lifecycle states.
    /// </summary>
    public enum TradeState : byte
    {
        /// <summary>Waiting for target to accept.</summary>
        Pending = 0,
        /// <summary>Both players can modify offers.</summary>
        Active = 1,
        /// <summary>Both confirmed, server validating and executing.</summary>
        Executing = 2,
        /// <summary>Trade executed successfully (pending cleanup).</summary>
        Completed = 3,
        /// <summary>Validation failed (pending cleanup).</summary>
        Failed = 4,
        /// <summary>One player cancelled (pending cleanup).</summary>
        Cancelled = 5
    }

    /// <summary>
    /// EPIC 17.3: Tag component to identify trade session entities in queries.
    /// </summary>
    public struct TradeSessionTag : IComponentData { }
}
