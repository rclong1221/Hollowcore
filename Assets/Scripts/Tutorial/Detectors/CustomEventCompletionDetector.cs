using System;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Completes when TutorialService.FireEvent(key) matches CompletionParam.
    /// </summary>
    public class CustomEventCompletionDetector : ICompletionDetector
    {
        public bool IsCompleted { get; private set; }
        public event Action OnCompleted;

        private string _eventKey;

        public void Start(TutorialStepSO step)
        {
            IsCompleted = false;
            _eventKey = step.CompletionParam;
        }

        public void Stop()
        {
            IsCompleted = false;
            _eventKey = null;
        }

        public void OnEventFired(string key)
        {
            if (IsCompleted || string.IsNullOrEmpty(_eventKey)) return;
            if (string.Equals(key, _eventKey, StringComparison.OrdinalIgnoreCase))
            {
                IsCompleted = true;
                OnCompleted?.Invoke();
            }
        }
    }
}
