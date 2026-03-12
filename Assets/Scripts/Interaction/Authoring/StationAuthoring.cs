using Unity.Entities;
using UnityEngine;

namespace DIG.Interaction.Authoring
{
    /// <summary>
    /// EPIC 16.1 Phase 2: Authoring component for station interactables.
    ///
    /// Bakes InteractionSession (and optionally AsyncProcessingState) onto the station entity.
    /// Use alongside InteractableAuthoring for the base Interactable component.
    ///
    /// Designer workflow:
    /// 1. Add InteractableAuthoring (Type = Instant, Verb = Use/Craft)
    /// 2. Add StationAuthoring (configure session type, locking, etc.)
    /// 3. Optionally add StationUILink for managed UI registration
    /// </summary>
    public class StationAuthoring : MonoBehaviour
    {
        [Header("Session Configuration")]
        [Tooltip("How the station UI is presented to the player")]
        public SessionType SessionType = SessionType.UIPanel;

        [Tooltip("Unique ID linking to managed UI prefab registry (must match StationUILink.SessionID)")]
        public int SessionID;

        [Tooltip("Allow multiple players to use this station at once")]
        public bool AllowConcurrentUsers = false;

        [Header("Player Constraints")]
        [Tooltip("Lock the player's position while in session")]
        public bool LockPosition = true;

        [Tooltip("Disable combat and movement abilities while in session")]
        public bool LockAbilities = true;

        [Tooltip("Auto-exit if player moves farther than this from the station (0 = no distance check)")]
        public float MaxDistance = 5f;

        [Header("Async Processing (Optional)")]
        [Tooltip("Enable time-based processing (smelting, fermenting, crafting queues)")]
        public bool EnableAsyncProcessing = false;

        public class Baker : Baker<StationAuthoring>
        {
            public override void Bake(StationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new InteractionSession
                {
                    SessionType = authoring.SessionType,
                    IsOccupied = false,
                    OccupantEntity = Entity.Null,
                    SessionID = authoring.SessionID,
                    AllowConcurrentUsers = authoring.AllowConcurrentUsers,
                    LockPosition = authoring.LockPosition,
                    LockAbilities = authoring.LockAbilities,
                    MaxDistance = authoring.MaxDistance
                });

                if (authoring.EnableAsyncProcessing)
                {
                    AddComponent(entity, new AsyncProcessingState
                    {
                        ProcessingTimeTotal = 0f,
                        ProcessingTimeElapsed = 0f,
                        IsProcessing = false,
                        OutputReady = false
                    });
                }
            }
        }
    }
}
