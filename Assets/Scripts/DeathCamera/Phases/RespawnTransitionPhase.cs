using DIG.CameraSystem;
using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Respawn transition phase. Smoothly blends the camera back
    /// to the gameplay camera via CameraTransitionManager. Releases camera authority.
    /// </summary>
    public class RespawnTransitionPhase : IDeathCameraPhase
    {
        public DeathCameraPhaseType PhaseType => DeathCameraPhaseType.RespawnTransition;
        public bool IsComplete => _complete;
        public bool CanSkip => false;

        private bool _complete;
        private DeathCameraContext _context;

        public void Enter(DeathCameraContext context)
        {
            _context = context;
            _complete = false;

            // Restore previous gameplay camera via smooth transition.
            // Must NOT call SetActiveCamera before TransitionToCamera, or the transition
            // detects from==to and skips the blend.
            if (context.PreviousCamera != null)
            {
                if (CameraTransitionManager.HasInstance)
                {
                    CameraTransitionManager.Instance.TransitionToCamera(
                        context.PreviousCamera,
                        context.Config.RespawnTransitionDuration,
                        () => { _complete = true; },
                        force: true
                    );
                    return;
                }

                // No transition manager — set active camera directly
                if (CameraModeProvider.HasInstance)
                    CameraModeProvider.Instance.SetActiveCamera(context.PreviousCamera, force: true);
            }

            // No transition manager or no previous camera — complete immediately
            _complete = true;
        }

        public void Update(float deltaTime)
        {
            // Transition is handled by CameraTransitionManager callback
            // If it's taking too long, force complete
            if (!_complete && CameraTransitionManager.HasInstance && !CameraTransitionManager.Instance.IsTransitioning)
            {
                _complete = true;
            }
        }

        public void Skip() { }

        public void Exit()
        {
            // Authority release is handled by the orchestrator
        }
    }
}
