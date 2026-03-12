using UnityEngine;

namespace DIG.DeathCamera
{
    /// <summary>
    /// EPIC 18.13: Death recap phase. Displays killer info, damage breakdown,
    /// and respawn timer. Camera holds at kill cam position or gently drifts overhead.
    /// Skippable.
    /// </summary>
    public class DeathRecapPhase : IDeathCameraPhase
    {
        public DeathCameraPhaseType PhaseType => DeathCameraPhaseType.DeathRecap;
        public bool IsComplete => _timer <= 0f || _skipped;
        public bool CanSkip => true;

        private float _timer;
        private bool _skipped;
        private DeathCameraContext _context;

        public void Enter(DeathCameraContext context)
        {
            _context = context;
            _skipped = false;
            _timer = context.Config.DeathRecapDuration;

            // Show recap UI
            var view = DeathRecapView.Instance;
            if (view != null)
                view.Show(context);
        }

        public void Update(float deltaTime)
        {
            if (_timer > 0f)
                _timer -= Time.unscaledDeltaTime;

            // Update respawn countdown on the UI — throttled to avoid per-frame string allocs
            if (Time.frameCount % 6 == 0)
            {
                var view = DeathRecapView.Instance;
                if (view != null)
                    view.UpdateRespawnCountdown(_context.RespawnTimeRemaining);
            }

            // Auto-advance if respawn is imminent
            if (_context.RespawnTimeRemaining < 1f)
                _skipped = true;
        }

        public void Skip()
        {
            _skipped = true;
        }

        public void Exit()
        {
            var view = DeathRecapView.Instance;
            if (view != null)
                view.Hide();
        }
    }
}
