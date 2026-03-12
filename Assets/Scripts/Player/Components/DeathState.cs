using Unity.Entities;
using Unity.NetCode;

namespace Player.Components
{
    /// <summary>
    /// Phase of death for state machine transitions.
    /// </summary>
    public enum DeathPhase : byte
    {
        Alive = 0,      // Normal gameplay
        Downed = 1,     // Optional: can be revived (future Epic)
        Dead = 2,       // Fully dead, awaiting respawn
        Respawning = 3  // In respawn process (Epic 4.5)
    }

    /// <summary>
    /// Death state component. Tracks whether the player is alive, downed, dead, or respawning.
    /// Server-authoritative with replication to clients for UI/effects.
    /// </summary>
    /// <remarks>
    /// Design goals (EPIC 4.1):
    /// - DeathTransitionSystem transitions Alive → Dead when Health.Current <= 0
    /// - Handles invulnerability windows to prevent repeat transitions
    /// - Replicated so clients can show death UI/effects
    /// - RespawnSystem (Epic 4.5) will transition Dead → Respawning → Alive
    ///
    /// NOTE: Using GhostPrefabType.All to replicate to INTERPOLATED ghosts (non-owned players).
    /// AllPredicted only replicates to predicted ghosts (owned players), but DeathState.Phase
    /// needs to be visible to all clients so they know when other players are dead.
    /// </remarks>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct DeathState : IComponentData
    {
        /// <summary>
        /// Current death phase.
        /// </summary>
        [GhostField]
        public DeathPhase Phase;

        /// <summary>
        /// Time when the current state started (for timing respawn, downed duration, etc.).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float StateStartTime;

        /// <summary>
        /// Delay in seconds before respawn is allowed (configurable per game mode).
        /// </summary>
        public float RespawnDelay;

        /// <summary>
        /// Invulnerability window after respawn to prevent immediate re-death.
        /// </summary>
        public float InvulnerabilityDuration;

        /// <summary>
        /// Time when invulnerability expires (0 if not invulnerable).
        /// </summary>
        [GhostField(Quantization = 100)]
        public float InvulnerabilityEndTime;

        /// <summary>
        /// Check if currently invulnerable.
        /// </summary>
        public bool IsInvulnerable(float currentTime) => currentTime < InvulnerabilityEndTime;

        /// <summary>
        /// Check if can respawn (respawn delay has passed).
        /// </summary>
        public bool CanRespawn(float currentTime) =>
            Phase == DeathPhase.Dead && (currentTime - StateStartTime) >= RespawnDelay;

        /// <summary>
        /// Default values for a living player.
        /// </summary>
        public static DeathState Default => new DeathState
        {
            Phase = DeathPhase.Alive,
            StateStartTime = 0f,
            RespawnDelay = 5f, // 5 seconds default
            InvulnerabilityDuration = 3f, // 3 seconds post-respawn
            InvulnerabilityEndTime = 0f
        };
    }
}
