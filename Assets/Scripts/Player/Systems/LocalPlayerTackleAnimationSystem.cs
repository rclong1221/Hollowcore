using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using Player.Components;
using Player.Animation;
using DIG.Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Local player tackle animation system (Epic 7.4.2).
    /// Triggers tackle animations for the local player only.
    /// 
    /// Runs in PresentationSystemGroup (client-side presentation layer).
    /// Tracks previous state to detect transitions.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class LocalPlayerTackleAnimationSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _ghostPresentationSystem;
        
        // Track tackle state per entity to detect transitions
        private struct TrackingState
        {
            public bool WasTackling;
            public bool LastDidHit;
        }
        
        private Dictionary<Entity, TrackingState> _entityStates = new();
        
        protected override void OnCreate()
        {
            _ghostPresentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            
            RequireForUpdate<GhostOwnerIsLocal>();
            RequireForUpdate<TackleState>();
        }
        
        protected override void OnUpdate()
        {
            // Clean up tracking for destroyed entities
            var toRemove = new List<Entity>();
            foreach (var entity in _entityStates.Keys)
            {
                if (!EntityManager.Exists(entity))
                {
                    toRemove.Add(entity);
                }
            }
            foreach (var entity in toRemove)
            {
                _entityStates.Remove(entity);
            }
            
            foreach (var (tackleState, entity) in SystemAPI.Query<RefRO<TackleState>>()
                .WithAll<GhostOwnerIsLocal>()
                .WithEntityAccess())
            {
                // Get presentation GameObject
                var presentationObject = _ghostPresentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentationObject == null)
                    continue;
                
                var bridge = presentationObject.GetComponent<TackleAnimatorBridge>();
                if (bridge == null)
                    continue;
                
                var currentState = tackleState.ValueRO;
                
                // Get or create tracking state for this entity
                if (!_entityStates.TryGetValue(entity, out var trackingState))
                {
                    trackingState = new TrackingState
                    {
                        WasTackling = false,
                        LastDidHit = false
                    };
                }
                
                bool isTackling = currentState.TackleTimeRemaining > 0;
                
                // Detect tackle start
                if (isTackling && !trackingState.WasTackling)
                {
                    bridge.TriggerTackle(currentState.TackleSpeed);
                }
                // Detect tackle end
                else if (!isTackling && trackingState.WasTackling)
                {
                    // Check if hit or miss
                    if (currentState.DidHitTarget || trackingState.LastDidHit)
                    {
                        bridge.TriggerTackleHit();
                    }
                    else
                    {
                        bridge.TriggerTackleMiss();
                    }
                }
                
                // Update tracking state
                trackingState.WasTackling = isTackling;
                trackingState.LastDidHit = currentState.DidHitTarget;
                _entityStates[entity] = trackingState;
            }
        }
    }
}
