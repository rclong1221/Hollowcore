using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 7: Cooperative Interactions
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// How players cooperate on this interaction.
    /// </summary>
    public enum CoopMode : byte
    {
        /// <summary>All players must act within a sync window (dual key turn).</summary>
        Simultaneous = 0,
        /// <summary>Players take turns in slot order (relay race).</summary>
        Sequential = 1,
        /// <summary>All players channel at the same time (team revive).</summary>
        Parallel = 2,
        /// <summary>Different players perform different roles (one hacks, one defends).</summary>
        Asymmetric = 3
    }

    /// <summary>
    /// EPIC 16.1 Phase 7: Configuration and state for a cooperative interaction.
    /// Placed on the INTERACTABLE entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct CoopInteraction : IComponentData
    {
        /// <summary>How many players are needed to complete this interaction.</summary>
        public int RequiredPlayers;

        /// <summary>How many players have currently joined.</summary>
        [GhostField]
        public int CurrentPlayers;

        /// <summary>How cooperation works for this interaction.</summary>
        public CoopMode Mode;

        /// <summary>Max seconds between players' Use inputs (Simultaneous mode).</summary>
        public float SyncTolerance;

        /// <summary>How long all players must channel together (Parallel mode).</summary>
        public float ChannelDuration;

        /// <summary>Progress of parallel channeling (0 to 1).</summary>
        [GhostField(Quantization = 100)]
        public float ChannelProgress;

        /// <summary>All required slots are filled.</summary>
        [GhostField]
        public bool AllPlayersReady;

        /// <summary>The cooperative interaction completed successfully.</summary>
        [GhostField]
        public bool CoopComplete;

        /// <summary>The interaction failed (player left, sync tolerance exceeded).</summary>
        [GhostField]
        public bool CoopFailed;

        /// <summary>Which slot is currently active (Sequential mode, 0-based).</summary>
        [GhostField]
        public int CurrentSequenceSlot;
    }

    /// <summary>
    /// EPIC 16.1 Phase 7: A slot for a player in a cooperative interaction.
    /// Buffer placed on the INTERACTABLE entity (NOT on ghost-replicated players).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CoopSlot : IBufferElementData
    {
        /// <summary>The player occupying this slot.</summary>
        public Entity PlayerEntity;

        /// <summary>Which role/position this slot represents (0-based).</summary>
        public int SlotIndex;

        /// <summary>Local-space position where this player stands.</summary>
        public float3 SlotPosition;

        /// <summary>Local-space facing direction for this player.</summary>
        public quaternion SlotRotation;

        /// <summary>Whether this slot is filled by a player.</summary>
        public bool IsOccupied;

        /// <summary>Whether the player in this slot has confirmed/started their part.</summary>
        public bool IsReady;

        /// <summary>Elapsed time when the player pressed ready (for sync tolerance checking).</summary>
        public float ReadyTimestamp;
    }

    /// <summary>
    /// EPIC 16.1 Phase 7: Tracks a player's participation in a cooperative interaction.
    /// Placed on the PLAYER entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct CoopParticipantState : IComponentData
    {
        /// <summary>Which coop interactable the player has joined.</summary>
        [GhostField]
        public Entity CoopEntity;

        /// <summary>Which slot index the player is assigned to.</summary>
        [GhostField]
        public int AssignedSlot;

        /// <summary>Whether the player is actively in a coop interaction.</summary>
        [GhostField]
        public bool IsInCoop;

        /// <summary>Whether the player has pressed confirm for the ready check.</summary>
        [GhostField]
        public bool HasConfirmed;
    }

    /// <summary>
    /// UI notification for cooperative interaction state changes.
    /// </summary>
    public struct CoopUIEvent
    {
        public Entity CoopEntity;
        public int CurrentPlayers;
        public int RequiredPlayers;
        public bool AllReady;
        public bool Complete;
        public bool Failed;
        public float ChannelProgress;
    }
}
