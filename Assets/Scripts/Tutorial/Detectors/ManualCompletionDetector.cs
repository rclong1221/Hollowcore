using System;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Completes when TutorialService.AdvanceStep() is called.
    /// Used for Popup and Tooltip steps with a "Continue" button.
    /// </summary>
    public class ManualCompletionDetector : ICompletionDetector
    {
        public bool IsCompleted { get; private set; }
        public event Action OnCompleted;

        public void Start(TutorialStepSO step)
        {
            IsCompleted = false;
        }

        public void Stop()
        {
            IsCompleted = false;
        }

        public void Complete()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            OnCompleted?.Invoke();
        }
    }
}
