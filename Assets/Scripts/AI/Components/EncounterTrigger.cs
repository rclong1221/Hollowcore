namespace DIG.AI.Components
{
    /// <summary>
    /// EPIC 15.32: Trigger condition types for encounter scripting.
    /// Evaluated by EncounterTriggerSystem each frame.
    /// </summary>
    public enum TriggerConditionType : byte
    {
        HPBelow = 0,           // Boss HP% drops below threshold
        HPAbove = 1,           // Boss HP% rises above threshold (heal mechanics)
        TimerElapsed = 2,      // Seconds since encounter start or phase start
        AddsDead = 3,          // N adds from a spawn group have died
        AddsAlive = 4,         // N adds from a spawn group are still alive
        PlayerCountInRange = 5,// N+ players within specified range
        AbilityCastCount = 6,  // Ability X has been cast N times this phase
        PhaseIs = 7,           // Current phase equals value (for composite gates)
        BossAtPosition = 8,    // Boss within range of a world position
        Composite_AND = 9,     // All referenced sub-triggers must be true
        Composite_OR = 10,     // Any referenced sub-trigger must be true
        Manual = 11            // Fired by other systems / external scripts
    }

    /// <summary>
    /// EPIC 15.32: Actions fired when a trigger condition is met.
    /// </summary>
    public enum TriggerActionType : byte
    {
        TransitionPhase = 0,   // Move to specified phase
        ForceAbility = 1,      // Immediately select and begin casting ability by ID
        SpawnAddGroup = 2,     // Spawn a group of adds by SpawnGroupId
        SetInvulnerable = 3,   // Toggle invulnerability for duration
        Teleport = 4,          // Move boss to world position
        ModifyStats = 5,       // Apply speed/damage multiplier
        PlayVFX = 6,           // Spawn VFX entity (presentation bridge)
        PlayDialogue = 7,      // Queue dialogue/yell text
        SetEnrage = 8,         // Enable hard enrage mode
        DestroyAdds = 9,       // Kill all adds from a spawn group
        ResetCooldowns = 10,   // Reset all ability cooldowns
        EnableTrigger = 11,    // Enable another trigger by index (chaining)
        DisableTrigger = 12,   // Disable another trigger by index

        // EPIC 15.33: Threat manipulation actions
        ThreatWipeAll = 13,        // Clear ALL ThreatEntry buffers (boss phase transition)
        ThreatMultiplyAll = 14,    // Multiply all ThreatValue entries by ActionValue
        ThreatFixateRandom = 15,   // Fixate on random player for ActionValue seconds

        // EPIC 17.5: Music override
        PlayMusic = 16,            // Force boss music track (ActionValue = TrackId)

        // EPIC 17.9: Cinematic trigger
        PlayCinematic = 17         // Trigger cinematic playback (ActionParam = CinematicId)
    }
}
