using UnityEngine;

namespace DIG.Weather
{
    [CreateAssetMenu(menuName = "DIG/Weather/Day-Night Config")]
    public class DayNightConfigSO : ScriptableObject
    {
        [Header("Time")]
        [Tooltip("Real seconds per game day.")]
        [Min(60f)]
        public float DayLengthSeconds = 1200f;

        [Tooltip("Game days per season cycle.")]
        [Min(1)]
        public int SeasonLengthDays = 7;

        [Header("Sun")]
        [Tooltip("Hour when sun rises above horizon.")]
        [Range(0f, 12f)]
        public float SunriseHour = 6.0f;

        [Tooltip("Hour when sun sets below horizon.")]
        [Range(12f, 24f)]
        public float SunsetHour = 18.0f;

        [Tooltip("Maximum sun elevation at solar noon (degrees).")]
        [Range(30f, 90f)]
        public float SunPitchMax = 75.0f;

        [Tooltip("Moon elevation at midnight (degrees).")]
        [Range(10f, 60f)]
        public float MoonPitchMax = 40.0f;

        [Header("Ambient")]
        [Tooltip("Ambient light intensity at midnight.")]
        [Range(0f, 0.5f)]
        public float NightAmbientIntensity = 0.05f;

        [Tooltip("Ambient light intensity at noon.")]
        [Range(0.5f, 2f)]
        public float DayAmbientIntensity = 1.0f;

        [Header("Gradients")]
        [Tooltip("Directional light color over the day cycle.")]
        public Gradient SunColorGradient;

        [Tooltip("Ambient light color over the day cycle.")]
        public Gradient AmbientColorGradient;

        [Tooltip("Directional light intensity curve over 24 hours.")]
        public AnimationCurve SunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Skybox")]
        public Material SkyboxDawnMaterial;
        public Material SkyboxDayMaterial;
        public Material SkyboxDuskMaterial;
        public Material SkyboxNightMaterial;

        [Tooltip("Star layer opacity curve. 1 at night, 0 during day.")]
        public AnimationCurve StarVisibilityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Start State")]
        [Tooltip("Time of day when the world starts (hours).")]
        [Range(0f, 24f)]
        public float StartTimeOfDay = 8.0f;

        private void Reset()
        {
            SunColorGradient = new Gradient();
            SunColorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0.00f),   // midnight
                    new GradientColorKey(new Color(1.0f, 0.5f, 0.2f), 0.25f),   // dawn
                    new GradientColorKey(new Color(1.0f, 0.95f, 0.85f), 0.50f), // noon
                    new GradientColorKey(new Color(1.0f, 0.6f, 0.3f), 0.75f),   // dusk
                    new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1.00f),   // midnight
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );

            AmbientColorGradient = new Gradient();
            AmbientColorGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 0.00f), // midnight
                    new GradientColorKey(new Color(0.6f, 0.5f, 0.4f), 0.25f),    // dawn
                    new GradientColorKey(new Color(0.8f, 0.85f, 0.9f), 0.50f),   // noon
                    new GradientColorKey(new Color(0.7f, 0.4f, 0.2f), 0.75f),    // dusk
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.15f), 1.00f), // midnight
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );

            SunIntensityCurve = new AnimationCurve(
                new Keyframe(0.00f, 0.0f),  // midnight
                new Keyframe(0.25f, 0.3f),  // 6am
                new Keyframe(0.50f, 1.0f),  // noon
                new Keyframe(0.75f, 0.3f),  // 6pm
                new Keyframe(1.00f, 0.0f)   // midnight
            );

            StarVisibilityCurve = new AnimationCurve(
                new Keyframe(0.00f, 1.0f),  // midnight
                new Keyframe(0.20f, 1.0f),  // 4:48am
                new Keyframe(0.30f, 0.0f),  // 7:12am
                new Keyframe(0.70f, 0.0f),  // 4:48pm
                new Keyframe(0.80f, 1.0f),  // 7:12pm
                new Keyframe(1.00f, 1.0f)   // midnight
            );
        }
    }
}
