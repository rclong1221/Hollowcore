using System.Collections.Generic;
using Unity.Mathematics;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Delta encoding/decoding for replay snapshots.
    /// Compares entity snapshots between current frame and last keyframe.
    /// Returns only changed entities for delta frames.
    /// </summary>
    public static class DeltaEncoder
    {
        private const float PositionEpsilon = 0.001f;
        private const float RotationEpsilon = 0.0001f;
        private const float HealthEpsilon = 0.01f;

        /// <summary>
        /// Given current frame data and the last keyframe data (indexed by ghost ID),
        /// populates the output list with only the entities whose component data differs.
        /// The output list is cleared before use — caller provides a reusable list.
        /// </summary>
        public static void EncodeDelta(
            Dictionary<ushort, EntityComponentData> currentFrame,
            Dictionary<ushort, EntityComponentData> lastKeyframe,
            List<KeyValuePair<ushort, EntityComponentData>> output)
        {
            output.Clear();
            foreach (var kvp in currentFrame)
            {
                if (!lastKeyframe.TryGetValue(kvp.Key, out var prev) || !ComponentDataEqual(prev, kvp.Value))
                {
                    output.Add(kvp);
                }
            }
        }

        /// <summary>
        /// Reconstruct a full frame by applying deltas on top of last keyframe.
        /// Used during playback seeking. Populates the output dictionary.
        /// The output dictionary is cleared before use — caller provides a reusable dictionary.
        /// </summary>
        public static void ApplyDelta(
            Dictionary<ushort, EntityComponentData> keyframe,
            List<KeyValuePair<ushort, EntityComponentData>> delta,
            Dictionary<ushort, EntityComponentData> output)
        {
            output.Clear();
            foreach (var kvp in keyframe)
                output[kvp.Key] = kvp.Value;
            foreach (var kvp in delta)
                output[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// Compare two EntityComponentData structs with epsilon tolerance.
        /// </summary>
        public static bool ComponentDataEqual(EntityComponentData a, EntityComponentData b)
        {
            if (math.any(math.abs(a.Position - b.Position) > PositionEpsilon))
                return false;

            // Quaternion comparison: dot product near 1.0 means identical
            float dot = math.abs(math.dot(a.Rotation.value, b.Rotation.value));
            if (dot < 1f - RotationEpsilon)
                return false;

            if (math.any(math.abs(a.Velocity - b.Velocity) > PositionEpsilon))
                return false;

            if (math.abs(a.HealthCurrent - b.HealthCurrent) > HealthEpsilon)
                return false;

            if (math.abs(a.HealthMax - b.HealthMax) > HealthEpsilon)
                return false;

            if (a.DeathPhase != b.DeathPhase)
                return false;

            return true;
        }
    }
}
