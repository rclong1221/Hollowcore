using UnityEngine;

namespace DIG.Tutorial.Config
{
    /// <summary>
    /// EPIC 18.4: Master tutorial system configuration.
    /// Create via Assets > Create > DIG > Tutorial > Config.
    /// Place in Assets/Resources/ as "TutorialConfig".
    /// </summary>
    [CreateAssetMenu(menuName = "DIG/Tutorial/Config", fileName = "TutorialConfig")]
    public class TutorialConfigSO : ScriptableObject
    {
        [Header("Spotlight")]
        [Tooltip("Color of the semi-transparent mask around the spotlight cutout.")]
        public Color SpotlightColor = new Color(0f, 0f, 0f, 0.7f);

        [Header("Tooltip")]
        [Tooltip("Offset in pixels from the target element to the tooltip bubble.")]
        public float TooltipOffset = 12f;

        [Header("Timing")]
        [Tooltip("Default delay between steps in seconds.")]
        public float StepTransitionDelay = 0.3f;

        [Header("World Markers")]
        [Tooltip("Number of world marker instances to pre-warm in the pool.")]
        public int MarkerPoolSize = 3;
        [Tooltip("Margin in pixels from screen edges for off-screen clamping.")]
        public float ScreenEdgeMargin = 40f;

        [Header("Audio")]
        [Tooltip("Default sound played when a step appears. Overridden by TutorialStepSO.Sound.")]
        public AudioClip DefaultStepSound;
        [Range(0f, 1f)]
        public float SoundVolume = 0.5f;
    }
}
