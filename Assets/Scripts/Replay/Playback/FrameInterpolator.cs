using System.Collections.Generic;
using Unity.Mathematics;

namespace DIG.Replay
{
    /// <summary>
    /// EPIC 18.10: Interpolates entity positions/rotations between two recorded frames.
    /// Used by ReplayPlayer for smooth playback between tick snapshots.
    /// </summary>
    public static class FrameInterpolator
    {
        /// <summary>
        /// Given two frames (fromFrame at fromTick, toFrame at toTick) and a target tick,
        /// populates the output dictionary with interpolated component data for each entity.
        /// The output dictionary is cleared before use — caller provides a reusable dictionary.
        /// </summary>
        public static void Interpolate(
            Dictionary<ushort, EntityComponentData> fromFrame,
            Dictionary<ushort, EntityComponentData> toFrame,
            uint fromTick, uint toTick, uint targetTick,
            Dictionary<ushort, EntityComponentData> output)
        {
            float t = (toTick > fromTick)
                ? (float)(targetTick - fromTick) / (toTick - fromTick)
                : 0f;
            t = math.saturate(t);

            output.Clear();

            foreach (var kvp in toFrame)
            {
                if (fromFrame.TryGetValue(kvp.Key, out var from))
                {
                    output[kvp.Key] = new EntityComponentData
                    {
                        Position = math.lerp(from.Position, kvp.Value.Position, t),
                        Rotation = math.slerp(from.Rotation, kvp.Value.Rotation, t),
                        Velocity = math.lerp(from.Velocity, kvp.Value.Velocity, t),
                        HealthCurrent = math.lerp(from.HealthCurrent, kvp.Value.HealthCurrent, t),
                        HealthMax = kvp.Value.HealthMax,
                        DeathPhase = kvp.Value.DeathPhase
                    };
                }
                else
                {
                    // Entity appeared mid-frame — use target data directly
                    output[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
