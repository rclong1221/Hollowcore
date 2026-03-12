using Unity.Entities;

namespace DIG.Music
{
    /// <summary>
    /// EPIC 17.5: Static helper for requesting music stingers from any system.
    /// Follows the XPGrantAPI pattern — creates a transient MusicStingerRequest entity.
    /// </summary>
    public static class MusicStingerAPI
    {
        /// <summary>
        /// Request a one-shot stinger to be played.
        /// </summary>
        /// <param name="ecb">EntityCommandBuffer (from EndSimulationEntityCommandBufferSystem or similar).</param>
        /// <param name="stingerId">StingerId from MusicDatabaseSO.Stingers.</param>
        /// <param name="priority">Higher interrupts lower. Use StingerPriority constants.</param>
        /// <param name="allowOverlap">If true, ignores cooldown and plays alongside current stinger.</param>
        /// <param name="volumeScale">Multiplier on config StingerVolume (default 1.0).</param>
        public static void RequestStinger(EntityCommandBuffer ecb, int stingerId, byte priority = 50, bool allowOverlap = false, float volumeScale = 1f)
        {
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new MusicStingerRequest
            {
                StingerId = stingerId,
                Priority = priority,
                AllowOverlap = allowOverlap,
                VolumeScale = volumeScale
            });
        }

        /// <summary>
        /// Request a one-shot stinger directly via EntityManager (for managed systems).
        /// </summary>
        public static void RequestStinger(EntityManager em, int stingerId, byte priority = 50, bool allowOverlap = false, float volumeScale = 1f)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new MusicStingerRequest
            {
                StingerId = stingerId,
                Priority = priority,
                AllowOverlap = allowOverlap,
                VolumeScale = volumeScale
            });
        }
    }
}
