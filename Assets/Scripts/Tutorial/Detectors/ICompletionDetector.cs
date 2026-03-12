using System;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Strategy interface for detecting tutorial step completion.
    /// </summary>
    public interface ICompletionDetector
    {
        bool IsCompleted { get; }
        event Action OnCompleted;
        void Start(TutorialStepSO step);
        void Stop();
    }
}
