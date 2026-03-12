using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Horror.Components;

namespace Horror
{
    /// <summary>
    /// MonoBehaviour that handles the actual playback of horror effects.
    /// Singleton that can be referenced by ECS systems.
    /// 
    /// Handles:
    /// - Light flickering (global or local)
    /// - Audio hallucinations (whispers, footsteps)
    /// - Visual distortions (post-processing)
    /// - Steam/vent bursts
    /// </summary>
    public class HorrorEventManager : MonoBehaviour
    {
        public static HorrorEventManager Instance { get; private set; }

        [Header("Audio")]
        [Tooltip("Audio clips for phantom footsteps")]
        public List<AudioClip> PhantomFootstepClips = new List<AudioClip>();
        
        [Tooltip("Audio clips for whispers")]
        public List<AudioClip> WhisperClips = new List<AudioClip>();
        
        [Tooltip("Audio clip for vent burst")]
        public AudioClip VentBurstClip;
        
        [Header("Audio Sources")]
        [Tooltip("Audio source for 2D/stereo effects")]
        public AudioSource StereoSource;
        
        [Tooltip("Audio source for 3D positioned effects")]
        public AudioSource SpatialSource;

        [Header("Lights")]
        [Tooltip("Lights that can flicker during horror events")]
        public List<Light> FlickerableLights = new List<Light>();
        
        [Tooltip("Include scene lights automatically")]
        public bool AutoFindLights = true;

        [Header("Visual Effects")]
        [Tooltip("Post-process volume for distortion effects")]
        public Volume DistortionVolume;
        
        [Tooltip("Particle system for vent bursts")]
        public ParticleSystem VentBurstVFX;

        [Header("Settings")]
        [Range(0f, 1f)]
        public float FootstepPanRange = 0.8f;
        
        [Range(0f, 1f)]
        public float WhisperVolume = 0.3f;

        private Dictionary<Light, float> _originalIntensities = new Dictionary<Light, float>();
        private Coroutine _flickerCoroutine;
        private Coroutine _distortionCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Create audio sources if not assigned
            if (StereoSource == null)
            {
                var go = new GameObject("HorrorStereoAudio");
                go.transform.SetParent(transform);
                StereoSource = go.AddComponent<AudioSource>();
                StereoSource.spatialBlend = 0f; // 2D
                StereoSource.playOnAwake = false;
            }
            
            if (SpatialSource == null)
            {
                var go = new GameObject("HorrorSpatialAudio");
                go.transform.SetParent(transform);
                SpatialSource = go.AddComponent<AudioSource>();
                SpatialSource.spatialBlend = 1f; // 3D
                SpatialSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            if (AutoFindLights)
            {
                FindAllLights();
            }
            
            // Store original intensities
            foreach (var light in FlickerableLights)
            {
                if (light != null)
                    _originalIntensities[light] = light.intensity;
            }
        }

        private void FindAllLights()
        {
            var allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var light in allLights)
            {
                if (!FlickerableLights.Contains(light))
                {
                    FlickerableLights.Add(light);
                }
            }
        }

        /// <summary>
        /// Process a horror event and trigger appropriate effects.
        /// </summary>
        public void ProcessEvent(HorrorEventType type, float intensity, float duration, Vector3 position, bool isPrivate)
        {
            switch (type)
            {
                case HorrorEventType.LightFlicker:
                    StartLightFlicker(intensity, duration);
                    break;
                    
                case HorrorEventType.PhantomFootsteps:
                    PlayPhantomFootsteps(intensity);
                    break;
                    
                case HorrorEventType.Whispers:
                    PlayWhispers(intensity, duration);
                    break;
                    
                case HorrorEventType.RadarGhost:
                    TriggerRadarGhost(duration);
                    break;
                    
                case HorrorEventType.VentBurst:
                    PlayVentBurst(position, intensity);
                    break;
                    
                case HorrorEventType.VisualDistortion:
                    StartVisualDistortion(intensity, duration);
                    break;
            }
        }

        #region Light Flicker

        private void StartLightFlicker(float intensity, float duration)
        {
            if (_flickerCoroutine != null)
                StopCoroutine(_flickerCoroutine);
            
            _flickerCoroutine = StartCoroutine(FlickerLightsCoroutine(intensity, duration));
        }

        private IEnumerator FlickerLightsCoroutine(float intensity, float duration)
        {
            float elapsed = 0f;
            int flickerCount = Mathf.CeilToInt(duration / 0.1f);
            
            for (int i = 0; i < flickerCount; i++)
            {
                // Dim or turn off lights
                float dimFactor = Random.Range(0f, 1f - intensity);
                foreach (var light in FlickerableLights)
                {
                    if (light != null && _originalIntensities.TryGetValue(light, out float original))
                    {
                        light.intensity = original * dimFactor;
                    }
                }
                
                yield return new WaitForSeconds(Random.Range(0.02f, 0.1f));
                
                // Restore briefly
                foreach (var light in FlickerableLights)
                {
                    if (light != null && _originalIntensities.TryGetValue(light, out float original))
                    {
                        light.intensity = original;
                    }
                }
                
                yield return new WaitForSeconds(Random.Range(0.01f, 0.05f));
                elapsed += 0.15f;
                
                if (elapsed >= duration) break;
            }
            
            // Ensure lights are restored
            RestoreLights();
            _flickerCoroutine = null;
        }

        private void RestoreLights()
        {
            foreach (var light in FlickerableLights)
            {
                if (light != null && _originalIntensities.TryGetValue(light, out float original))
                {
                    light.intensity = original;
                }
            }
        }

        #endregion

        #region Audio Effects

        private void PlayPhantomFootsteps(float intensity)
        {
            if (PhantomFootstepClips.Count == 0)
            {
                Debug.LogWarning("[Horror] No phantom footstep clips assigned");
                return;
            }
            
            var clip = PhantomFootstepClips[Random.Range(0, PhantomFootstepClips.Count)];
            
            // Pan to left or right (behind player)
            float pan = Random.Range(-FootstepPanRange, FootstepPanRange);
            
            StereoSource.panStereo = pan;
            StereoSource.volume = 0.3f + (intensity * 0.4f);
            StereoSource.PlayOneShot(clip);
            
            Debug.Log($"[Horror] Phantom footsteps (pan={pan:F2})");
        }

        private void PlayWhispers(float intensity, float duration)
        {
            if (WhisperClips.Count == 0)
            {
                Debug.LogWarning("[Horror] No whisper clips assigned");
                return;
            }
            
            StartCoroutine(WhisperCoroutine(intensity, duration));
        }

        private IEnumerator WhisperCoroutine(float intensity, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                var clip = WhisperClips[Random.Range(0, WhisperClips.Count)];
                
                // Random pan (surround effect)
                StereoSource.panStereo = Random.Range(-0.9f, 0.9f);
                StereoSource.volume = WhisperVolume * intensity;
                StereoSource.PlayOneShot(clip);
                
                // Wait between whispers
                float waitTime = Random.Range(0.5f, 1.5f);
                yield return new WaitForSeconds(waitTime);
                elapsed += waitTime;
            }
            
            Debug.Log($"[Horror] Whispers ended after {elapsed:F1}s");
        }

        private void PlayVentBurst(Vector3 position, float intensity)
        {
            if (VentBurstClip != null)
            {
                SpatialSource.transform.position = position;
                SpatialSource.volume = 0.5f + (intensity * 0.5f);
                SpatialSource.PlayOneShot(VentBurstClip);
            }
            
            if (VentBurstVFX != null)
            {
                VentBurstVFX.transform.position = position;
                VentBurstVFX.Play();
            }
            
            Debug.Log($"[Horror] Vent burst at {position}");
        }

        #endregion

        #region Visual Effects

        private void TriggerRadarGhost(float duration)
        {
            // TODO: Integrate with Motion Tracker system when available
            Debug.Log($"[Horror] Radar ghost for {duration:F1}s (Motion Tracker integration needed)");
        }

        private void StartVisualDistortion(float intensity, float duration)
        {
            if (DistortionVolume == null)
            {
                Debug.LogWarning("[Horror] No distortion volume assigned");
                return;
            }
            
            if (_distortionCoroutine != null)
                StopCoroutine(_distortionCoroutine);
            
            _distortionCoroutine = StartCoroutine(DistortionCoroutine(intensity, duration));
        }

        private IEnumerator DistortionCoroutine(float intensity, float duration)
        {
            DistortionVolume.weight = intensity;
            DistortionVolume.gameObject.SetActive(true);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Pulse the effect
                float pulse = Mathf.Sin(elapsed * 5f) * 0.3f + 0.7f;
                DistortionVolume.weight = intensity * pulse;
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Fade out
            float fadeTime = 0.3f;
            float startWeight = DistortionVolume.weight;
            elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                DistortionVolume.weight = Mathf.Lerp(startWeight, 0f, elapsed / fadeTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            DistortionVolume.weight = 0f;
            DistortionVolume.gameObject.SetActive(false);
            _distortionCoroutine = null;
            
            Debug.Log("[Horror] Visual distortion ended");
        }

        #endregion

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
