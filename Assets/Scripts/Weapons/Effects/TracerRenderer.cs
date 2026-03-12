using UnityEngine;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Renders tracer rounds using LineRenderer or Trail.
    /// Attach to tracer prefab for automatic rendering.
    /// </summary>
    public class TracerRenderer : MonoBehaviour
    {
        [Header("Rendering Mode")]
        [SerializeField] private TracerRenderMode renderMode = TracerRenderMode.LineRenderer;

        [Header("Line Renderer Settings")]
        [SerializeField] private float lineWidth = 0.03f;
        [SerializeField] private float lineLength = 3f;
        [SerializeField] private Color startColor = new Color(1f, 0.9f, 0.5f, 1f);
        [SerializeField] private Color endColor = new Color(1f, 0.5f, 0.2f, 0f);
        [SerializeField] private Material tracerMaterial;

        [Header("Trail Settings")]
        [SerializeField] private float trailTime = 0.1f;
        [SerializeField] private float trailWidth = 0.02f;

        [Header("Light Settings")]
        [SerializeField] private bool emitLight = true;
        [SerializeField] private Color lightColor = new Color(1f, 0.8f, 0.4f);
        [SerializeField] private float lightIntensity = 1f;
        [SerializeField] private float lightRange = 2f;

        private LineRenderer _lineRenderer;
        private TrailRenderer _trailRenderer;
        private Light _light;

        private void Awake()
        {
            SetupRenderer();

            if (emitLight)
            {
                SetupLight();
            }
        }

        private void SetupRenderer()
        {
            switch (renderMode)
            {
                case TracerRenderMode.LineRenderer:
                    SetupLineRenderer();
                    break;
                case TracerRenderMode.TrailRenderer:
                    SetupTrailRenderer();
                    break;
            }
        }

        private void SetupLineRenderer()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth * 0.5f;
            _lineRenderer.startColor = startColor;
            _lineRenderer.endColor = endColor;
            _lineRenderer.useWorldSpace = true;

            if (tracerMaterial != null)
            {
                _lineRenderer.material = tracerMaterial;
            }
            else
            {
                // Create default additive material
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            // Set gradient
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(endColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            _lineRenderer.colorGradient = gradient;
        }

        private void SetupTrailRenderer()
        {
            _trailRenderer = GetComponent<TrailRenderer>();
            if (_trailRenderer == null)
            {
                _trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            _trailRenderer.time = trailTime;
            _trailRenderer.startWidth = trailWidth;
            _trailRenderer.endWidth = 0f;
            _trailRenderer.startColor = startColor;
            _trailRenderer.endColor = endColor;

            if (tracerMaterial != null)
            {
                _trailRenderer.material = tracerMaterial;
            }
        }

        private void SetupLight()
        {
            _light = GetComponent<Light>();
            if (_light == null)
            {
                _light = gameObject.AddComponent<Light>();
            }

            _light.type = LightType.Point;
            _light.color = lightColor;
            _light.intensity = lightIntensity;
            _light.range = lightRange;
        }

        private void LateUpdate()
        {
            if (_lineRenderer != null && renderMode == TracerRenderMode.LineRenderer)
            {
                // Update line positions based on movement direction
                Vector3 endPos = transform.position;
                Vector3 startPos = endPos - transform.forward * lineLength;

                _lineRenderer.SetPosition(0, startPos);
                _lineRenderer.SetPosition(1, endPos);
            }
        }

        private void OnDisable()
        {
            // Clear trail when recycled
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }
        }
    }

    public enum TracerRenderMode
    {
        LineRenderer,
        TrailRenderer,
        ParticleSystem
    }
}
