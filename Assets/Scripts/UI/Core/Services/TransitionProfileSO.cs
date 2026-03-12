using UnityEngine;

namespace DIG.UI.Core.Services
{
    public enum TransitionType
    {
        None,
        Fade,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        Scale
    }

    /// <summary>
    /// EPIC 18.1: Configuration asset for screen transitions.
    /// USS-class-based: TransitionPlayer adds/removes USS classes to trigger CSS transitions.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/UI/Transition Profile")]
    public class TransitionProfileSO : ScriptableObject
    {
        [Tooltip("The type of transition (determines which USS classes are toggled).")]
        public TransitionType Type = TransitionType.Fade;

        [Tooltip("Duration in seconds.")]
        [Range(0f, 2f)]
        public float Duration = 0.2f;

        [Tooltip("Optional custom easing curve. If null, USS transition-timing-function is used.")]
        public AnimationCurve EasingCurve;

        [Tooltip("Delay before the transition starts (seconds).")]
        [Range(0f, 1f)]
        public float Delay = 0f;

        /// <summary>Total transition time including delay.</summary>
        public float TotalTime => Duration + Delay;

        // Cached class name strings — resolved once on enable/validate
        [System.NonSerialized] private string _cachedActiveClass;
        [System.NonSerialized] private string _cachedStartClass;
        [System.NonSerialized] private bool _classesCached;

        /// <summary>USS class name for the "active/visible" state.</summary>
        public string ActiveClass
        {
            get
            {
                if (!_classesCached) CacheClasses();
                return _cachedActiveClass;
            }
        }

        /// <summary>USS class for the starting position (applied before active class).</summary>
        public string StartClass
        {
            get
            {
                if (!_classesCached) CacheClasses();
                return _cachedStartClass;
            }
        }

        private void CacheClasses()
        {
            _cachedActiveClass = Type switch
            {
                TransitionType.Fade => "screen-transition--fade-in",
                TransitionType.SlideLeft => "screen-transition--slide-left-in",
                TransitionType.SlideRight => "screen-transition--slide-right-in",
                TransitionType.SlideUp => "screen-transition--slide-up-in",
                TransitionType.SlideDown => "screen-transition--slide-down-in",
                TransitionType.Scale => "screen-transition--scale-in",
                _ => ""
            };
            _cachedStartClass = Type switch
            {
                TransitionType.SlideLeft => "screen-transition--slide-left-start",
                TransitionType.SlideRight => "screen-transition--slide-right-start",
                TransitionType.SlideUp => "screen-transition--slide-up-start",
                TransitionType.SlideDown => "screen-transition--slide-down-start",
                TransitionType.Scale => "screen-transition--scale-start",
                _ => ""
            };
            _classesCached = true;
        }

        private void OnEnable() => _classesCached = false;
        private void OnValidate() => _classesCached = false;
    }
}
