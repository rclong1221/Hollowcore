using Unity.Entities;

namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: Per-boss encounter runtime state.
    /// Tracks current phase, timers, add counts, and enrage status.
    /// Managed by EncounterTriggerSystem and PhaseTransitionSystem.
    /// </summary>
    public struct EncounterState : IComponentData
    {
        public byte CurrentPhase;
        public byte PendingPhase;           // Set by triggers, applied by PhaseTransitionSystem
        public float PhaseTimer;            // Time in current phase
        public float EncounterTimer;        // Time since combat started
        public float EnrageTimer;           // Countdown to hard enrage (-1 = no enrage)
        public float EnrageDamageMultiplier;
        public bool IsTransitioning;        // Currently in invulnerability window
        public float TransitionTimer;       // Elapsed transition time
        public float TransitionDuration;    // How long the transition lasts
        public bool IsEnraged;              // Hard enrage active
        public bool EncounterStarted;       // Has the encounter begun (first aggro)

        // Add group trackers (4 groups max)
        public byte AddTracker0Spawned;
        public byte AddTracker0Alive;
        public byte AddTracker1Spawned;
        public byte AddTracker1Alive;
        public byte AddTracker2Spawned;
        public byte AddTracker2Alive;
        public byte AddTracker3Spawned;
        public byte AddTracker3Alive;

        // Ability cast counters (for AbilityCastCount triggers)
        public byte AbilityCastCount0;
        public byte AbilityCastCount1;

        public static EncounterState Default(float enrageTimer, float enrageDamageMultiplier) => new EncounterState
        {
            CurrentPhase = 0,
            PendingPhase = 0,
            EnrageTimer = enrageTimer,
            EnrageDamageMultiplier = enrageDamageMultiplier
        };
    }
}
