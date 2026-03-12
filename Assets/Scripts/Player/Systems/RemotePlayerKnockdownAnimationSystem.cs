using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using DIG.Player.Components;
using Player.Animation;

namespace Player.Systems
{
    /// <summary>
    /// Remote player stagger/knockdown animation system (Epic 7.4.1).
    /// Triggers stagger and knockdown animations for remote players (not locally owned).
    /// 
    /// Runs in PresentationSystemGroup (client-side presentation layer).
    /// Tracks state per-entity to detect transitions and clean up destroyed entities.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class RemotePlayerKnockdownAnimationSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _ghostPresentationSystem;
        
        // Track stagger/knockdown state per entity to detect transitions
        private struct TrackingState
        {
            public bool WasStaggered;
            public bool WasKnockedDown;
            public bool WasRecovering;
            public float LastKnockdownTime;
        }
        
        private NativeHashMap<Entity, TrackingState> _entityStates;
        
        protected override void OnCreate()
        {
            _ghostPresentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            _entityStates = new NativeHashMap<Entity, TrackingState>(64, Allocator.Persistent);
            
            RequireForUpdate<PlayerCollisionState>();
        }
        
        protected override void OnDestroy()
        {
            if (_entityStates.IsCreated)
            {
                _entityStates.Dispose();
            }
        }
        
        protected override void OnUpdate()
        {
            // Clean up tracking for destroyed entities
            var allEntities = _entityStates.GetKeyArray(Allocator.Temp);
            foreach (var entity in allEntities)
            {
                if (!EntityManager.Exists(entity))
                {
                    _entityStates.Remove(entity);
                }
            }
            allEntities.Dispose();
            
            // Process remote players (not locally owned)
            foreach (var (collisionState, entity) in SystemAPI.Query<RefRO<PlayerCollisionState>>()
                .WithNone<GhostOwnerIsLocal>()
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
                
                // Get or create tracking state for this entity
                if (!_entityStates.TryGetValue(entity, out var trackingState))
                {
                    trackingState = new TrackingState
                    {
                        WasStaggered = false,
                        WasKnockedDown = false,
                        WasRecovering = false,
                        LastKnockdownTime = 0
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
                trackingState.LastKnockdownTime = currentState.KnockdownTimeRemaining;
                _entityStates[entity] = trackingState;
            }
        }
    }
}
