using System.Collections.Generic;
using UnityEngine;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// MonoBehaviour audio manager for airlock sounds.
    /// Assign audio clips in the inspector for each sound type.
    /// </summary>
    /// <remarks>
    /// Place this component in your scene (e.g., on a "Managers" GameObject).
    /// Assign AudioClips for each sound type, or leave null to use generated placeholder sounds.
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    public class AirlockAudioManager : MonoBehaviour
    {
        public static AirlockAudioManager Instance { get; private set; }

        [Header("Audio Pool")]
        [Tooltip("Number of pooled AudioSources")]
        public int PoolSize = 8;

        [Header("Cycle Sounds")]
        [Tooltip("Sound when airlock cycle begins (seal/lock)")]
        public List<AudioClip> CycleStartClips;

        [Tooltip("Sound when depressurizing (exiting to vacuum)")]
        public List<AudioClip> DepressurizeClips;

        [Tooltip("Sound when pressurizing (entering from vacuum)")]
        public List<AudioClip> PressurizeClips;

        [Tooltip("Ambient vent sound during cycling")]
        public List<AudioClip> VentClips;

        [Tooltip("Sound when cycle completes")]
        public List<AudioClip> CycleCompleteClips;

        [Header("Door Sounds")]
        [Tooltip("Door opening sound")]
        public List<AudioClip> DoorOpenClips;

        [Tooltip("Door closing sound")]
        public List<AudioClip> DoorCloseClips;

        [Header("Alert Sounds")]
        [Tooltip("Denied/error buzzer")]
        public List<AudioClip> DeniedClips;

        [Tooltip("Emergency alarm")]
        public List<AudioClip> EmergencyClips;

        [Header("Volume Settings")]
        [Range(0f, 1f)]
        public float MasterVolume = 1f;

        [Range(0f, 1f)]
        public float CycleVolume = 0.8f;

        [Range(0f, 1f)]
        public float DoorVolume = 0.6f;

        [Range(0f, 1f)]
        public float AlertVolume = 1f;

        [Header("3D Audio Settings")]
        public float MinDistance = 1f;
        public float MaxDistance = 30f;

        private Queue<AudioSource> _pool = new Queue<AudioSource>();
        private Dictionary<int, AudioClip> _generatedClips = new Dictionary<int, AudioClip>();

        void Awake()
        {
            Instance = this;
            InitializePool();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void InitializePool()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"AirlockAudioSrc_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1f; // 3D audio
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = MinDistance;
                src.maxDistance = MaxDistance;
                _pool.Enqueue(src);
            }
        }

        /// <summary>
        /// Play an airlock sound at a world position.
        /// </summary>
        public void PlaySound(AirlockSoundType soundType, Vector3 position, float volumeMultiplier = 1f)
        {
            var clips = GetClipsForType(soundType);
            float baseVolume = GetVolumeForType(soundType);

            AudioClip clip = null;
            if (clips != null && clips.Count > 0)
            {
                clip = clips[Random.Range(0, clips.Count)];
            }

            // Generate placeholder if no clip assigned
            if (clip == null)
            {
                clip = GetOrCreatePlaceholderClip(soundType);
            }

            if (clip == null) return;

            var src = GetSource();
            src.transform.position = position;
            src.volume = MasterVolume * baseVolume * volumeMultiplier;
            src.clip = clip;
            src.Play();

            StartCoroutine(ReturnWhenFinished(src));
        }

        /// <summary>
        /// Play a looping sound (returns the AudioSource for stopping).
        /// </summary>
        public AudioSource PlayLoopingSound(AirlockSoundType soundType, Vector3 position, float volumeMultiplier = 1f)
        {
            var clips = GetClipsForType(soundType);
            float baseVolume = GetVolumeForType(soundType);

            AudioClip clip = null;
            if (clips != null && clips.Count > 0)
            {
                clip = clips[Random.Range(0, clips.Count)];
            }

            if (clip == null)
            {
                clip = GetOrCreatePlaceholderClip(soundType);
            }

            if (clip == null) return null;

            var src = GetSource();
            src.transform.position = position;
            src.volume = MasterVolume * baseVolume * volumeMultiplier;
            src.clip = clip;
            src.loop = true;
            src.Play();

            return src;
        }

        /// <summary>
        /// Stop a looping sound and return its AudioSource to the pool.
        /// </summary>
        public void StopLoopingSound(AudioSource src)
        {
            if (src == null) return;
            src.Stop();
            src.loop = false;
            _pool.Enqueue(src);
        }

        private List<AudioClip> GetClipsForType(AirlockSoundType soundType)
        {
            return soundType switch
            {
                AirlockSoundType.CycleStart => CycleStartClips,
                AirlockSoundType.Depressurize => DepressurizeClips,
                AirlockSoundType.Pressurize => PressurizeClips,
                AirlockSoundType.Vent => VentClips,
                AirlockSoundType.CycleComplete => CycleCompleteClips,
                AirlockSoundType.DoorOpen => DoorOpenClips,
                AirlockSoundType.DoorClose => DoorCloseClips,
                AirlockSoundType.Denied => DeniedClips,
                AirlockSoundType.Emergency => EmergencyClips,
                _ => null
            };
        }

        private float GetVolumeForType(AirlockSoundType soundType)
        {
            return soundType switch
            {
                AirlockSoundType.CycleStart => CycleVolume,
                AirlockSoundType.Depressurize => CycleVolume,
                AirlockSoundType.Pressurize => CycleVolume,
                AirlockSoundType.Vent => CycleVolume * 0.5f,
                AirlockSoundType.CycleComplete => CycleVolume,
                AirlockSoundType.DoorOpen => DoorVolume,
                AirlockSoundType.DoorClose => DoorVolume,
                AirlockSoundType.Denied => AlertVolume,
                AirlockSoundType.Emergency => AlertVolume,
                _ => 1f
            };
        }

        private AudioClip GetOrCreatePlaceholderClip(AirlockSoundType soundType)
        {
            int key = (int)soundType;
            if (_generatedClips.TryGetValue(key, out var existing))
            {
                return existing;
            }

            // Generate different placeholder sounds for each type
            float frequency;
            float duration;
            
            switch (soundType)
            {
                case AirlockSoundType.CycleStart:
                    frequency = 300f;
                    duration = 0.5f;
                    break;
                case AirlockSoundType.Depressurize:
                    frequency = 150f; // Low hiss
                    duration = 1.5f;
                    break;
                case AirlockSoundType.Pressurize:
                    frequency = 200f;
                    duration = 1.5f;
                    break;
                case AirlockSoundType.Vent:
                    frequency = 100f; // Very low rumble
                    duration = 0.3f;
                    break;
                case AirlockSoundType.CycleComplete:
                    frequency = 800f; // High chime
                    duration = 0.3f;
                    break;
                case AirlockSoundType.DoorOpen:
                    frequency = 400f;
                    duration = 0.5f;
                    break;
                case AirlockSoundType.DoorClose:
                    frequency = 350f;
                    duration = 0.4f;
                    break;
                case AirlockSoundType.Denied:
                    frequency = 250f; // Buzzer
                    duration = 0.3f;
                    break;
                case AirlockSoundType.Emergency:
                    frequency = 600f; // Alarm
                    duration = 0.8f;
                    break;
                default:
                    frequency = 440f;
                    duration = 0.3f;
                    break;
            }

            var clip = GenerateWaveClip(soundType, frequency, duration);
            _generatedClips[key] = clip;
            return clip;
        }

        private AudioClip GenerateWaveClip(AirlockSoundType type, float frequency, float durationSeconds)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * durationSeconds);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 1f - (t / durationSeconds); // Fade out

                // Different waveforms for different sound types
                float sample;
                switch (type)
                {
                    case AirlockSoundType.Depressurize:
                    case AirlockSoundType.Pressurize:
                        // White noise mixed with low frequency for hiss
                        float noise = Random.Range(-1f, 1f);
                        sample = (Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.3f + noise * 0.7f) * envelope;
                        break;
                        
                    case AirlockSoundType.Vent:
                        // Rumble
                        sample = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.8f;
                        sample += Random.Range(-0.2f, 0.2f) * envelope;
                        break;
                        
                    case AirlockSoundType.Denied:
                        // Harsh buzzer (square-ish wave)
                        sample = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t)) * 0.5f * envelope;
                        break;
                        
                    case AirlockSoundType.Emergency:
                        // Alternating two-tone alarm
                        float altFreq = (Mathf.Sin(2f * Mathf.PI * 3f * t) > 0) ? frequency : frequency * 0.8f;
                        sample = Mathf.Sin(2f * Mathf.PI * altFreq * t) * envelope;
                        break;
                        
                    default:
                        // Simple sine wave
                        sample = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
                        break;
                }

                data[i] = sample * 0.6f; // Master volume
            }

            var clip = AudioClip.Create($"airlock_{type}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioSource GetSource()
        {
            if (_pool.Count > 0)
            {
                return _pool.Dequeue();
            }

            // Pool exhausted - create temporary source
            var go = new GameObject("AirlockAudioTmp");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.playOnAwake = false;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = MinDistance;
            src.maxDistance = MaxDistance;
            return src;
        }

        System.Collections.IEnumerator ReturnWhenFinished(AudioSource src)
        {
            yield return new WaitUntil(() => !src.isPlaying);
            
            if (src.gameObject.name.StartsWith("AirlockAudioTmp"))
            {
                Destroy(src.gameObject);
            }
            else
            {
                _pool.Enqueue(src);
            }
        }
    }
}
