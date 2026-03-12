using System;
using UnityEngine;
using Audio.Config;

namespace Audio.Events
{
    public enum ClipSelection : byte
    {
        Random = 0,
        Sequential = 1,
        Shuffle = 2,
        RandomNoRepeat = 3
    }

    [Serializable]
    public struct RangeFloat
    {
        public float Min;
        public float Max;

        public RangeFloat(float min, float max) { Min = min; Max = max; }

        public float Evaluate()
        {
            return Min >= Max ? Min : UnityEngine.Random.Range(Min, Max);
        }
    }

    [CreateAssetMenu(fileName = "AudioEvent", menuName = "DIG/Audio/Audio Event")]
    public class AudioEventSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique identifier for this event (auto-generated from asset name if empty).")]
        public string EventId;

        [Header("Clips")]
        [Tooltip("Clip variations. One is selected per play based on SelectionMode.")]
        public AudioClip[] Clips = Array.Empty<AudioClip>();

        [Tooltip("How clips are chosen from the array.")]
        public ClipSelection SelectionMode = ClipSelection.Random;

        [Header("Volume & Pitch")]
        [Tooltip("Random volume range applied per play.")]
        public RangeFloat Volume = new RangeFloat(0.8f, 1.0f);

        [Tooltip("Random pitch range applied per play.")]
        public RangeFloat Pitch = new RangeFloat(0.95f, 1.05f);

        [Header("Bus & Priority")]
        [Tooltip("Audio bus for mixer routing.")]
        public AudioBusType Bus = AudioBusType.Combat;

        [Tooltip("Pool priority (0=lowest, 255=highest). Higher priority sources survive eviction.")]
        [Range(0, 255)]
        public int Priority = 128;

        [Header("Cooldown & Instances")]
        [Tooltip("Minimum seconds between successive plays of this event. 0 = no cooldown.")]
        [Min(0)]
        public float Cooldown;

        [Tooltip("Maximum concurrent instances of this event. 0 = unlimited.")]
        [Min(0)]
        public int MaxInstances;

        [Header("Spatial")]
        [Tooltip("0 = 2D (UI/music), 1 = fully 3D positional.")]
        [Range(0f, 1f)]
        public float SpatialBlend = 1f;

        [Tooltip("Distance below which volume is at full.")]
        [Min(0.01f)]
        public float MinDistance = 1f;

        [Tooltip("Distance beyond which the source is inaudible.")]
        [Min(0.1f)]
        public float MaxDistance = 50f;

        [Tooltip("Distance rolloff mode.")]
        public AudioRolloffMode RolloffMode = AudioRolloffMode.Logarithmic;

        [Tooltip("Custom falloff curve (only used when RolloffMode is Custom).")]
        public AnimationCurve CustomRolloff;

        [Header("Looping & Fading")]
        public bool Loop;

        [Tooltip("Fade-in duration in seconds. 0 = instant.")]
        [Min(0)]
        public float FadeIn;

        [Tooltip("Fade-out duration in seconds. 0 = instant.")]
        [Min(0)]
        public float FadeOut;

        [Header("Environment")]
        [Tooltip("Apply occlusion filtering to this source.")]
        public bool OcclusionEnabled = true;

        [Tooltip("Reverb zone send level.")]
        [Range(0f, 1f)]
        public float ReverbSend = 0.5f;

        /// <summary>
        /// Convenience: play at a 3D world position.
        /// Routes through AudioEventService singleton.
        /// </summary>
        public AudioEventHandle Play(Vector3 position)
        {
            return AudioEventService.Instance != null
                ? AudioEventService.Instance.Play(this, position)
                : AudioEventHandle.Invalid;
        }

        /// <summary>
        /// Convenience: play as 2D (non-positional, e.g. UI or music stinger).
        /// </summary>
        public AudioEventHandle Play2D()
        {
            return AudioEventService.Instance != null
                ? AudioEventService.Instance.Play2D(this)
                : AudioEventHandle.Invalid;
        }

        /// <summary>
        /// Convenience: play attached to a transform (follows it).
        /// </summary>
        public AudioEventHandle PlayAttached(Transform parent)
        {
            return AudioEventService.Instance != null
                ? AudioEventService.Instance.PlayAttached(this, parent)
                : AudioEventHandle.Invalid;
        }

        /// <summary>
        /// Stop a playing instance of this event.
        /// </summary>
        public void Stop(AudioEventHandle handle, float fadeOut = 0f)
        {
            if (AudioEventService.Instance != null)
                AudioEventService.Instance.Stop(handle, fadeOut);
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(EventId))
                EventId = name;
        }
    }
}
