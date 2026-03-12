using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24 Phase 6: Screen Dirt Effect System.
    /// Detects large explosions near the player and triggers a fullscreen dust/dirt overlay.
    /// The overlay is a CanvasGroup on a UI Image that fades over time.
    /// Requires a ScreenDirtOverlay MonoBehaviour in the scene.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SurfaceImpactPresenterSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class ScreenDirtSystem : SystemBase
    {
        private const float TriggerDistance = 5f;
        private const float CooldownSeconds = 1f;
        private float _lastTriggerTime = -100f;

        protected override void OnUpdate()
        {
            // Early-outs before touching Camera.main
            if (!ScreenDirtTrigger.HasPending) return;

            float time = (float)SystemAPI.Time.ElapsedTime;
            if (time - _lastTriggerTime < CooldownSeconds) return;

            var cam = Camera.main;
            if (cam == null) return;

            float3 camPos = cam.transform.position;
            var pending = ScreenDirtTrigger.Consume();
            float dist = math.distance(camPos, pending.Position);

            if (dist <= TriggerDistance)
            {
                float intensity = 1f - math.saturate(dist / TriggerDistance);
                ScreenDirtOverlay.Trigger(intensity, 2f); // 2 second fade
                _lastTriggerTime = time;
            }
        }
    }

    /// <summary>
    /// Static trigger for screen dirt from any system. Set by SurfaceImpactPresenterSystem
    /// when processing Explosion_Large impacts near the camera.
    /// </summary>
    public static class ScreenDirtTrigger
    {
        public struct DirtEvent
        {
            public float3 Position;
            public float Intensity;
        }

        private static DirtEvent _pending;
        public static bool HasPending { get; private set; }

        public static void Set(float3 position, float intensity)
        {
            _pending = new DirtEvent { Position = position, Intensity = intensity };
            HasPending = true;
        }

        public static DirtEvent Consume()
        {
            HasPending = false;
            return _pending;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            HasPending = false;
        }
    }

    /// <summary>
    /// MonoBehaviour overlay for screen dirt effect. Place on a UI Canvas Image.
    /// Receives trigger calls from ScreenDirtSystem and fades alpha over time.
    /// </summary>
    public class ScreenDirtOverlay : MonoBehaviour
    {
        private static ScreenDirtOverlay _instance;

        [SerializeField] private CanvasGroup _canvasGroup;
        private float _fadeTimer;
        private float _fadeDuration;
        private float _startAlpha;

        private void Awake()
        {
            _instance = this;
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (_canvasGroup == null || _fadeTimer <= 0f) return;

            _fadeTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_fadeTimer / _fadeDuration);
            _canvasGroup.alpha = Mathf.Lerp(_startAlpha, 0f, t);

            if (_fadeTimer <= 0f)
                _canvasGroup.alpha = 0f;
        }

        public static void Trigger(float intensity, float duration)
        {
            if (_instance == null || _instance._canvasGroup == null) return;

            _instance._startAlpha = Mathf.Clamp01(intensity);
            _instance._canvasGroup.alpha = _instance._startAlpha;
            _instance._fadeDuration = duration;
            _instance._fadeTimer = duration;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
