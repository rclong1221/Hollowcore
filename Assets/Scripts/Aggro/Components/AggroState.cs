using Unity.Entities;

namespace DIG.Aggro.Components
{
    /// <summary>
    /// EPIC 15.19: Runtime state for the aggro system.
    /// Tracks current target and aggro status.
    /// </summary>
    public struct AggroState : IComponentData
    {
        /// <summary>Current highest-threat target (may differ from TargetData.TargetEntity during transition)</summary>
        public Entity CurrentThreatLeader;
        
        /// <summary>Total threat of CurrentThreatLeader (for hysteresis comparison)</summary>
        public float CurrentLeaderThreat;
        
        /// <summary>Whether AI is actively engaged (has any threat entries)</summary>
        public bool IsAggroed;
        
        /// <summary>Time spent without any valid targets (for de-aggro timing)</summary>
        public float TimeSinceLastValidTarget;

        /// <summary>EPIC 15.33: Time since last target switch (for TargetSwitchCooldown).</summary>
        public float TimeSinceLastSwitch;

        /// <summary>Creates default aggro state (not aggroed).</summary>
        public static AggroState Default => new AggroState
        {
            CurrentThreatLeader = Entity.Null,
            CurrentLeaderThreat = 0f,
            IsAggroed = false,
            TimeSinceLastValidTarget = 0f,
            TimeSinceLastSwitch = 0f
        };
    }
}
