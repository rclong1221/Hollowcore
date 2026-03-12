using Unity.Entities;

namespace DIG.Widgets
{
    /// <summary>
    /// EPIC 15.26 Phase 5: Marks an entity for off-screen tracking.
    /// When the entity fails frustum culling, an edge-of-screen indicator arrow
    /// is shown pointing toward its world position.
    ///
    /// Add via OffScreenTrackerAuthoring on boss, quest, party member, or
    /// waypoint prefabs.
    /// </summary>
    public struct OffScreenTracker : IComponentData
    {
        /// <summary>What type of tracked entity this is (for icon/color selection).</summary>
        public TrackedEntityType TrackedType;

        /// <summary>
        /// If true, this entity is always tracked when alive/active.
        /// If false, only tracked when the paradigm profile enables off-screen for this type.
        /// </summary>
        public bool AlwaysTrack;
    }

    /// <summary>
    /// Categories for off-screen indicator icons and colors.
    /// </summary>
    public enum TrackedEntityType : byte
    {
        Boss = 0,           // Skull icon, red
        QuestObjective = 1, // Exclamation mark, yellow
        PartyMember = 2,    // Shield icon, green
        Targeted = 3,       // Crosshair icon, red
        Waypoint = 4,       // Diamond icon, white
        LegendaryLoot = 5   // Star icon, orange
    }
}
