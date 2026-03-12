using UnityEngine;

namespace Audio.Systems
{
    /// <summary>
    /// Runtime audio-related settings and feature flags.
    /// Toggle `UseAnimatorForFootsteps` to switch between Animator-driven footstep timing
    /// (client-side) and DOTS-emitted footstep timers.
    /// </summary>
    public static class AudioSettings
    {
        // Default to true to prefer the Animator→ECS hybrid approach.
        public static bool UseAnimatorForFootsteps = true;
    }
}
