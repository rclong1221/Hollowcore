using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Consumes animation events from the MonoBehaviour bridge and updates ECS climb state.
    /// 
    /// This system bridges Opsive's animation event system (which fires from MonoBehaviour)
    /// to ECS by polling the static FreeClimbAnimationEvents queue.
    /// 
    /// Event handling:
    /// - StartInPosition: Mount animation complete, clear IsTransitioning, enable input
    /// - Complete: Dismount animation complete, exit climb state entirely
    /// - TurnComplete: Corner turn complete, clear IsTransitioning, resume movement
    /// - HangStartInPosition: Hang transition complete, update hang state
    /// - HangComplete: Pull-up complete, exit hang state
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(FreeClimbMovementSystem))]
    public partial class FreeClimbAnimationEventSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Poll for animation events from the MonoBehaviour bridge
            var animEvent = FreeClimbAnimationEvents.ConsumeEvent();
            
            if (animEvent == FreeClimbAnimationEvents.EventType.None)
                return;
            
            // SERVER-ONLY: Only server modifies climb state from animation events
            // (Client still consumes the event above to keep queue clear)
            bool isServer = World.Unmanaged.IsServer();
            if (!isServer)
                return;
            
            // Process the event for all climbing entities
            foreach (var (climbState, entity) in SystemAPI.Query<RefRW<FreeClimbState>>().WithEntityAccess())
            {
                ref var climb = ref climbState.ValueRW;
                
                switch (animEvent)
                {
                    case FreeClimbAnimationEvents.EventType.StartInPosition:
                        // Mount animation complete - character is in position and ready for input
                        if (climb.IsClimbing && climb.IsTransitioning)
                        {
                            climb.IsTransitioning = false;
                            climb.TransitionProgress = 1f;
                            Debug.Log($"[FreeClimbAnimEventSystem] StartInPosition received - IsTransitioning set to FALSE, input now enabled");
                        }
                        break;
                        
                    case FreeClimbAnimationEvents.EventType.Complete:
                        // Dismount animation complete - exit climb state entirely
                        if (climb.IsClimbing)
                        {
                            climb.IsClimbing = false;
                            climb.IsTransitioning = false;
                            climb.TransitionProgress = 0f;
                            climb.IsClimbingUp = false;
                            climb.IsFreeHanging = false;
                            climb.LastDismountTime = SystemAPI.Time.ElapsedTime;
                            Debug.Log($"[CLIMB_ABORT] Opsive Animation 'Complete' Event received - FORCE DISMOUNTING.");
                        }
                        break;
                        
                    case FreeClimbAnimationEvents.EventType.TurnComplete:
                        // Corner turn complete - resume normal climbing
                        if (climb.IsClimbing && climb.IsTransitioning)
                        {
                            climb.IsTransitioning = false;
                            climb.TransitionProgress = 1f;
                            Debug.Log($"[FreeClimbAnimEventSystem] TurnComplete received - Corner turn done, input enabled");
                        }
                        break;
                        
                    case FreeClimbAnimationEvents.EventType.HangStartInPosition:
                        // EPIC 14.24: Complete hang transition - now in active hang state
                        // This event fires when hang entry animation is in position
                        if (climb.IsHangTransitioning)
                        {
                            climb.IsHangTransitioning = false;
                            climb.IsFreeHanging = true;
                            climb.FreeHangStartTime = SystemAPI.Time.ElapsedTime;
                            Debug.Log($"[FreeClimbAnimEventSystem] HangStartInPosition - Hang active, shimmy/vault input enabled");
                        }
                        // Also handle legacy IsTransitioning for backward compatibility
                        else if (climb.IsTransitioning)
                        {
                            climb.IsTransitioning = false;
                            Debug.Log($"[FreeClimbAnimEventSystem] HangStartInPosition - Opsive Hang transition complete");
                        }
                        break;
                        
                    case FreeClimbAnimationEvents.EventType.HangComplete:
                        // EPIC 14.24: Vault/pull-up complete - exit climb mode entirely
                        // Player is now standing on the ledge with full input restored
                        if (climb.IsClimbing || climb.IsClimbingUp || climb.IsFreeHanging)
                        {
                            climb.IsClimbing = false;
                            climb.IsClimbingUp = false;
                            climb.IsTransitioning = false;
                            climb.IsHangTransitioning = false;
                            climb.IsFreeHanging = false;
                            climb.TransitionProgress = 0f;
                            climb.LastDismountTime = SystemAPI.Time.ElapsedTime;
                            Debug.Log($"[FreeClimbAnimEventSystem] HangComplete - Vault finished, full input restored, exiting climb");
                        }
                        break;
                }
            }
        }
    }
}
