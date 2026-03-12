using System;

namespace DIG.Widgets
{
    /// <summary>
    /// EPIC 15.26 Phase 1: Widget type identifiers for the widget ecosystem.
    /// </summary>
    public enum WidgetType : byte
    {
        HealthBar = 0,
        Nameplate = 1,
        DamageNumber = 2,
        FloatingText = 3,
        InteractPrompt = 4,
        CastBar = 5,
        BuffRow = 6,
        BossPlate = 7,
        QuestMarker = 8,
        LootLabel = 9,
        OffScreenIndicator = 10,
        MinimapPip = 11,
        TelegraphIndicator = 12
    }

    /// <summary>
    /// Bitmask for multiple active widget types on a single entity.
    /// An MMO enemy might have HealthBar | Nameplate | CastBar | BuffRow.
    /// </summary>
    [Flags]
    public enum WidgetFlags : ushort
    {
        None             = 0,
        HealthBar        = 1 << 0,
        Nameplate        = 1 << 1,
        CastBar          = 1 << 2,
        BuffRow          = 1 << 3,
        InteractPrompt   = 1 << 4,
        QuestMarker      = 1 << 5,
        LootLabel        = 1 << 6,
        BossPlate        = 1 << 7,
        OffScreen        = 1 << 8,
    }

    /// <summary>
    /// Distance-based level of detail for widget rendering.
    /// Thresholds are paradigm-configurable via ParadigmWidgetProfile.
    /// </summary>
    public enum WidgetLODTier : byte
    {
        Full = 0,       // All details: bar + trail + flash + name + guild + buffs + cast bar
        Reduced = 1,    // Thin bar, name only, top 3 buffs, no cast bar
        Minimal = 2,    // Health pip (dot), no name/buffs/cast
        Culled = 3      // Hidden (beyond max distance)
    }

    /// <summary>
    /// Billboard orientation mode for world-space widgets.
    /// Selected per paradigm via ParadigmWidgetProfile.
    /// </summary>
    public enum BillboardMode : byte
    {
        CameraAligned = 0,  // Face camera direction (FPS/TPS/MMO)
        FlatOverhead = 1,   // Horizontal above entity, rotated to face camera (ARPG/MOBA)
        ScreenSpace = 2     // Screen-space overlay, no world object (boss plates, off-screen)
    }

    /// <summary>
    /// Visual style for health bars, per paradigm.
    /// </summary>
    public enum HealthBarStyle : byte
    {
        Thin = 0,       // Narrow bar (Shooter/TwinStick)
        Standard = 1,   // Normal width (MMO/ARPG/SideScroller)
        Compact = 2     // Wide and thin, no decoration (MOBA)
    }

    /// <summary>
    /// Nameplate detail level, per paradigm.
    /// </summary>
    public enum NameplateComplexity : byte
    {
        Full = 0,       // Name + guild + level + health + buffs + cast bar
        Reduced = 1,    // Name + health + top 3 buffs
        Compact = 2     // Health bar only, wider and thin
    }
}
