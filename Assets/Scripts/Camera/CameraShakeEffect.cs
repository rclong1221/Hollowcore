using UnityEngine;
using Unity.Mathematics;

namespace DIG.CameraSystem
{
    /// <summary>
    /// EPIC 14.9 Phase 5 - Camera Shake Effect
    /// Provides advanced screen shake functionality for the camera system.
    ///
    /// Features:
    /// - Multiple shake types (Perlin, Sine, Trauma-based)
    /// - Shake layering (multiple simultaneous shakes)
    /// - Direction-aware shakes (explosions, impacts)
    /// - Integration with CameraModeProvider
    ///
    /// Usage:
    /// - Attach to camera or call static methods via Instance
    /// - Call Shake() for simple shakes
    /// - Call ShakeFromDirection() for directional impacts
    /// - Call AddTrauma() for trauma-based accumulating shake
    /// </summary>
    public class CameraShakeEffect : MonoBehaviour
    {
        // ============================================================
        // SINGLETON
        // ============================================================

        private static CameraShakeEffect _instance;

        public static CameraShakeEffect Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<CameraShakeEffect>();
                }
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        // ============================================================
        // SETTINGS
        // ============================================================

        [Header("Shake Settings")]
        [Tooltip("Global shake intensity multiplier.")]
        [Range(0f, 2f)]
        [SerializeField] private float _globalMultiplier = 1f;

        [Tooltip("Maximum shake amplitude in units.")]
        [Range(0.01f, 2f)]
        [SerializeField] private float _maxAmplitude = 0.5f;

        [Tooltip("Base shake frequency (oscillations per second).")]
        [Range(1f, 100f)]
        [SerializeField] private float _baseFrequency = 25f;

        [Tooltip("How quickly shake decays (amplitude reduction per second).")]
        [Range(0.1f, 30f)]
        [SerializeField] private float _decayRate = 10f;

        [Header("Trauma Settings")]
        [Tooltip("Maximum trauma value (shake intensity at max trauma).")]
        [Range(0f, 1f)]
        [SerializeField] private float _maxTrauma = 1f;

        [Tooltip("Trauma decay rate per second.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float _traumaDecay = 1f;

        [Tooltip("Exponent for trauma-to-shake conversion (2 = quadratic, feels more natural).")]
        [Range(1f, 3f)]
        [SerializeField] private float _traumaExponent = 2f;

        [Header("Rotation Shake")]
        [Tooltip("Enable rotational shake (camera roll).")]
        [SerializeField] private bool _enableRotationShake = true;

        [Tooltip("Maximum rotation shake in degrees.")]
        [Range(0f, 10f)]
        [SerializeField] private float _maxRotationShake = 3f;

        // ============================================================
        // STATE
        // ============================================================

        // Current shake values
        private float _currentAmplitude;
        private float _currentDuration;
        private float _shakeTimer;
        private Vector3 _shakeDirection;
        private bool _isDirectionalShake;

        // Trauma system
        private float _currentTrauma;

        // Noise offsets for Perlin noise
        private float _noiseOffsetX;
        private float _noiseOffsetY;
        private float _noiseOffsetZ;
        private float _noiseOffsetRot;

        // Camera reference
        private Camera _camera;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _positionCaptured;

        // ============================================================
        // PROPERTIES
        // ============================================================

        /// <summary>
        /// Global shake intensity multiplier.
        /// </summary>
        public float GlobalMultiplier
        {
            get => _globalMultiplier;
            set => _globalMultiplier = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Current trauma value (0-1).
        /// </summary>
        public float CurrentTrauma => _currentTrauma;

        /// <summary>
        /// Whether any shake is currently active.
        /// </summary>
        public bool IsShaking => _currentAmplitude > 0.001f || _currentTrauma > 0.001f;

        /// <summary>
        /// Current effective shake intensity.
        /// </summary>
        public float CurrentShakeIntensity => Mathf.Max(_currentAmplitude, GetTraumaShake());

        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            // Random noise offsets for variety
            _noiseOffsetX = UnityEngine.Random.Range(0f, 1000f);
            _noiseOffsetY = UnityEngine.Random.Range(0f, 1000f);
            _noiseOffsetZ = UnityEngine.Random.Range(0f, 1000f);
            _noiseOffsetRot = UnityEngine.Random.Range(0f, 1000f);

            // Try to get camera
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            UpdateShake(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ============================================================
        // PUBLIC API - SIMPLE SHAKE
        // ============================================================

        /// <summary>
        /// Apply a simple screen shake.
        /// </summary>
        /// <param name="intensity">Shake intensity (0-1).</param>
        /// <param name="duration">Duration in seconds.</param>
        public void Shake(float intensity, float duration)
        {
            if (_globalMultiplier <= 0f) return;

            // Only override if new shake is stronger
            float scaledIntensity = intensity * _globalMultiplier * _maxAmplitude;
            if (scaledIntensity > _currentAmplitude)
            {
                _currentAmplitude = scaledIntensity;
                _currentDuration = duration;
                _shakeTimer = 0f;
                _isDirectionalShake = false;
            }
        }

        /// <summary>
        /// Apply a directional shake (e.g., from an explosion or impact).
        /// </summary>
        /// <param name="intensity">Shake intensity (0-1).</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="direction">World-space direction of the impact.</param>
        public void ShakeFromDirection(float intensity, float duration, Vector3 direction)
        {
            if (_globalMultiplier <= 0f) return;

            float scaledIntensity = intensity * _globalMultiplier * _maxAmplitude;
            if (scaledIntensity > _currentAmplitude)
            {
                _currentAmplitude = scaledIntensity;
                _currentDuration = duration;
                _shakeTimer = 0f;
                _shakeDirection = direction.normalized;
                _isDirectionalShake = true;
            }
        }

        /// <summary>
        /// Apply a shake from a world position (shake direction is from position to camera).
        /// </summary>
        /// <param name="intensity">Shake intensity (0-1).</param>
        /// <param name="duration">Duration in seconds.</param>
        /// <param name="worldPosition">World position of the impact source.</param>
        public void ShakeFromPosition(float intensity, float duration, Vector3 worldPosition)
        {
            if (_camera == null) return;

            Vector3 direction = _camera.transform.position - worldPosition;
            ShakeFromDirection(intensity, duration, direction);
        }

        /// <summary>
        /// Stop all current shakes immediately.
        /// </summary>
        public void StopShake()
        {
            _currentAmplitude = 0f;
            _currentDuration = 0f;
            _shakeTimer = 0f;
            _currentTrauma = 0f;
        }

        // ============================================================
        // PUBLIC API - TRAUMA SYSTEM
        // ============================================================

        /// <summary>
        /// Add trauma to the camera (accumulating shake).
        /// Trauma decays over time and creates more organic shake patterns.
        /// </summary>
        /// <param name="amount">Amount of trauma to add (0-1).</param>
        public void AddTrauma(float amount)
        {
            if (_globalMultiplier <= 0f) return;

            _currentTrauma = Mathf.Min(_maxTrauma, _currentTrauma + amount * _globalMultiplier);
        }

        /// <summary>
        /// Set trauma to a specific value.
        /// </summary>
        /// <param name="trauma">Trauma value (0-1).</param>
        public void SetTrauma(float trauma)
        {
            _currentTrauma = Mathf.Clamp(trauma, 0f, _maxTrauma);
        }

        /// <summary>
        /// Reset trauma to zero.
        /// </summary>
        public void ResetTrauma()
        {
            _currentTrauma = 0f;
        }

        // ============================================================
        // PUBLIC API - PRESET SHAKES
        // ============================================================

        /// <summary>
        /// Light shake for small impacts (footsteps, light hits).
        /// </summary>
        public void ShakeLight()
        {
            Shake(0.1f, 0.1f);
        }

        /// <summary>
        /// Medium shake for moderate impacts (melee hits, small explosions).
        /// </summary>
        public void ShakeMedium()
        {
            Shake(0.3f, 0.2f);
        }

        /// <summary>
        /// Heavy shake for large impacts (big explosions, boss attacks).
        /// </summary>
        public void ShakeHeavy()
        {
            Shake(0.6f, 0.35f);
        }

        /// <summary>
        /// Extreme shake for massive impacts (death, massive explosions).
        /// </summary>
        public void ShakeExtreme()
        {
            Shake(1f, 0.5f);
        }

        /// <summary>
        /// Continuous rumble shake (for ongoing effects like earthquakes).
        /// Call every frame to maintain the effect.
        /// </summary>
        /// <param name="intensity">Intensity (0-1).</param>
        public void Rumble(float intensity)
        {
            AddTrauma(intensity * Time.deltaTime * 2f);
        }

        // ============================================================
        // PRIVATE METHODS
        // ============================================================

        private void UpdateShake(float deltaTime)
        {
            if (_camera == null) return;

            // Decay trauma
            if (_currentTrauma > 0f)
            {
                _currentTrauma = Mathf.Max(0f, _currentTrauma - _traumaDecay * deltaTime);
            }

            // Check if any shake is active
            float traumaShake = GetTraumaShake();
            if (_currentAmplitude <= 0f && traumaShake <= 0f)
            {
                return;
            }

            // Update timer for amplitude-based shake
            if (_currentAmplitude > 0f)
            {
                _shakeTimer += deltaTime;
                float decayedAmplitude = Mathf.Max(0f, _currentAmplitude - _decayRate * deltaTime);

                // Also check duration
                if (_currentDuration > 0f && _shakeTimer >= _currentDuration)
                {
                    decayedAmplitude = 0f;
                }

                _currentAmplitude = decayedAmplitude;
            }

            // Calculate combined shake
            float totalIntensity = Mathf.Max(_currentAmplitude, traumaShake);
            Vector3 offset = CalculateShakeOffset(totalIntensity);

            // Apply offset to camera
            // Note: Most camera implementations apply shake in their own update,
            // so this is an additional layer for standalone use
            if (!_positionCaptured)
            {
                _originalPosition = _camera.transform.position;
                _originalRotation = _camera.transform.rotation;
                _positionCaptured = true;
            }

            // Apply position offset
            _camera.transform.position += offset;

            // Apply rotation shake
            if (_enableRotationShake && _maxRotationShake > 0f)
            {
                float rotationIntensity = totalIntensity / _maxAmplitude;
                float t = Time.time * _baseFrequency;
                float rotShake = (Mathf.PerlinNoise(t + _noiseOffsetRot, _noiseOffsetRot) * 2f - 1f)
                    * _maxRotationShake * rotationIntensity;

                _camera.transform.Rotate(Vector3.forward, rotShake, Space.Self);
            }
        }

        private float GetTraumaShake()
        {
            if (_currentTrauma <= 0f) return 0f;

            // Quadratic falloff makes shake feel more natural
            float shake = Mathf.Pow(_currentTrauma, _traumaExponent);
            return shake * _maxAmplitude;
        }

        private Vector3 CalculateShakeOffset(float intensity)
        {
            float t = Time.time * _baseFrequency;

            if (_isDirectionalShake && _shakeDirection.sqrMagnitude > 0.01f)
            {
                // Directional shake: mostly in the impact direction with some perpendicular motion
                float primary = (Mathf.PerlinNoise(t + _noiseOffsetX, _noiseOffsetY) * 2f - 1f) * intensity;
                float secondary = (Mathf.PerlinNoise(t + _noiseOffsetY, _noiseOffsetX) * 2f - 1f) * intensity * 0.3f;

                // Get perpendicular vector
                Vector3 perp = Vector3.Cross(_shakeDirection, Vector3.up);
                if (perp.sqrMagnitude < 0.01f)
                {
                    perp = Vector3.Cross(_shakeDirection, Vector3.right);
                }
                perp.Normalize();

                return _shakeDirection * primary + perp * secondary;
            }
            else
            {
                // Omnidirectional shake using Perlin noise
                float px = Mathf.PerlinNoise(t + _noiseOffsetX, _noiseOffsetY) * 2f - 1f;
                float py = Mathf.PerlinNoise(t + _noiseOffsetY, _noiseOffsetZ) * 2f - 1f;
                float pz = Mathf.PerlinNoise(t + _noiseOffsetZ, _noiseOffsetX) * 2f - 1f;

                return new Vector3(
                    px * intensity,
                    py * intensity * 0.6f, // Less vertical shake
                    pz * intensity * 0.4f  // Less forward/back shake
                );
            }
        }

        // ============================================================
        // STATIC CONVENIENCE METHODS
        // ============================================================

        /// <summary>
        /// Trigger a shake on the singleton instance.
        /// </summary>
        public static void TriggerShake(float intensity, float duration)
        {
            if (HasInstance)
            {
                Instance.Shake(intensity, duration);
            }
            else if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                // Fall back to camera mode's shake
                CameraModeProvider.Instance.ActiveCamera.Shake(intensity, duration);
            }
        }

        /// <summary>
        /// Trigger a directional shake on the singleton instance.
        /// </summary>
        public static void TriggerDirectionalShake(float intensity, float duration, Vector3 direction)
        {
            if (HasInstance)
            {
                Instance.ShakeFromDirection(intensity, duration, direction);
            }
            else if (CameraModeProvider.HasInstance && CameraModeProvider.Instance.ActiveCamera != null)
            {
                CameraModeProvider.Instance.ActiveCamera.Shake(intensity, duration);
            }
        }

        /// <summary>
        /// Add trauma to the singleton instance.
        /// </summary>
        public static void TriggerTrauma(float amount)
        {
            if (HasInstance)
            {
                Instance.AddTrauma(amount);
            }
        }
    }
}
