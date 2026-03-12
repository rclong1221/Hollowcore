using DIG.Tutorial.Config;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Creates the appropriate ICompletionDetector for a given condition type.
    /// </summary>
    public static class CompletionDetectorFactory
    {
        public static ICompletionDetector Create(CompletionCondition condition)
        {
            return condition switch
            {
                CompletionCondition.ManualContinue => new ManualCompletionDetector(),
                CompletionCondition.InputPerformed => new InputCompletionDetector(),
                CompletionCondition.UIScreenOpened => new UIScreenCompletionDetector(),
                CompletionCondition.CustomEvent => new CustomEventCompletionDetector(),
                CompletionCondition.Timer => new TimerCompletionDetector(),
                _ => new ManualCompletionDetector(),
            };
        }
    }
}
