using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace DIG.Ship.Airlocks
{
    /// <summary>
    /// Client-side system that triggers screen effects during airlock transitions.
    /// Adds visual feedback for environment changes (vacuum ↔ pressurized).
    /// </summary>
    /// <remarks>
    /// Implements Sub-Epic 3.1.5: Presentation (Screen FX)
    /// - Helmet HUD activation when exiting to vacuum
    /// - Screen flash/fade during teleport
    /// - Optional post-process effects
    /// </remarks>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(AirlockUISystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial class AirlockScreenFXSystem : SystemBase
    {
        private bool _wasTransitioning;
        private AirlockDirection _lastDirection;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            bool isTransitioning = false;
            AirlockDirection direction = AirlockDirection.EnterShip;
            float progress = 0f;

            // Check if local player is transitioning
            foreach (var (transition, playerState, entity) in
                     SystemAPI.Query<RefRO<AirlockTransitionPending>, RefRO<PlayerState>>()
                     .WithAll<GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                isTransitioning = true;
                direction = transition.ValueRO.Direction;

                Entity airlockEntity = transition.ValueRO.AirlockEntity;
                if (SystemAPI.HasComponent<Airlock>(airlockEntity))
                {
                    var airlock = SystemAPI.GetComponent<Airlock>(airlockEntity);
                    progress = airlock.CycleTime > 0 
                        ? airlock.CycleProgress / airlock.CycleTime 
                        : 0f;
                }
                break;
            }

            // Detect transition start
            if (isTransitioning && !_wasTransitioning)
            {
                _lastDirection = direction;
                OnTransitionStart(direction);
            }

            // Detect transition end
            if (!isTransitioning && _wasTransitioning)
            {
                OnTransitionComplete(_lastDirection);
            }

            // Update during transition
            if (isTransitioning)
            {
                OnTransitionProgress(direction, progress);
            }

            _wasTransitioning = isTransitioning;
        }

        private void OnTransitionStart(AirlockDirection direction)
        {
            var fxController = Object.FindAnyObjectByType<AirlockScreenFXController>();
            if (fxController == null) return;

            if (direction == AirlockDirection.ExitShip)
            {
                // Exiting to vacuum - prepare helmet HUD
                fxController.BeginVacuumTransition();
            }
            else
            {
                // Entering ship - begin depressurization visual
                fxController.BeginPressurizeTransition();
            }
        }

        private void OnTransitionProgress(AirlockDirection direction, float progress)
        {
            var fxController = Object.FindAnyObjectByType<AirlockScreenFXController>();
            if (fxController == null) return;

            fxController.UpdateTransitionProgress(progress);

            // Near completion - prepare for teleport flash
            if (progress > 0.9f)
            {
                fxController.PrepareTeleportFlash();
            }
        }

        private void OnTransitionComplete(AirlockDirection direction)
        {
            var fxController = Object.FindAnyObjectByType<AirlockScreenFXController>();
            if (fxController == null) return;

            // Trigger teleport flash
            fxController.TriggerTeleportFlash();

            if (direction == AirlockDirection.ExitShip)
            {
                // Now in vacuum - activate EVA HUD
                fxController.ActivateEVAMode();
            }
            else
            {
                // Now in ship - deactivate EVA HUD
                fxController.DeactivateEVAMode();
            }
        }
    }
}
