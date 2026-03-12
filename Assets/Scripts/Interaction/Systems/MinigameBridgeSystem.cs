using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using DIG.Interaction.Bridges;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 16.1 Phase 5: Managed bridge system for minigame UI lifecycle.
    ///
    /// Handles:
    /// - Detecting MinigameState.IsActive transitions
    /// - Opening/closing minigame UI via MinigameLink registry
    /// - Dequeuing results from managed UI (MinigameResult queue)
    /// - Writing results back to MinigameState
    /// - Ticking TimeRemaining countdown and auto-failing on timeout
    ///
    /// Follows the same pattern as InteractableHybridBridgeSystem and StationSessionBridgeSystem.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class MinigameBridgeSystem : SystemBase
    {
        /// <summary>
        /// Static registry mapping MinigameTypeID → MinigameLink MonoBehaviour.
        /// Populated by MinigameLink.OnEnable/OnDisable.
        /// </summary>
        private static readonly Dictionary<int, MinigameLink> s_Registry = new();

        /// <summary>
        /// Static result queue for managed → ECS callbacks.
        /// MinigameLink.ReportResult enqueues here, this system dequeues.
        /// </summary>
        private static readonly Queue<MinigameResult> s_ResultQueue = new();

        /// <summary>
        /// Track which entities had IsActive=true last frame for transition detection.
        /// </summary>
        private readonly HashSet<Entity> _previouslyActive = new();

        // --- Static API for MinigameLink ---

        public static void RegisterLink(int minigameTypeID, MinigameLink link)
        {
            if (link != null)
            {
                s_Registry[minigameTypeID] = link;
            }
        }

        public static void UnregisterLink(int minigameTypeID)
        {
            s_Registry.Remove(minigameTypeID);
        }

        public static void EnqueueResult(MinigameResult result)
        {
            s_ResultQueue.Enqueue(result);
        }

        protected override void OnUpdate()
        {
            // --- Process results from managed UI ---
            while (s_ResultQueue.Count > 0)
            {
                var result = s_ResultQueue.Dequeue();

                if (result.TargetEntity != Entity.Null &&
                    SystemAPI.HasComponent<MinigameState>(result.TargetEntity))
                {
                    var mgState = SystemAPI.GetComponentRW<MinigameState>(result.TargetEntity);
                    mgState.ValueRW.Succeeded = result.Succeeded;
                    mgState.ValueRW.Failed = !result.Succeeded;
                    mgState.ValueRW.Score = result.Score;
                    mgState.ValueRW.IsActive = false;
                }
            }

            // --- Detect activation/deactivation transitions ---
            var currentlyActive = new HashSet<Entity>();

            foreach (var (mgConfig, mgState, entity) in
                     SystemAPI.Query<RefRO<MinigameConfig>, RefRW<MinigameState>>()
                     .WithEntityAccess())
            {
                if (mgState.ValueRO.IsActive)
                {
                    currentlyActive.Add(entity);

                    if (!_previouslyActive.Contains(entity))
                    {
                        // Just activated — open minigame UI
                        int typeID = mgConfig.ValueRO.MinigameTypeID;
                        if (s_Registry.TryGetValue(typeID, out var link))
                        {
                            link.OpenMinigame(
                                entity,
                                mgConfig.ValueRO.DifficultyLevel,
                                mgConfig.ValueRO.TimeLimit);
                        }
                    }

                    // Tick timeout
                    if (mgConfig.ValueRO.TimeLimit > 0)
                    {
                        mgState.ValueRW.TimeRemaining -= SystemAPI.Time.DeltaTime;

                        if (mgState.ValueRO.TimeRemaining <= 0)
                        {
                            mgState.ValueRW.TimeRemaining = 0;
                            mgState.ValueRW.Failed = true;
                            mgState.ValueRW.IsActive = false;

                            // Close UI
                            int typeID = mgConfig.ValueRO.MinigameTypeID;
                            if (s_Registry.TryGetValue(typeID, out var link))
                            {
                                link.CloseMinigame();
                            }
                        }
                    }
                }
            }

            // Check for deactivation (was active last frame, not this frame)
            foreach (var previousEntity in _previouslyActive)
            {
                if (!currentlyActive.Contains(previousEntity))
                {
                    // Deactivated — close UI if still open
                    if (SystemAPI.HasComponent<MinigameConfig>(previousEntity))
                    {
                        int typeID = SystemAPI.GetComponent<MinigameConfig>(previousEntity).MinigameTypeID;
                        if (s_Registry.TryGetValue(typeID, out var link))
                        {
                            link.CloseMinigame();
                        }
                    }
                }
            }

            _previouslyActive.Clear();
            foreach (var e in currentlyActive)
            {
                _previouslyActive.Add(e);
            }
        }
    }
}
