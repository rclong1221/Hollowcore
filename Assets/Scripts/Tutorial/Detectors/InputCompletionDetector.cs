using System;
using System.Collections.Generic;
using DIG.Tutorial.Config;
using UnityEngine.InputSystem;

namespace DIG.Tutorial.Detectors
{
    /// <summary>
    /// EPIC 18.4: Completes when a specific InputAction is performed.
    /// CompletionParam = action name (e.g., "Move", "Jump", "Interact").
    /// Searches enabled InputActions by name at step start.
    /// </summary>
    public class InputCompletionDetector : ICompletionDetector
    {
        public bool IsCompleted { get; private set; }
        public event Action OnCompleted;

        private InputAction _action;
        private static readonly List<InputAction> s_enabledActionsCache = new();

        public void Start(TutorialStepSO step)
        {
            IsCompleted = false;
            _action = null;

            if (string.IsNullOrEmpty(step.CompletionParam)) return;

            // Reuse cached list to avoid allocation
            s_enabledActionsCache.Clear();
            InputSystem.ListEnabledActions(s_enabledActionsCache);
            foreach (var action in s_enabledActionsCache)
            {
                if (string.Equals(action.name, step.CompletionParam, StringComparison.OrdinalIgnoreCase))
                {
                    _action = action;
                    break;
                }
            }

            if (_action != null)
            {
                _action.performed += OnActionPerformed;
            }
        }

        public void Stop()
        {
            if (_action != null)
            {
                _action.performed -= OnActionPerformed;
                _action = null;
            }
            IsCompleted = false;
        }

        private void OnActionPerformed(InputAction.CallbackContext ctx)
        {
            if (IsCompleted) return;
            IsCompleted = true;
            OnCompleted?.Invoke();
        }
    }
}
