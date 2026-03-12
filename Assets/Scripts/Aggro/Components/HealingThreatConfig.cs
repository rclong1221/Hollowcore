using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.33: Configuration for healing-generated threat.
    /// Add to healer entities. When they heal a target, all enemies fighting
    /// that target add threat for the healer.
    /// </summary>
    public struct HealingThreatConfig : IComponentData
    {
        /// <summary>Threat generated per HP healed. Default 0.5.</summary>
        public float ThreatPerHealPoint;

        /// <summary>If true, divide healing threat among all enemies in combat with healed target.</summary>
        public bool SplitAcrossEnemies;
    }
}
