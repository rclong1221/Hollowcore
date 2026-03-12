using DIG.CameraSystem;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Kill cam phase. Dramatic orbit around the kill position
    /// with optional slow motion. Skippable by player input.
    ///
    /// Slow-motion is LOCAL-ONLY: on a listen server the server world
    /// keeps ticking at normal speed so other players are never affected.
    /// We achieve this by NOT touching Time.timeScale and instead using a
    /// local time multiplier that only drives the kill cam orbit speed.
    /// </summary>
    public class KillCamPhase : IDeathCameraPhase
    {
        public DeathCameraPhaseType PhaseType => DeathCameraPhaseType.KillCam;
        public bool IsComplete => _timer <= 0f;
        public bool CanSkip => true;

        private float _timer;
        private float _totalDuration;
        private float _localTimeScale;
        private float _startRadius;
        private float _endRadius;
        private float _startHeight;
        private float _endHeight;
        private DeathKillCam _killCam;
        private DeathCameraContext _context;

        public void Enter(DeathCameraContext context)
        {
            _context = context;
            var config = context.Config;
            _timer = config.KillCamDuration;
            _totalDuration = config.KillCamDuration;

            // Local slow-motion multiplier (never touches Time.timeScale)
            _localTimeScale = config.KillCamSlowMotion ? config.KillCamTimeScale : 1f;

            // Zoom-in: cache start/end values for interpolation
            _startRadius = config.KillCamOrbitRadius;
            _endRadius = config.KillCamEndRadius;
            _startHeight = config.KillCamOrbitHeight;
            _endHeight = config.KillCamEndHeight;

            // Create kill cam camera mode
            var go = new GameObject("[DeathKillCam]");
            Object.DontDestroyOnLoad(go);
            _killCam = go.AddComponent<DeathKillCam>();
            _killCam.SetGameplayMode(context.GameplayMode, context.GameplayCameraConfig, config);
            _killCam.SetKillPosition(
                context.KillPosition,
                config.KillCamOrbitRadius,
                config.KillCamOrbitHeight,
                config.KillCamOrbitSpeed,
                config.FOV
            );

            // Built-in ease-in: capture current camera pose before switching
            if (context.TargetCamera != null)
            {
                var camT = context.TargetCamera.transform;
                _killCam.SetTransitionFrom(camT.position, camT.rotation, config.KillCamTransitionIn);
            }

            // Set kill cam as active (TransitionToCamera would do this, but it doesn't exist in scene)
            bool hasTransMgr = CameraTransitionManager.HasInstance;
            bool hasModeProvider = CameraModeProvider.HasInstance;
            if (hasTransMgr)
                CameraTransitionManager.Instance.TransitionToCamera(_killCam, config.KillCamTransitionIn, force: true);
            else if (hasModeProvider)
                CameraModeProvider.Instance.SetActiveCamera((ICameraMode)_killCam, force: true);

            DCamLog.Log($"[DCam] KillCam ENTER — duration={_totalDuration}s, radius={_startRadius}→{_endRadius}, height={_startHeight}→{_endHeight}, localTimeScale={_localTimeScale}, transitionMgr={hasTransMgr}, modeProvider={hasModeProvider}, killCamGO={_killCam.gameObject.name}");
        }

        public void Update(float deltaTime)
        {
            // Timer always ticks at real time (unaffected by slow-mo)
            _timer -= Time.unscaledDeltaTime;

            if (_killCam == null) return;

            // Zoom-in: smoothly interpolate radius and height over the phase duration
            float progress = _totalDuration > 0f
                ? math.saturate(1f - _timer / _totalDuration)
                : 1f;
            // Smooth-step easing for a cinematic feel
            float t = progress * progress * (3f - 2f * progress);
            _killCam.SetOrbitRadius(math.lerp(_startRadius, _endRadius, t));
            _killCam.SetOrbitHeight(math.lerp(_startHeight, _endHeight, t));

            // Kill cam orbits at the local slow-motion rate for dramatic effect
            // This is purely cosmetic — does NOT touch Time.timeScale
            _killCam.UpdateCamera(Time.unscaledDeltaTime * _localTimeScale);

            // Throttled per-frame log (~1/sec)
            if (UnityEngine.Time.frameCount % 60 == 0)
                DCamLog.Log($"[DCam] KillCam tick — timer={_timer:F2}/{_totalDuration:F2}, zoom={t:F2}, pos={_killCam.transform.position}");
        }

        public void Skip()
        {
            _timer = 0f;
        }

        public void Exit()
        {
            DCamLog.Log("[DCam] KillCam EXIT");
            if (_killCam != null)
            {
                Object.Destroy(_killCam.gameObject);
                _killCam = null;
            }
        }
    }
}
