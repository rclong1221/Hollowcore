namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Penalty severity levels, escalating from warning to permanent ban.
    /// </summary>
    public enum PenaltyLevel : byte
    {
        None = 0,
        Warn = 1,
        Kick = 2,
        TempBan = 3,
        PermaBan = 4
    }

    /// <summary>
    /// EPIC 17.11: Categories of anti-cheat violations.
    /// </summary>
    public enum ViolationType : byte
    {
        RateLimit = 0,
        Movement = 1,
        Economy = 2,
        Cooldown = 3,
        Generic = 4
    }

    /// <summary>
    /// EPIC 17.11: Source system for economy audit trail.
    /// </summary>
    public enum TransactionSourceSystem : byte
    {
        Craft = 0,
        Trade = 1,
        Loot = 2,
        Quest = 3,
        Admin = 4,
        Death = 5,
        Vendor = 6,
        Reward = 7
    }

    /// <summary>
    /// EPIC 17.11: Stable RPC type identifiers for rate limiting.
    /// Each RPC type has a unique ushort ID used as the key in RateLimitEntry buffers.
    /// </summary>
    public static class RpcTypeIds
    {
        public const ushort DIALOGUE_CHOICE = 1;
        public const ushort DIALOGUE_SKIP = 2;
        public const ushort CRAFT_REQUEST = 3;
        public const ushort STAT_ALLOCATION = 4;
        public const ushort TALENT_ALLOCATION = 5;
        public const ushort TALENT_RESPEC = 6;
        public const ushort VOXEL_DAMAGE = 7;
        public const ushort TRADE_REQUEST = 8;
        public const ushort RESPAWN = 9;
        // 10-255: reserved for future RPCs
    }
}
