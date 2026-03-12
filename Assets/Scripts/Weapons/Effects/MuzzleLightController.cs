using UnityEngine;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Controls muzzle flash light behavior.
    /// Attach to muzzle flash prefab for automatic light flash on fire.
    /// </summary>
    public class MuzzleLightController : MonoBehaviour
    {
        [Header("Light Settings")]
        [Tooltip("Color of the muzzle flash light")]
        [SerializeField] private Color lightColor = new Color(1f, 0.8f, 0.4f);

        [Tooltip("Maximum light intensity")]
        [SerializeField] private float maxIntensity = 4f;

        [Tooltip("Light range in meters")]
        [SerializeField] private float lightRange = 6f;

        [Tooltip("Duration of the light flash")]
        [SerializeField] private float flashDuration = 0.05f;

        [Header("Animation")]
        [Tooltip("Use smooth fade or instant off")]
        [SerializeField] private bool smoothFade = true;

        [Tooltip("Animation curve for light fade (if smooth)")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private Light _light;
        private float _elapsedTime;
        private bool _isFlashing;

        private void Awake()
        {
            _light = GetComponent<Light>();
            if (_light == null)
            {
                _light = gameObject.AddComponent<Light>();
            }

            _light.type = LightType.Point;
            _light.color = lightColor;
            _light.intensity = 0f;
            _light.range = lightRange;
            _light.renderMode = LightRenderMode.Auto;
        }

        private void OnEnable()
        {
            // Start flash when enabled (spawned)
            StartFlash();
        }

        private void Update()
        {
            if (!_isFlashing) return;

            _elapsedTime += Time.deltaTime;
            float progress = _elapsedTime / flashDuration;

            if (progress >= 1f)
            {
                _light.intensity = 0f;
                _isFlashing = false;
                return;
            }

            if (smoothFade)
            {
                _light.intensity = maxIntensity * fadeCurve.Evaluate(progress);
            }
            else
            {
                _light.intensity = progress < 0.5f ? maxIntensity : 0f;
            }
        }

        public void StartFlash()
        {
            _elapsedTime = 0f;
            _isFlashing = true;
            _light.intensity = maxIntensity;
        }
    }
}
