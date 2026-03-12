using System.Collections.Generic;
using UnityEngine;
using Audio.Config;

namespace Audio.Systems
{
    /// <summary>
    /// Simple AudioManager that resolves SurfaceMaterial by id and plays a random clip for footsteps/landings.
    /// This is a minimal reference implementation; it uses a small pool of AudioSources.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AudioManager : MonoBehaviour
    {
        public SurfaceMaterialRegistry Registry;
        public int PoolSize = 8;
        public VFXManager VFXManager;
        
        [Header("Mixer Control")]
        public UnityEngine.Audio.AudioMixer MasterMixer;
        public string VacuumCutoffParam = "VacuumCutoff";
        public string VacuumVolumeParam = "VacuumVolume";

        [Header("Weather Audio (EPIC 17.8)")]
        public string RainVolumeParam = "RainVolume";
        public string WindVolumeParam = "WindVolume";
        public string ThunderVolumeParam = "ThunderVolume";
        public string AmbientNightVolumeParam = "AmbientNightVolume";

        private readonly Queue<AudioSource> _legacyPool = new Queue<AudioSource>();
        private readonly System.Collections.Generic.Dictionary<int, AudioClip> _generatedClips = new System.Collections.Generic.Dictionary<int, AudioClip>();

        // Audio variance
        [Header("Audio Variance")]
        [Tooltip("Random pitch range applied to impact sounds (± this value around 1.0)")]
        [Range(0f, 0.3f)]
        public float PitchVariance = 0.1f;

        [Tooltip("Random volume variation (± this fraction of base volume)")]
        [Range(0f, 0.2f)]
        public float VolumeVariance = 0.05f;

        private readonly Dictionary<int, int> _lastClipIndices = new Dictionary<int, int>();

        // EPIC 15.27: Track active pool sources for return
        private readonly List<AudioSourcePool.PooledSource> _activePoolSources = new List<AudioSourcePool.PooledSource>();

        void Awake()
        {
            // Legacy pool fallback (used when AudioSourcePool singleton not yet available)
            for (int i = 0; i < PoolSize; ++i)
            {
                var go = new GameObject("AudioPoolSrc");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 1.0f;
                src.playOnAwake = false;
                _legacyPool.Enqueue(src);
            }
        }

        public void PlayFootstep(int materialId, Vector3 pos, int stance)
        {
            if (Registry == null) return;
            if (!Registry.TryGetById(materialId, out var mat) || mat == null)
            {
                // fallback to default material if available
                mat = Registry.DefaultMaterial;
                if (mat == null) return;
            }

            var list = mat.WalkClips;
            if (stance == 3) list = mat.RunClips;
            if (stance == 1) list = mat.CrouchClips;

            AudioClip clip = null;
            if (list != null && list.Count > 0)
            {
                clip = list[Random.Range(0, list.Count)];
            }

            // If no designer-provided clip, generate a short beep so QA can confirm audio pipeline
            if (clip == null)
            {
                if (!_generatedClips.TryGetValue(materialId, out clip))
                {
                    clip = GenerateBeepClip(600f + (materialId % 5) * 120f, 0.22f);
                    _generatedClips[materialId] = clip;
                }
            }

            if (clip == null) return;

            var src = GetSource(AudioBusType.Footstep, 50, pos);
            src.transform.position = pos;
            src.volume = mat.FootstepVolume;
            src.clip = clip;
            src.Play();
            StartCoroutine(ReturnWhenFinished(src));

            // Spawn VFX if present
            if (VFXManager == null)
            {
                VFXManager = Object.FindAnyObjectByType<VFXManager>();
            }
            if (VFXManager != null && mat.VFXPrefab != null)
            {
                VFXManager.PlayVFX(mat.VFXPrefab, pos);
            }
        }

        public void PlayJump(int materialId, Vector3 pos, float intensity)
        {
            PlayActionSound(materialId, pos, intensity, mat => mat.JumpClips, "jump");
        }

        public void PlayRoll(int materialId, Vector3 pos, float intensity)
        {
            PlayActionSound(materialId, pos, intensity, mat => mat.RollClips, "roll");
        }

        public void PlayDive(int materialId, Vector3 pos, float intensity)
        {
            PlayActionSound(materialId, pos, intensity, mat => mat.DiveClips, "dive");
        }

        public void PlayClimb(int materialId, Vector3 pos)
        {
            PlayActionSound(materialId, pos, 1.0f, mat => mat.ClimbClips, "climb");
        }

        public void PlaySlide(int materialId, Vector3 pos, float intensity)
        {
            PlayActionSound(materialId, pos, intensity, mat => mat.SlideClips, "slide");
        }

        public void PlayImpact(int materialId, Vector3 pos, float intensity)
        {
            PlayActionSound(materialId, pos, intensity, mat => mat.ImpactClips, "impact");
        }

        private void PlayActionSound(int materialId, Vector3 pos, float intensity, System.Func<SurfaceMaterial, List<AudioClip>> clipSelector, string actionName)
        {
            if (Registry == null) return;
            if (!Registry.TryGetById(materialId, out var mat) || mat == null)
            {
                mat = Registry.DefaultMaterial;
                if (mat == null) return;
            }

            var list = clipSelector(mat);
            AudioClip clip = null;
            if (list != null && list.Count > 0)
            {
                // EPIC 15.24: No-repeat — avoid playing the same clip twice in a row
                int noRepeatKey = materialId * 1000 + actionName.GetHashCode();
                int clipIndex = Random.Range(0, list.Count);
                if (list.Count > 1 && _lastClipIndices.TryGetValue(noRepeatKey, out int lastIndex) && clipIndex == lastIndex)
                {
                    clipIndex = (clipIndex + 1) % list.Count;
                }
                _lastClipIndices[noRepeatKey] = clipIndex;
                clip = list[clipIndex];
            }

            // If no designer-provided clip, generate a short beep for QA
            if (clip == null)
            {
                int key = materialId * 1000 + actionName.GetHashCode();
                if (!_generatedClips.TryGetValue(key, out clip))
                {
                    clip = GenerateBeepClip(800f + (key % 7) * 100f, 0.15f);
                    _generatedClips[key] = clip;
                }
            }

            if (clip == null) return;

            // EPIC 15.27: Route to appropriate bus
            AudioBusType bus = (actionName == "impact") ? AudioBusType.Combat : AudioBusType.Footstep;
            byte priority = (byte)((actionName == "impact") ? 80 : 50);

            var src = GetSource(bus, priority, pos);
            src.transform.position = pos;
            // EPIC 15.24: Volume and pitch variance
            float volumeVar = 1f + Random.Range(-VolumeVariance, VolumeVariance);
            src.volume = mat.FootstepVolume * intensity * volumeVar;
            src.pitch = 1f + Random.Range(-PitchVariance, PitchVariance);
            src.clip = clip;
            src.Play();
            StartCoroutine(ReturnWhenFinished(src));

            // Spawn VFX if present
            if (VFXManager == null)
            {
                VFXManager = Object.FindAnyObjectByType<VFXManager>();
            }
            if (VFXManager != null && mat.VFXPrefab != null)
            {
                VFXManager.PlayVFX(mat.VFXPrefab, pos);
            }
        }

        /// <summary>
        /// Sets a weather-related AudioMixer exposed parameter volume (dB).
        /// Called by WeatherAudioSystem.
        /// </summary>
        public void SetWeatherVolume(string paramName, float dB)
        {
            if (MasterMixer == null) return;
            MasterMixer.SetFloat(paramName, dB);
        }

        /// <summary>
        /// Acquire an AudioSource from AudioSourcePool if available, otherwise legacy pool.
        /// Bus-aware source acquisition.
        /// </summary>
        private AudioSource GetSource(AudioBusType bus = AudioBusType.Footstep, byte priority = 50, Vector3 position = default)
        {
            if (AudioSourcePool.Instance != null)
            {
                var pooled = AudioSourcePool.Instance.Acquire(bus, priority, position);
                _activePoolSources.Add(pooled);
                return pooled.Source;
            }

            // Legacy fallback
            if (_legacyPool.Count > 0) return _legacyPool.Dequeue();
            var go = new GameObject("AudioTempSrc");
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1.0f;
            src.playOnAwake = false;
            return src;
        }

        private AudioClip GenerateBeepClip(float frequency, float durationSeconds)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * durationSeconds);
            var data = new float[samples];
            for (int i = 0; i < samples; ++i)
            {
                float t = i / (float)sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.6f;
            }

            var clip = AudioClip.Create($"beep_{frequency}", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        System.Collections.IEnumerator ReturnWhenFinished(AudioSource src)
        {
            yield return new WaitUntil(() => !src.isPlaying);

            // EPIC 15.27: Return to AudioSourcePool if managed by it
            if (AudioSourcePool.Instance != null)
            {
                for (int i = _activePoolSources.Count - 1; i >= 0; i--)
                {
                    if (_activePoolSources[i].Source == src)
                    {
                        AudioSourcePool.Instance.Release(_activePoolSources[i]);
                        _activePoolSources.RemoveAt(i);
                        yield break;
                    }
                }
            }

            // Legacy fallback
            if (src.gameObject.name == "AudioTempSrc")
            {
                Destroy(src.gameObject);
            }
            else
            {
                src.pitch = 1f;
                _legacyPool.Enqueue(src);
            }
        }
    }
}
