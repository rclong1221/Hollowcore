using UnityEngine;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 5: Configuration for off-screen indicator icons.
    /// Maps TrackedEntityType to icon sprites, colors, and display settings.
    ///
    /// Create via: Assets > Create > DIG/Widgets/Off-Screen Indicator Config
    /// </summary>
    [CreateAssetMenu(fileName = "OffScreenIndicatorConfig", menuName = "DIG/Widgets/Off-Screen Indicator Config")]
    public class OffScreenIndicatorConfig : ScriptableObject
    {
        [Header("Layout")]
        [Tooltip("Margin in pixels from screen edge for indicator placement.")]
        [Range(10f, 80f)]
        public float EdgeMargin = 40f;

        [Tooltip("Maximum simultaneous off-screen indicators.")]
        [Range(1, 10)]
        public int MaxIndicators = 5;

        [Tooltip("Show distance text below the arrow icon.")]
        public bool ShowDistanceText = true;

        [Tooltip("Distance text format. {0} = distance in meters.")]
        public string DistanceFormat = "{0:F0}m";

        [Header("Tracked Types")]
        public TrackedTypeEntry[] TrackedTypes = new TrackedTypeEntry[]
        {
            new() { Type = TrackedEntityType.Boss, Icon = null, Color = new Color(0.9f, 0.15f, 0.15f, 1f), Priority = 0 },
            new() { Type = TrackedEntityType.QuestObjective, Icon = null, Color = new Color(1f, 0.85f, 0.1f, 1f), Priority = 1 },
            new() { Type = TrackedEntityType.PartyMember, Icon = null, Color = new Color(0.2f, 0.85f, 0.2f, 1f), Priority = 2 },
            new() { Type = TrackedEntityType.Targeted, Icon = null, Color = new Color(0.9f, 0.15f, 0.15f, 1f), Priority = 3 },
            new() { Type = TrackedEntityType.Waypoint, Icon = null, Color = new Color(1f, 1f, 1f, 0.9f), Priority = 4 },
            new() { Type = TrackedEntityType.LegendaryLoot, Icon = null, Color = new Color(1f, 0.6f, 0.1f, 1f), Priority = 5 },
        };

        /// <summary>
        /// Get the config entry for a tracked entity type. Returns null if not found.
        /// </summary>
        public TrackedTypeEntry GetEntry(TrackedEntityType type)
        {
            if (TrackedTypes == null) return null;
            for (int i = 0; i < TrackedTypes.Length; i++)
            {
                if (TrackedTypes[i] != null && TrackedTypes[i].Type == type)
                    return TrackedTypes[i];
            }
            return null;
        }
    }

    [System.Serializable]
    public class TrackedTypeEntry
    {
        public TrackedEntityType Type;
        public Sprite Icon;
        public Color Color = Color.white;
        [Tooltip("Lower = higher priority when budget exceeded.")]
        public int Priority;
    }
}
