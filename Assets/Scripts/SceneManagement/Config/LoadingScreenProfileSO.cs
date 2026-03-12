using UnityEngine;

namespace DIG.SceneManagement
{
    /// <summary>
    /// EPIC 18.6: Designer-configurable loading screen appearance.
    /// Assigned per GameFlowState or used as a fallback default.
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Scene Management/Loading Screen Profile")]
    public class LoadingScreenProfileSO : ScriptableObject
    {
        [Header("Background")]
        [Tooltip("Random background art selected per load.")]
        public Sprite[] BackgroundSprites;

        [Header("Tips")]
        [Tooltip("Pool of gameplay tips — one chosen at random, rotated every 5 seconds.")]
        [TextArea(1, 3)] public string[] Tips;

        [Header("Progress")]
        public bool ShowProgressBar = true;
        public ProgressBarStyle ProgressBarStyle = ProgressBarStyle.Continuous;

        [Header("Timing")]
        [Tooltip("Minimum seconds the loading screen is displayed.")]
        [Min(0f)] public float MinDisplaySeconds = 1.0f;

        [Tooltip("Fade-in duration for the loading screen overlay.")]
        [Min(0f)] public float FadeInDuration = 0.3f;

        [Tooltip("Fade-out duration when dismissing the loading screen.")]
        [Min(0f)] public float FadeOutDuration = 0.3f;

        [Header("Audio")]
        [Tooltip("Music played during loading. Null keeps current music.")]
        public AudioClip MusicClip;
    }
}
