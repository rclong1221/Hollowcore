using Unity.Entities;
using UnityEngine;

namespace Audio.Components
{
    /// <summary>
    /// Runtime state for an active audio source linked to this entity.
    /// Managed by AudioSourcePoolSystem. Not baked — added at runtime when a sound plays.
    /// This is a managed component (class) because it holds a Unity AudioSource reference.
    /// EPIC 15.27 Phase 2.
    /// </summary>
    public class AudioSourceState : IComponentData
    {
        /// <summary>The pooled Unity AudioSource currently assigned to this entity.</summary>
        public AudioSource Source;

        /// <summary>The AudioLowPassFilter on the source (for occlusion).</summary>
        public AudioLowPassFilter LowPass;

        /// <summary>Current occlusion factor (0=fully occluded, 1=clear).</summary>
        public float OcclusionFactor = 1f;

        /// <summary>Target occlusion factor (lerped toward over time for smooth transitions).</summary>
        public float TargetOcclusionFactor = 1f;

        /// <summary>Frame counter for spread-scheduling occlusion raycasts.</summary>
        public int OcclusionFrameSlot;
    }
}
