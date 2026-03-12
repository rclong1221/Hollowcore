using Unity.Entities;
using Unity.NetCode;

namespace DIG.Interaction
{
    // ─────────────────────────────────────────────────────
    //  EPIC 16.1 Phase 2: Station Sessions & Async Processing
    // ─────────────────────────────────────────────────────

    /// <summary>
    /// How the station UI is presented to the player.
    /// </summary>
    public enum SessionType : byte
    {
        UIPanel = 0,        // Opens a panel overlay (crafting bench, inventory)
        FullScreen = 1,     // Takes over the screen (computer terminal, map)
        WorldSpace = 2      // UI appears in world space near the object (vending machine)
    }

    /// <summary>
    /// EPIC 16.1: Marks an interactable as a station that the player "enters" for a session.
    /// When interacted with, the player enters a UI session (crafting bench, vendor, terminal).
    /// Placed on the STATION entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct InteractionSession : IComponentData
    {
        /// <summary>
        /// How the station UI is presented.
        /// </summary>
        public SessionType SessionType;

        /// <summary>
        /// Whether someone is currently using this station.
        /// </summary>
        [GhostField]
        public bool IsOccupied;

        /// <summary>
        /// The entity currently using this station.
        /// </summary>
        [GhostField]
        public Entity OccupantEntity;

        /// <summary>
        /// Links to managed UI prefab registry. Each unique station type gets a unique ID.
        /// </summary>
        public int SessionID;

        /// <summary>
        /// Whether multiple players can use this station simultaneously.
        /// </summary>
        public bool AllowConcurrentUsers;

        /// <summary>
        /// Lock the player's position while in session.
        /// </summary>
        public bool LockPosition;

        /// <summary>
        /// Disable combat and movement abilities while in session.
        /// Uses existing InteractionAbilityBlockingSystem via BlockedAbilitiesMask.
        /// </summary>
        public bool LockAbilities;

        /// <summary>
        /// Auto-exit if player moves farther than this from the station. 0 = no distance check.
        /// </summary>
        public float MaxDistance;
    }

    /// <summary>
    /// EPIC 16.1: Tracks whether a player is currently in a station session.
    /// Placed on the PLAYER entity (always present, IsInSession = false by default).
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct StationSessionState : IComponentData
    {
        /// <summary>
        /// The station entity this player is currently using.
        /// </summary>
        [GhostField]
        public Entity SessionEntity;

        /// <summary>
        /// Whether the player is actively in a station session.
        /// </summary>
        [GhostField]
        public bool IsInSession;

        /// <summary>
        /// Cached BlockedAbilitiesMask before entering session, restored on exit.
        /// </summary>
        public int PreviousBlockedAbilitiesMask;
    }

    /// <summary>
    /// EPIC 16.1: Enables time-based processing on a station (smelting, fermenting, crafting queues).
    /// Processing continues on the server even when the player walks away.
    /// Placed on the STATION entity.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct AsyncProcessingState : IComponentData
    {
        /// <summary>
        /// Total time required for the current processing batch.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ProcessingTimeTotal;

        /// <summary>
        /// Time elapsed in the current processing batch.
        /// </summary>
        [GhostField(Quantization = 100)]
        public float ProcessingTimeElapsed;

        /// <summary>
        /// Whether the station is actively processing something.
        /// </summary>
        [GhostField]
        public bool IsProcessing;

        /// <summary>
        /// Whether processed output is ready for collection.
        /// </summary>
        [GhostField]
        public bool OutputReady;
    }
}
