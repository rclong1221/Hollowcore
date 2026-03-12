using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using DIG.Player.Components;
using Player.Animation;

namespace Player.Systems
{
    /// <summary>
    /// Local player stagger/knockdown animation system (Epic 7.4.1).
    /// Triggers stagger and knockdown animations for the local player only.
    /// 
    /// Runs in PresentationSystemGroup (client-side presentation layer).
    /// Tracks previous state to detect transitions and avoid re-triggering on every frame.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class LocalPlayerKnockdownAnimationSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _ghostPresentationSystem;
        
        // Track stagger/knockdown state per entity to detect transitions
        private struct TrackingState
        {
            public bool WasStaggered;
            public bool WasKnockedDown;
            public bool WasRecovering;
        }
        
        private Dictionary<Entity, TrackingState> _entityStates = new();
        
        protected override void OnCreate()
        {
            _ghostPresentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            
            // Process ALL players, not just local - remote players also need knockdown animations
            RequireForUpdate<PlayerCollisionState>();
            

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
            
            // Query ALL entities with PlayerCollisionState (not just local player)
            foreach (var (collisionState, entity) in SystemAPI.Query<RefRO<PlayerCollisionState>>()
                .WithEntityAccess())
            {
                // Get presentation GameObject
                var presentationObject = _ghostPresentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentationObject == null)
                    continue;
                
                var bridge = presentationObject.GetComponent<KnockdownAnimatorBridge>();
                if (bridge == null)
                    continue;
                
                var currentState = collisionState.ValueRO;
                
                // DEBUG: Log collision state every few frames (Removed to reduce spam)
                // if (UnityEngine.Time.frameCount % 60 == 0)
                // {
                //      Debug.Log($"[LocalKnockdownAnim] ... ");
                // }
                
                // Get or create tracking state for this entity
                if (!_entityStates.TryGetValue(entity, out var trackingState))
                {
                    trackingState = new TrackingState
                    {
                        WasStaggered = false,
                        WasKnockedDown = false,
                        WasRecovering = false
                    };
                }
                
                bool isStaggered = currentState.StaggerTimeRemaining > 0;
                bool isKnockedDown = currentState.KnockdownTimeRemaining > 0;
                bool isRecovering = currentState.IsRecoveringFromKnockdown;
                
                // === STAGGER TRANSITIONS ===
                // Detect stagger start (wasn't staggered, now is)
                if (isStaggered && !trackingState.WasStaggered)
                {
                    bridge.TriggerStagger(currentState.StaggerIntensity);
                }
                // Detect stagger end (was staggered, now not)
                else if (!isStaggered && trackingState.WasStaggered)
                {
                    bridge.EndStagger();
                }
                
                // === KNOCKDOWN TRANSITIONS ===
                // Detect knockdown start (wasn't knocked down, now is)
                if (isKnockedDown && !trackingState.WasKnockedDown)
                {

                    bridge.TriggerKnockdown(currentState.KnockdownImpactSpeed);
                }
                // Detect recovery start (was knocked down, now recovering)
                else if (isRecovering && !trackingState.WasRecovering && trackingState.WasKnockedDown)
                {

                    bridge.StartRecovery();
                }
                // Detect knockdown end (was in knockdown/recovery, now neither)
                else if (!isKnockedDown && !isRecovering && (trackingState.WasKnockedDown || trackingState.WasRecovering))
                {

                    bridge.EndKnockdown();
                }
                
                // Update tracking state
                trackingState.WasStaggered = isStaggered;
                trackingState.WasKnockedDown = isKnockedDown;
                trackingState.WasRecovering = isRecovering;
                _entityStates[entity] = trackingState;
            }
        }
    }
}
