using System;
using DIG.Tutorial.Config;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Completes when a specific UI screen is opened.
    /// CompletionParam = screen type name (e.g., "InventoryScreen", "MapScreen").
    /// Polls UIServices.Screen.ActiveScreen each frame via TutorialService.
    /// </summary>
    public class UIScreenCompletionDetector : ICompletionDetector
    {
        public bool IsCompleted { get; private set; }
        public event Action OnCompleted;

        private string _targetScreen;

        public void Start(TutorialStepSO step)
        {
            IsCompleted = false;
            _targetScreen = step.CompletionParam;
        }

        public void Stop()
        {
            IsCompleted = false;
            _targetScreen = null;
        }

        public void CheckScreen(string currentScreenName)
        {
            if (IsCompleted || string.IsNullOrEmpty(_targetScreen)) return;
            if (string.Equals(currentScreenName, _targetScreen, StringComparison.OrdinalIgnoreCase))
            {
                IsCompleted = true;
                OnCompleted?.Invoke();
            }
        }
    }
}
