using System;
using DIG.Tutorial.Config;
using UnityEngine;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Auto-completes after TimeoutSeconds.
    /// Ticked by TutorialService.Update().
    /// </summary>
    public class TimerCompletionDetector : ICompletionDetector
    {
        public bool IsCompleted { get; private set; }
        public event Action OnCompleted;

        private float _duration;
        private float _elapsed;

        public void Start(TutorialStepSO step)
        {
            IsCompleted = false;
            _elapsed = 0f;
            _duration = step.TimeoutSeconds > 0 ? step.TimeoutSeconds : 3f;
        }

        public void Stop()
        {
            IsCompleted = false;
            _elapsed = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (IsCompleted) return;
            _elapsed += deltaTime;
            if (_elapsed >= _duration)
            {
                IsCompleted = true;
                OnCompleted?.Invoke();
            }
        }
    }
}
