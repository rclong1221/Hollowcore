using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DIG.UI.Core.Services
{
    /// <summary>
    /// EPIC 18.1: Plays USS-class-based transitions on VisualElements.
    /// No DOTween, no UniTask — uses VisualElement.schedule + USS class toggling.
    ///
    /// Pattern:
    ///   1. Prepare() adds base + start classes (element hidden via opacity:0)
    ///   2. PlayIn() sets display:flex, adds active class after 1 frame (triggers CSS transition)
    ///   3. PlayOut() removes active class, schedules display:none after duration
    /// </summary>
    public static class TransitionPlayer
    {
        private const string BaseClass = "screen-transition-base";

        // Reusable list to avoid allocation on every PlayIn/PlayOut call
        private static readonly List<TimeValue> _timeValueBuffer = new(1);

        /// <summary>
        /// Prepares an element for a transition-in.
        /// Adds base class (opacity:0) and optional start position class.
        /// Call before making the element visible.
        /// </summary>
        public static void Prepare(VisualElement element, TransitionProfileSO profile)
        {
            if (element == null) return;
            if (profile == null || profile.Type == TransitionType.None) return;

            element.AddToClassList(BaseClass);

            string startClass = profile.StartClass;
            if (!string.IsNullOrEmpty(startClass))
                element.AddToClassList(startClass);
        }

        /// <summary>
        /// Plays the "in" transition (element becomes visible with animation).
        /// Element must already be in the visual tree.
        /// </summary>
        public static void PlayIn(VisualElement element, TransitionProfileSO profile, Action onComplete = null)
        {
            if (element == null)
            {
                onComplete?.Invoke();
                return;
            }

            // No transition: instant show
            if (profile == null || profile.Type == TransitionType.None)
            {
                element.RemoveFromClassList(BaseClass);
                element.style.display = DisplayStyle.Flex;
                element.style.opacity = 1f;
                onComplete?.Invoke();
                return;
            }

            // Set duration inline from profile (reuse buffer to avoid allocation)
            _timeValueBuffer.Clear();
            _timeValueBuffer.Add(new TimeValue(profile.Duration * 1000f, TimeUnit.Millisecond));
            element.style.transitionDuration = new StyleList<TimeValue>(_timeValueBuffer);

            // Make visible first (display:flex so transitions can run)
            element.style.display = DisplayStyle.Flex;

            // Schedule the active class addition after 1 frame so the browser/UIToolkit
            // registers the initial state before transitioning to the final state
            long delayMs = Mathf.Max(1, Mathf.RoundToInt(profile.Delay * 1000f));
            element.schedule.Execute(() =>
            {
                // Remove start class and add active class to trigger transition
                string startClass = profile.StartClass;
                if (!string.IsNullOrEmpty(startClass))
                    element.RemoveFromClassList(startClass);

                element.AddToClassList(profile.ActiveClass);
            }).StartingIn(delayMs > 1 ? delayMs : 16); // At least 1 frame (16ms)

            // Schedule completion after full transition time
            long totalMs = Mathf.RoundToInt(profile.TotalTime * 1000f) + 16; // +1 frame buffer
            element.schedule.Execute(() =>
            {
                onComplete?.Invoke();
            }).StartingIn(totalMs);
        }

        /// <summary>
        /// Plays the "out" transition (element becomes hidden with animation).
        /// </summary>
        public static void PlayOut(VisualElement element, TransitionProfileSO profile, Action onComplete = null)
        {
            if (element == null)
            {
                onComplete?.Invoke();
                return;
            }

            // No transition: instant hide
            if (profile == null || profile.Type == TransitionType.None)
            {
                element.style.display = DisplayStyle.None;
                element.style.opacity = 1f;
                CleanupClasses(element, profile);
                onComplete?.Invoke();
                return;
            }

            // Set duration inline from profile (reuse buffer to avoid allocation)
            _timeValueBuffer.Clear();
            _timeValueBuffer.Add(new TimeValue(profile.Duration * 1000f, TimeUnit.Millisecond));
            element.style.transitionDuration = new StyleList<TimeValue>(_timeValueBuffer);

            // Remove active class to trigger reverse transition
            element.RemoveFromClassList(profile.ActiveClass);

            // Add start class for position-based transitions
            string startClass = profile.StartClass;
            if (!string.IsNullOrEmpty(startClass))
                element.AddToClassList(startClass);

            // Schedule hide + cleanup after transition duration
            long totalMs = Mathf.RoundToInt(profile.TotalTime * 1000f) + 16;
            element.schedule.Execute(() =>
            {
                element.style.display = DisplayStyle.None;
                CleanupClasses(element, profile);
                onComplete?.Invoke();
            }).StartingIn(totalMs);
        }

        /// <summary>
        /// Instantly shows an element without any transition (for initial screen setup or teleport).
        /// </summary>
        public static void ShowInstant(VisualElement element)
        {
            if (element == null) return;
            CleanupClasses(element, null);
            element.style.display = DisplayStyle.Flex;
            element.style.opacity = 1f;
        }

        /// <summary>
        /// Instantly hides an element without any transition.
        /// </summary>
        public static void HideInstant(VisualElement element)
        {
            if (element == null) return;
            CleanupClasses(element, null);
            element.style.display = DisplayStyle.None;
        }

        private static void CleanupClasses(VisualElement element, TransitionProfileSO profile)
        {
            element.RemoveFromClassList(BaseClass);
            if (profile != null)
            {
                if (!string.IsNullOrEmpty(profile.ActiveClass))
                    element.RemoveFromClassList(profile.ActiveClass);
                if (!string.IsNullOrEmpty(profile.StartClass))
                    element.RemoveFromClassList(profile.StartClass);
            }
        }
    }
}
