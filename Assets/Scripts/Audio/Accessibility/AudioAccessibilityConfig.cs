using UnityEngine;

namespace Audio.Accessibility
{
    /// <summary>
    /// Audio accessibility settings for hearing-impaired players.
    /// Persisted via PlayerPrefs (same pattern as WidgetAccessibilityManager).
    /// EPIC 15.27 Phase 7.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioAccessibilityConfig", menuName = "DIG/Audio/Accessibility Config")]
    public class AudioAccessibilityConfig : ScriptableObject
    {
        [Header("Sound Radar")]
        [Tooltip("Show directional sound indicators on HUD")]
        public bool EnableSoundRadar = false;

        [Tooltip("Radar size multiplier")]
        [Range(0.5f, 2f)]
        public float RadarSize = 1f;

        [Tooltip("Minimum source priority to show on radar (skip quiet ambient)")]
        [Range(0, 200)]
        public int RadarMinPriority = 40;

        [Header("Directional Subtitles")]
        [Tooltip("Show directional arrows with dialogue subtitles")]
        public bool EnableDirectionalSubtitles = false;

        [Tooltip("Subtitle font scale")]
        [Range(1f, 2f)]
        public float SubtitleFontScale = 1f;

        [Header("Visual Sound Indicators")]
        [Tooltip("Show screen-edge flash for off-screen sounds")]
        public bool EnableVisualSoundIndicators = false;

        [Tooltip("Intensity of visual indicators")]
        [Range(0f, 1f)]
        public float VisualIndicatorIntensity = 0.7f;

        [Header("Tinnitus")]
        [Tooltip("Disable the high-pitched tinnitus audio (keep visual flash only)")]
        public bool DisableTinnitusAudio = false;

        private const string kPrefsPrefix = "AudioAccess_";

        public void LoadFromPrefs()
        {
            EnableSoundRadar = PlayerPrefs.GetInt(kPrefsPrefix + "SoundRadar", 0) == 1;
            RadarSize = PlayerPrefs.GetFloat(kPrefsPrefix + "RadarSize", 1f);
            EnableDirectionalSubtitles = PlayerPrefs.GetInt(kPrefsPrefix + "DirSubs", 0) == 1;
            SubtitleFontScale = PlayerPrefs.GetFloat(kPrefsPrefix + "SubScale", 1f);
            EnableVisualSoundIndicators = PlayerPrefs.GetInt(kPrefsPrefix + "VisualInd", 0) == 1;
            VisualIndicatorIntensity = PlayerPrefs.GetFloat(kPrefsPrefix + "VisIntensity", 0.7f);
            DisableTinnitusAudio = PlayerPrefs.GetInt(kPrefsPrefix + "NoTinnitus", 0) == 1;
        }

        public void SaveToPrefs()
        {
            PlayerPrefs.SetInt(kPrefsPrefix + "SoundRadar", EnableSoundRadar ? 1 : 0);
            PlayerPrefs.SetFloat(kPrefsPrefix + "RadarSize", RadarSize);
            PlayerPrefs.SetInt(kPrefsPrefix + "DirSubs", EnableDirectionalSubtitles ? 1 : 0);
            PlayerPrefs.SetFloat(kPrefsPrefix + "SubScale", SubtitleFontScale);
            PlayerPrefs.SetInt(kPrefsPrefix + "VisualInd", EnableVisualSoundIndicators ? 1 : 0);
            PlayerPrefs.SetFloat(kPrefsPrefix + "VisIntensity", VisualIndicatorIntensity);
            PlayerPrefs.SetInt(kPrefsPrefix + "NoTinnitus", DisableTinnitusAudio ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
