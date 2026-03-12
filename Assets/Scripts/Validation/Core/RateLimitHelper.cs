using Unity.Burst;
using Unity.Entities;

namespace DIG.Validation
{
    /// <summary>
    /// EPIC 17.11: Static helper for RPC rate limiting.
    /// Called by RPC receive systems to check token bucket before processing.
    /// O(K) linear scan where K < 10 RPC types per player.
    /// </summary>
    [BurstCompile]
    public static class RateLimitHelper
    {
        /// <summary>
        /// Check if an RPC is allowed (has tokens) and consume one token.
        /// Returns true if allowed, false if rate-limited.
        /// </summary>
        public static bool CheckAndConsume(EntityManager em, Entity validationChild, ushort rpcTypeId)
        {
            if (validationChild == Entity.Null) return true;
            if (!em.HasBuffer<RateLimitEntry>(validationChild)) return true;

            var buffer = em.GetBuffer<RateLimitEntry>(validationChild);
            for (int i = 0; i < buffer.Length; i++)
            {
                var entry = buffer[i];
                if (entry.RpcTypeId != rpcTypeId) continue;

                if (entry.TokenCount >= 1f)
                {
                    entry.TokenCount -= 1f;
                    entry.BurstConsumed++;
                    buffer[i] = entry;
                    return true;
                }
                return false;
            }

            // Unknown RPC type — allow (no rate limit configured)
            return true;
        }

        /// <summary>
        /// Create a ViolationEvent transient entity via EntityManager.
        /// Call when rate limiting denies an RPC or validation fails.
        /// </summary>
        public static void CreateViolation(
            EntityManager em,
            Entity player,
            ViolationType type,
            float severity,
            ushort detailCode,
            uint serverTick)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new ViolationEvent
            {
                PlayerEntity = player,
                ViolationType = (byte)type,
                Severity = severity,
                DetailCode = detailCode,
                ServerTick = serverTick
            });
        }

        /// <summary>
        /// Create a ViolationEvent transient entity via EntityCommandBuffer (deferred).
        /// Burst-compatible — use from ISystem.OnUpdate.
        /// </summary>
        [BurstCompile]
        public static void CreateViolationDeferred(
            in EntityCommandBuffer ecb,
            in Entity player,
            ViolationType type,
            float severity,
            ushort detailCode,
            uint serverTick)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new ViolationEvent
            {
                PlayerEntity = player,
                ViolationType = (byte)type,
                Severity = severity,
                DetailCode = detailCode,
                ServerTick = serverTick
            });
        }
    }
}
