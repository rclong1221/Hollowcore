using UnityEngine;
using DIG.Core.Input;

namespace DIG.Widgets.Config
{
    /// <summary>
    /// EPIC 15.26 Phase 4: Per-paradigm widget configuration ScriptableObject.
    /// One profile per InputParadigm (Shooter/MMO/ARPG/MOBA/TwinStick/SideScroller).
    /// Designers tune widget behavior without code changes.
    ///
    /// Follows the established pattern from ParadigmSurfaceProfile (EPIC 15.24)
    /// and ProceduralMotionProfile paradigm weights (EPIC 15.25).
    ///
    /// Create via: Assets > Create > DIG/Widgets/Paradigm Widget Profile
    /// </summary>
    [CreateAssetMenu(fileName = "WidgetProfile_Shooter", menuName = "DIG/Widgets/Paradigm Widget Profile")]
    public class ParadigmWidgetProfile : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which paradigm this profile configures.")]
        public InputParadigm Paradigm = InputParadigm.Shooter;

        // ── Budget & Distance ──────────────────────────────────────

        [Header("Budget & Culling")]
        [Tooltip("Maximum active widgets across all types. Excess culled by importance.")]
        [Range(10, 300)]
        public int MaxActiveWidgets = 40;

        [Tooltip("Multiplier on LOD distance thresholds. Higher = widgets visible further. Isometric: 2.0")]
        [Range(0.5f, 3f)]
        public float LODDistanceMultiplier = 1f;

        [Tooltip("Widget size multiplier. Isometric cameras need larger widgets. ARPG: 1.8")]
        [Range(0.5f, 3f)]
        public float WidgetScaleMultiplier = 1f;

        [Tooltip("Importance distance falloff rate. Higher = closer entities score much higher. Shooter: 3.0, ARPG: 1.0")]
        [Range(0.5f, 5f)]
        public float DistanceFalloff = 3f;

        // ── Widget Enable Flags ────────────────────────────────────

        [Header("Widget Types Enabled")]
        public bool HealthBarEnabled = true;
        public bool NameplateEnabled = false;
        public bool CastBarEnabled = true;
        public bool BuffRowEnabled = false;
        public bool LootLabelEnabled = true;
        public bool QuestMarkerEnabled = false;
        public bool OffScreenEnabled = true;
        public bool ShowHealthBarOnPlayer = false;

        // ── Visual Style ───────────────────────────────────────────

        [Header("Visual Style")]
        [Tooltip("Billboard orientation for world-space widgets.")]
        public BillboardMode Billboard = BillboardMode.CameraAligned;

        [Tooltip("Health bar visual style.")]
        public HealthBarStyle Style = HealthBarStyle.Thin;

        [Tooltip("Nameplate detail level.")]
        public NameplateComplexity Complexity = NameplateComplexity.Compact;

        // ── Scaling ────────────────────────────────────────────────

        [Header("Per-Type Scaling")]
        [Tooltip("Damage number size multiplier. ARPG: 1.5")]
        [Range(0.5f, 3f)]
        public float DamageNumberScale = 1f;

        [Tooltip("Y offset above entity for widget anchor in meters.")]
        [Range(0f, 5f)]
        public float HealthBarYOffset = 2.5f;

        [Tooltip("Font scale for accessibility. Combined with WidgetAccessibilityConfig.")]
        [Range(0.5f, 2f)]
        public float AccessibilityFontScale = 1f;

        // ── Stacking & Grouping ────────────────────────────────────

        [Header("Stacking & Grouping")]
        [Tooltip("Resolve overlapping widgets by displacing vertically.")]
        public bool StackingEnabled = true;

        [Tooltip("Group clusters of same-type entities into a single badge.")]
        public bool GroupingEnabled = false;

        [Tooltip("Minimum cluster size before grouping activates.")]
        [Range(2, 10)]
        public int GroupingThreshold = 4;

        // ── Buff Row ───────────────────────────────────────────────

        [Header("Buff Row")]
        [Tooltip("Maximum buff/debuff icons shown per entity.")]
        [Range(0, 16)]
        public int BuffRowMaxIcons = 5;

        // ── Boss Plate ─────────────────────────────────────────────

        [Header("Boss Plate")]
        [Tooltip("Screen position for boss health plate.")]
        public BossPlatePosition BossPlatePosition = BossPlatePosition.Top;
    }

    /// <summary>
    /// Where the boss plate health bar renders on screen.
    /// </summary>
    public enum BossPlatePosition : byte
    {
        Top = 0,
        Bottom = 1
    }
}
