using UnityEngine;

namespace Player.Bridges
{
    /// <summary>
    /// Client-side bridge for collision audio (Epic 7.4.4).
    /// Attach to the player presentation prefab (ghost presentation GameObject).
    /// </summary>
    public class CollisionAudioBridge : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("3D audio source used for collision sounds")]
        public AudioSource CollisionAudioSource;

        [Header("Clips")]
        public AudioClip[] BumpSounds;
        public AudioClip[] ImpactSounds;
        public AudioClip[] GruntSounds;
        public AudioClip[] SurprisedSounds;
        public AudioClip[] EvadeSounds;

        [Header("Tuning")]
        [Range(0f, 1f)]
        public float MinVolume = 0.15f;

        [Range(0f, 1f)]
        public float MaxVolume = 1.0f;

        [Range(0.5f, 2f)]
        public float MinPitch = 0.95f;

        [Range(0.5f, 2f)]
        public float MaxPitch = 1.05f;

        [Header("Debug")]
        public bool DebugLogging = false;

        private readonly AudioClip[] _fallbackBeeps = new AudioClip[4];

        private void Awake()
        {
            if (CollisionAudioSource == null)
            {
                CollisionAudioSource = GetComponentInChildren<AudioSource>();
            }

            if (CollisionAudioSource == null)
            {
                CollisionAudioSource = gameObject.AddComponent<AudioSource>();
                CollisionAudioSource.spatialBlend = 1.0f;
                CollisionAudioSource.playOnAwake = false;
            }

            CollisionAudioSource.spatialBlend = 1.0f;
            CollisionAudioSource.playOnAwake = false;
        }

        /// <summary>
        /// Plays an appropriate collision sound.
        /// hitDirection: 0=front/braced, 1=side, 2=back, 3=evaded
        /// </summary>
        public void PlayCollisionSound(float intensity, int hitDirection)
        {
            if (CollisionAudioSource == null)
                return;

            intensity = Mathf.Clamp01(intensity);

            var clips = SelectClipSet(intensity, hitDirection);
            AudioClip clip = null;

            if (clips != null && clips.Length > 0)
            {
                clip = clips[Random.Range(0, clips.Length)];
            }

            // QA-friendly fallback: beep if no designer clips wired.
            if (clip == null)
            {
                clip = GetFallbackBeep(hitDirection);
            }

            CollisionAudioSource.volume = Mathf.Lerp(MinVolume, MaxVolume, intensity);
            CollisionAudioSource.pitch = Random.Range(MinPitch, MaxPitch);
            CollisionAudioSource.PlayOneShot(clip);

            if (DebugLogging)
            {
                Debug.Log($"[CollisionAudioBridge] PlayCollisionSound intensity={intensity:F2} hitDir={hitDirection}");
            }
        }

        private AudioClip[] SelectClipSet(float intensity, int hitDirection)
        {
            // Evaded gets its own category.
            if (hitDirection == 3)
                return EvadeSounds;

            // Heavy impacts prefer impact set.
            if (intensity >= 0.75f && ImpactSounds != null && ImpactSounds.Length > 0)
                return ImpactSounds;

            // Directional flavor.
            if (hitDirection == 2)
                return SurprisedSounds;
            if (hitDirection == 0)
                return GruntSounds;

            return BumpSounds;
        }

        private AudioClip GenerateBeepClip(float frequency, float durationSeconds)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * durationSeconds);
            var data = new float[samples];
            for (int i = 0; i < samples; ++i)
            {
                float t = i / (float)sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.5f;
            }

            var clip = AudioClip.Create($"collision_beep_{frequency:F0}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GetFallbackBeep(int hitDirection)
        {
            int idx = Mathf.Clamp(hitDirection, 0, _fallbackBeeps.Length - 1);
            var cached = _fallbackBeeps[idx];
            if (cached != null)
                return cached;

            // Keep this deterministic and cheap; intensity is already communicated via volume/pitch.
            float frequency = 600f + idx * 140f;
            float durationSeconds = 0.14f;
            cached = GenerateBeepClip(frequency, durationSeconds);
            _fallbackBeeps[idx] = cached;
            return cached;
        }
    }
}
