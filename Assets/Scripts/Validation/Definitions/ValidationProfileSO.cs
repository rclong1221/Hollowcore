using System;
using UnityEngine;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Per-RPC-type rate limit configuration.
    /// </summary>
    [Serializable]
    public struct RpcRateLimitEntry
    {
        [Tooltip("Stable RPC type identifier (from RpcTypeIds).")]
        public ushort RpcTypeId;
        [Tooltip("Human-readable name for editor display.")]
        public string DisplayName;
        [Tooltip("Token refill rate (tokens per second).")]
        [Min(0.01f)] public float TokensPerSecond;
        [Tooltip("Maximum token bucket capacity (burst limit).")]
        [Min(1f)] public float MaxBurst;
        [Tooltip("Severity when rate limit exceeded (0.0-1.0).")]
        [Range(0f, 1f)] public float ViolationSeverity;
    }

    /// <summary>
    /// EPIC 17.11: Validation profile defining per-RPC rate limits.
    /// Place in Resources/ValidationProfile.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Validation/Validation Profile")]
    public class ValidationProfileSO : ScriptableObject
    {
        [Tooltip("Per-RPC-type rate limit configuration.")]
        public RpcRateLimitEntry[] RpcRateLimits = new[]
        {
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.DIALOGUE_CHOICE, DisplayName = "Dialogue Choice", TokensPerSecond = 2f, MaxBurst = 3f, ViolationSeverity = 0.3f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.DIALOGUE_SKIP, DisplayName = "Dialogue Skip", TokensPerSecond = 2f, MaxBurst = 3f, ViolationSeverity = 0.3f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.CRAFT_REQUEST, DisplayName = "Craft Request", TokensPerSecond = 3f, MaxBurst = 5f, ViolationSeverity = 0.5f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.STAT_ALLOCATION, DisplayName = "Stat Allocation", TokensPerSecond = 1f, MaxBurst = 5f, ViolationSeverity = 0.7f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.TALENT_ALLOCATION, DisplayName = "Talent Allocation", TokensPerSecond = 1f, MaxBurst = 5f, ViolationSeverity = 0.7f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.TALENT_RESPEC, DisplayName = "Talent Respec", TokensPerSecond = 0.1f, MaxBurst = 1f, ViolationSeverity = 0.9f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.VOXEL_DAMAGE, DisplayName = "Voxel Damage", TokensPerSecond = 10f, MaxBurst = 20f, ViolationSeverity = 0.3f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.TRADE_REQUEST, DisplayName = "Trade Request", TokensPerSecond = 1f, MaxBurst = 3f, ViolationSeverity = 0.8f },
            new RpcRateLimitEntry { RpcTypeId = RpcTypeIds.RESPAWN, DisplayName = "Respawn", TokensPerSecond = 0.5f, MaxBurst = 2f, ViolationSeverity = 0.5f },
        };

        [Tooltip("Fallback refill rate for unregistered RPC types.")]
        [Min(0.1f)] public float DefaultTokensPerSecond = 2f;

        [Tooltip("Fallback max token count for unregistered RPC types.")]
        [Min(1f)] public float DefaultMaxBurst = 5f;
    }
}
