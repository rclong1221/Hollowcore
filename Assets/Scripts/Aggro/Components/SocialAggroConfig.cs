using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Per-entity configuration for social/group aggro behaviors.
    /// Separate from AggroConfig so solo enemies don't pay the archetype cost.
    /// Add SocialAggroAuthoring to enemy prefabs that need group behavior.
    /// </summary>
    public struct SocialAggroConfig : IComponentData
    {
        /// <summary>Group ID for linked-pull behavior. 0 = no group. Same ID = linked pull.</summary>
        public int EncounterGroupId;

        /// <summary>Bitmask of enabled social behaviors.</summary>
        public SocialAggroFlags Flags;

        /// <summary>Radius within which call-for-help is heard. 0 = silent.</summary>
        public float CallForHelpRadius;

        /// <summary>Minimum seconds between call-for-help emissions.</summary>
        public float CallForHelpCooldown;

        /// <summary>Fraction of own threat shared in call-for-help (0-1).</summary>
        public float CallForHelpThreatShare;

        /// <summary>Flat threat bonus added to killer when an ally dies.</summary>
        public float AllyDeathThreatBonus;

        /// <summary>Multiply all threat entries by this when an ally dies. 1.0 = no change.</summary>
        public float AllyDeathRageMultiplier;

        /// <summary>When ally takes damage, share this fraction of threat to nearby allies. 0 = disabled.</summary>
        public float AllyDamagedThreatShare;

        /// <summary>Pack hierarchy role for this entity.</summary>
        public PackRole Role;

        public static SocialAggroConfig Default => new SocialAggroConfig
        {
            EncounterGroupId = 0,
            Flags = SocialAggroFlags.None,
            CallForHelpRadius = 25f,
            CallForHelpCooldown = 3f,
            CallForHelpThreatShare = 0.5f,
            AllyDeathThreatBonus = 50f,
            AllyDeathRageMultiplier = 1f,
            AllyDamagedThreatShare = 0f,
            Role = PackRole.None
        };
    }
}
