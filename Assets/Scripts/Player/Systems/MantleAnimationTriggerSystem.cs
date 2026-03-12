using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using Player.Bridges;
using Player.Components;
using UnityEngine;

namespace Player.Systems
{
    /// <summary>
    /// Client-only system that monitors MantleState changes and triggers MantleAnimatorBridge animations.
    /// Runs in PresentationSystemGroup to bridge DOTS gameplay state to Unity Animator.
    /// Handles both local and remote players.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class MantleAnimationTriggerSystem : SystemBase
    {
        public bool EnableDebugLog = false;
        
        private GhostPresentationGameObjectSystem _presentationSystem;
        private readonly Dictionary<Entity, byte> _lastIsActive = new();
        private readonly Dictionary<Entity, uint> _lastStartTick = new();

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();
            _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }

            // Process FreeClimbState based mantling (EPIC 15.3)
            foreach (var (climbState, settings, entity) in SystemAPI.Query<RefRO<FreeClimbState>, RefRO<FreeClimbSettings>>().WithAll<PlayerTag>().WithEntityAccess())
            {
                var state = climbState.ValueRO;
                var cfg = settings.ValueRO;
                
                // Track state transitions
                _lastIsActive.TryGetValue(entity, out var lastActive);
                bool isNewAction = false;
                bool actionEnded = false;
                
                // 1 = Mantle, 2 = Vault. FreeClimbState only has IsMantling (1)
                // We use IsMantling=true AND IsTransitioning=true to denote active state
                byte currentActive = 0;
                if (state.IsMantling && state.IsTransitioning) currentActive = 1;
                
                if (currentActive > 0)
                {
                    if (lastActive == 0)
                    {
                        isNewAction = true;
                    }
                    else if (_lastStartTick.TryGetValue(entity, out var lastTick) && lastTick != (uint)state.TransitionStartTime && state.TransitionStartTime != 0) 
                    {
                        // Detect re-trigger? (Using TransitionStartTime as tick surrogate)
                         isNewAction = true;
                    }
                }
                else if (lastActive > 0)
                {
                    actionEnded = true;
                }
                
                // Update tracking
                _lastIsActive[entity] = currentActive;
                if (currentActive > 0)
                    _lastStartTick[entity] = (uint)state.TransitionStartTime; // StartTime is double, casting to uint, might be messy but works for change detection
                    
                // Get Presentation
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null) continue;
                
                var bridge = presentation.GetComponentInChildren<MantleAnimatorBridge>();
                if (bridge == null) continue;
                
                if (isNewAction)
                {
                    // Calculate Duration
                    float speed = cfg.MountTransitionSpeed > 0 ? cfg.MountTransitionSpeed : 2.5f;
                    float duration = 1.0f / speed;
                    
                    Vector3 endPos = state.TransitionTargetPos;
                    bridge.TriggerMantle(endPos, duration);
                    
                    if (EnableDebugLog) Debug.Log($"[MantleAnimTrigger] ✓ Triggered MANTLE (FreeClimb) Entity {entity.Index}");
                }
                else if (actionEnded)
                {
                    bridge.EndMantle();
                    if (EnableDebugLog) Debug.Log($"[MantleAnimTrigger] Ended mantle (FreeClimb) Entity {entity.Index}");
                }
            }

            // Existing MantleState Logic (Legacy/Parallel)
            foreach (var (mantleState, entity) in SystemAPI.Query<RefRO<MantleState>>().WithAll<PlayerTag>().WithEntityAccess())
            {
                // Skip if we already processed this entity via FreeClimbState (avoid double trigger)
                if (SystemAPI.HasComponent<FreeClimbState>(entity))
                {
                     var fc = SystemAPI.GetComponent<FreeClimbState>(entity);
                     if (fc.IsMantling) continue;
                }

                var state = mantleState.ValueRO;
                // ... (Original Logic)
                _lastIsActive.TryGetValue(entity, out var lastActive);
                bool isNewAction = false;
                bool actionEnded = false;

                if (state.IsActive > 0)
                {
                    if (lastActive == 0) isNewAction = true;
                    else if (_lastStartTick.TryGetValue(entity, out var lastTick) && lastTick != state.StartTick && state.StartTick != 0) isNewAction = true;
                }
                else if (lastActive > 0) actionEnded = true;

                _lastIsActive[entity] = state.IsActive;
                if (state.IsActive > 0) _lastStartTick[entity] = state.StartTick;

                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null) continue;
                var bridge = presentation.GetComponentInChildren<MantleAnimatorBridge>();
                if (bridge == null) continue;

                if (isNewAction)
                {
                    if (state.IsActive == 2) bridge.TriggerVault(state.Duration);
                    else if (state.IsActive == 1) bridge.TriggerMantle(state.EndPosition, state.Duration);
                }
                else if (actionEnded)
                {
                    bridge.EndMantle();
                    bridge.EndVault();
                }
            }

            // Clean up
            var entitiesToRemove = new List<Entity>();
            foreach (var entity in _lastIsActive.Keys)
            {
                bool hasMantle = EntityManager.HasComponent<MantleState>(entity);
                bool hasFreeClimb = EntityManager.HasComponent<FreeClimbState>(entity);
                
                if (!EntityManager.Exists(entity) || (!hasMantle && !hasFreeClimb))
                {
                    entitiesToRemove.Add(entity);
                }
            }
             foreach (var entity in entitiesToRemove)
            {
                _lastIsActive.Remove(entity);
                _lastStartTick.Remove(entity);
            }
        }
    }
}
