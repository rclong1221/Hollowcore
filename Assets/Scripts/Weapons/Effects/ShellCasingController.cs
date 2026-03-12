using UnityEngine;
using DIG.Weapons.Audio;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Controls shell casing physics and audio.
    /// Attach to shell casing prefab for realistic ejection behavior.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShellCasingController : MonoBehaviour
    {
        [Header("Physics Settings")]
        [Tooltip("Initial ejection force")]
        [SerializeField] private float ejectionForce = 3f;

        [Tooltip("Random force variation")]
        [SerializeField] private float forceVariation = 0.5f;

        [Tooltip("Initial spin torque")]
        [SerializeField] private float spinTorque = 10f;

        [Tooltip("Random spin variation")]
        [SerializeField] private float spinVariation = 5f;

        [Header("Audio Settings")]
        [Tooltip("Play bounce sound when hitting surfaces")]
        [SerializeField] private bool playBounceSound = true;

        [Tooltip("Minimum velocity to trigger bounce sound")]
        [SerializeField] private float minBounceVelocity = 0.5f;

        [Tooltip("Maximum bounces that play sound")]
        [SerializeField] private int maxBounceSounds = 3;

        [Tooltip("Cooldown between bounce sounds")]
        [SerializeField] private float bounceSoundCooldown = 0.1f;

        [Header("Lifetime")]
        [Tooltip("Time before shell disappears")]
        [SerializeField] private float lifetime = 5f;

        [Tooltip("Fade out before destroying")]
        [SerializeField] private bool fadeOut = true;

        [Tooltip("Fade duration")]
        [SerializeField] private float fadeDuration = 1f;

        private Rigidbody _rigidbody;
        private Renderer _renderer;
        private int _bounceCount;
        private float _lastBounceTime;
        private float _elapsedTime;
        private bool _isFading;
        private Color _originalColor;
        private Material _material;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _renderer = GetComponent<Renderer>();

            // Configure rigidbody
            _rigidbody.mass = 0.01f; // Light shell
            _rigidbody.linearDamping = 0.5f;
            _rigidbody.angularDamping = 0.5f;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (_renderer != null)
            {
                _material = _renderer.material;
                _originalColor = _material.color;
            }
        }

        private void OnEnable()
        {
            // Reset state when spawned from pool
            _bounceCount = 0;
            _elapsedTime = 0f;
            _isFading = false;

            if (_material != null)
            {
                _material.color = _originalColor;
            }

            // Apply initial ejection force
            ApplyEjectionForce();
        }

        private void ApplyEjectionForce()
        {
            if (_rigidbody == null) return;

            // Reset velocity
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            // Random force variation
            float force = ejectionForce + Random.Range(-forceVariation, forceVariation);
            Vector3 ejectionDir = transform.right + Vector3.up * 0.5f;
            ejectionDir += new Vector3(
                Random.Range(-0.2f, 0.2f),
                Random.Range(0f, 0.3f),
                Random.Range(-0.2f, 0.2f)
            );
            ejectionDir.Normalize();

            _rigidbody.AddForce(ejectionDir * force, ForceMode.Impulse);

            // Random spin
            Vector3 torque = new Vector3(
                Random.Range(-spinTorque, spinTorque),
                Random.Range(-spinTorque, spinTorque),
                Random.Range(-spinTorque, spinTorque)
            );
            torque += new Vector3(
                Random.Range(-spinVariation, spinVariation),
                Random.Range(-spinVariation, spinVariation),
                Random.Range(-spinVariation, spinVariation)
            );

            _rigidbody.AddTorque(torque, ForceMode.Impulse);
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            // Start fading
            if (fadeOut && !_isFading && _elapsedTime >= lifetime - fadeDuration)
            {
                _isFading = true;
            }

            // Apply fade
            if (_isFading && _material != null)
            {
                float fadeProgress = (_elapsedTime - (lifetime - fadeDuration)) / fadeDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);

                Color fadedColor = _originalColor;
                fadedColor.a = Mathf.Lerp(_originalColor.a, 0f, fadeProgress);
                _material.color = fadedColor;
            }

            // Destroy after lifetime (handled by pool manager, but safety fallback)
            if (_elapsedTime >= lifetime)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!playBounceSound) return;
            if (_bounceCount >= maxBounceSounds) return;
            if (Time.time - _lastBounceTime < bounceSoundCooldown) return;

            float velocity = collision.relativeVelocity.magnitude;
            if (velocity < minBounceVelocity) return;

            _bounceCount++;
            _lastBounceTime = Time.time;

            // Play bounce sound via audio manager
            if (WeaponAudioManager.Instance != null)
            {
                // Detect surface for appropriate sound
                SurfaceMaterialType surface = SurfaceMaterialType.Default;
                var surfaceTag = collision.collider.GetComponent<SurfaceMaterialTag>();
                if (surfaceTag != null)
                {
                    surface = surfaceTag.MaterialType;
                }

                // Volume based on impact velocity
                float volumeScale = Mathf.Clamp01(velocity / 3f);

                WeaponAudioManager.Instance.PlayImpactSound(surface, collision.contacts[0].point, volumeScale);
            }
        }

        private void OnDisable()
        {
            // Reset for pooling
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }
    }
}
