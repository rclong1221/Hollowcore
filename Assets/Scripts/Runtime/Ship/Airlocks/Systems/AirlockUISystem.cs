using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Client-side system that updates UI elements during airlock cycling.
    /// Shows progress bar and status text while player is transitioning.
    /// </summary>
    /// <remarks>
    /// Implements Sub-Epic 3.1.5: Presentation (UI Progress Indicator)
    /// - Progress bar during cycle
    /// - Status text (Depressurizing... / Pressurizing...)
    /// - Countdown timer
    /// </remarks>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial class AirlockUISystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            // Find local player with pending transition
            foreach (var (transition, playerState, entity) in
                     SystemAPI.Query<RefRO<AirlockTransitionPending>, RefRO<PlayerState>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                Entity airlockEntity = transition.ValueRO.AirlockEntity;
                
                // Get airlock data
                if (!SystemAPI.HasComponent<Airlock>(airlockEntity))
                    continue;

                var airlock = SystemAPI.GetComponent<Airlock>(airlockEntity);

                // Calculate progress
                float progress = airlock.CycleTime > 0 
                    ? airlock.CycleProgress / airlock.CycleTime 
                    : 0f;

                float timeRemaining = airlock.CycleTime - airlock.CycleProgress;

                // Determine status text
                string statusText = airlock.State switch
                {
                    AirlockState.CyclingToExterior => "DEPRESSURIZING...",
                    AirlockState.CyclingToInterior => "PRESSURIZING...",
                    _ => "CYCLING..."
                };

                // Update UI
                var uiController = Object.FindAnyObjectByType<AirlockUIController>();
                if (uiController != null)
                {
                    uiController.ShowProgress(progress, statusText, timeRemaining);
                }
            }

            // Hide UI if no local player is transitioning
            bool anyTransitioning = false;
            foreach (var (transition, entity) in
                     SystemAPI.Query<RefRO<AirlockTransitionPending>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                anyTransitioning = true;
                break;
            }

            if (!anyTransitioning)
            {
                var uiController = Object.FindAnyObjectByType<AirlockUIController>();
                if (uiController != null)
                {
                    uiController.HideProgress();
                }
            }
        }
    }
}
