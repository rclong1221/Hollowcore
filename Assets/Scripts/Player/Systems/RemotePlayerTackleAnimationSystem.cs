using Unity.Collections;
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
    /// Remote player tackle animation system (Epic 7.4.2).
    /// Triggers tackle animations for remote players (not locally owned).
    /// 
    /// Runs in PresentationSystemGroup (client-side presentation layer).
    /// Tracks state per-entity to detect transitions and clean up destroyed entities.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class RemotePlayerTackleAnimationSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _ghostPresentationSystem;
        
        // Track tackle state per entity to detect transitions
        private struct TrackingState
        {
            public bool WasTackling;
            public bool LastDidHit;
        }
        
        private NativeHashMap<Entity, TrackingState> _entityStates;
        
        protected override void OnCreate()
        {
            _ghostPresentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
            _entityStates = new NativeHashMap<Entity, TrackingState>(64, Allocator.Persistent);
            
            RequireForUpdate<TackleState>();
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
            foreach (var (tackleState, entity) in SystemAPI.Query<RefRO<TackleState>>()
                .WithNone<GhostOwnerIsLocal>()
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
