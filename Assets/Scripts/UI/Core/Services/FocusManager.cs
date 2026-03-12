using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Manages focus state for gamepad/keyboard navigation.
    /// Tracks a focus stack so that closing a modal restores focus to
    /// the element that was focused before the modal opened.
    /// </summary>
    public static class FocusManager
    {
        private struct FocusEntry
        {
            public VisualElement PreviouslyFocused;
            public VisualElement ScreenRoot;
        }

        private static readonly Stack<FocusEntry> _focusStack = new();

        /// <summary>Current stack depth (for debug display).</summary>
        public static int Depth => _focusStack.Count;

        /// <summary>
        /// Saves the current focus state and sets focus on the new screen.
        /// Call when pushing a new screen/modal.
        /// </summary>
        /// <param name="screenRoot">The root VisualElement of the new screen.</param>
        /// <param name="initialFocusName">USS name of the element to auto-focus. Null = focus the root.</param>
        public static void PushFocus(VisualElement screenRoot, string initialFocusName = null)
        {
            if (screenRoot == null) return;

            // Save whatever currently has focus
            var currentFocused = screenRoot.panel?.focusController?.focusedElement as VisualElement;

            _focusStack.Push(new FocusEntry
            {
                PreviouslyFocused = currentFocused,
                ScreenRoot = screenRoot
            });

            // Set initial focus on the new screen
            SetInitialFocus(screenRoot, initialFocusName);
        }

        /// <summary>
        /// Restores focus to the element that was focused before the last push.
        /// Call when popping/closing a screen/modal.
        /// </summary>
        public static void PopFocus()
        {
            if (_focusStack.Count == 0) return;

            var entry = _focusStack.Pop();

            // Restore previous focus
            if (entry.PreviouslyFocused != null && entry.PreviouslyFocused.panel != null)
            {
                entry.PreviouslyFocused.Focus();
            }
        }

        /// <summary>
        /// Clears the entire focus stack. Call on scene transitions or full UI reset.
        /// </summary>
        public static void Clear()
        {
            _focusStack.Clear();
        }

        /// <summary>
        /// Sets focus on a named element within a screen root, or the root itself.
        /// </summary>
        public static void SetInitialFocus(VisualElement screenRoot, string elementName = null)
        {
            if (screenRoot == null) return;

            VisualElement target = null;

            if (!string.IsNullOrEmpty(elementName))
            {
                target = screenRoot.Q(elementName);
                if (target == null)
                {
                    Debug.LogWarning($"[FocusManager] Initial focus element '{elementName}' not found in screen.");
                }
            }

            // Fallback: find first focusable element
            if (target == null)
            {
                target = FindFirstFocusable(screenRoot);
            }

            // Last resort: focus the root
            if (target == null)
            {
                target = screenRoot;
            }

            if (target.focusable)
            {
                target.Focus();
            }
        }

        /// <summary>
        /// Finds the first focusable descendant element via depth-first traversal.
        /// </summary>
        private static VisualElement FindFirstFocusable(VisualElement parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent[i];
                if (child.focusable && child.enabledSelf && child.resolvedStyle.display == DisplayStyle.Flex)
                    return child;

                var found = FindFirstFocusable(child);
                if (found != null) return found;
            }

            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _focusStack.Clear();
        }
    }
}
