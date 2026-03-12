using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// Client-side presentation system that triggers slide animations
    /// for all players (both local and remote).
    /// Updates animator parameters when slide state changes.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class SlideAnimationTriggerSystem : SystemBase
    {
        public bool EnableDebugLog = false; // Disabled to reduce spam
        
        private GhostPresentationGameObjectSystem _presentationSystem;
        private readonly Dictionary<Entity, bool> _lastIsSliding = new();
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

            // Check for entities with SlideState and trigger animation
            int entityCount = 0;
            foreach (var (slideState, entity) in SystemAPI.Query<RefRO<SlideState>>().WithAll<PlayerTag>().WithEntityAccess())
            {
                entityCount++;
                var slide = slideState.ValueRO;
                
                // Track state transitions to detect slide start/end
                _lastIsSliding.TryGetValue(entity, out var wasSliding);
                bool isNewSlide = false;
                bool slideEnded = false;

                // Detect new slide by checking:
                // 1. IsSliding went from false to true (state transition)
                // 2. OR StartTick changed while IsSliding (handles late replication)
                if (slide.IsSliding)
                {
                    if (!wasSliding)
                    {
                        // IsSliding transition: false -> true
                        isNewSlide = true;
                        if (EnableDebugLog)
                        {
                            // Debug.Log($"[SlideAnimTrigger] Entity {entity.Index} started sliding, speed={slide.CurrentSpeed:F2}, triggerType={slide.TriggerType}");
                        }
                    }
                    else if (_lastStartTick.TryGetValue(entity, out var lastTick) && lastTick != slide.StartTick && slide.StartTick != 0)
                    {
                        // StartTick changed (late replication or packet recovery)
                        isNewSlide = true;
                        if (EnableDebugLog)
                        {
                            // Debug.Log($"[SlideAnimTrigger] Entity {entity.Index} StartTick changed {lastTick}->{slide.StartTick}");
                        }
                    }
                }
                else if (wasSliding)
                {
                    // IsSliding transition: true -> false (slide ended)
                    slideEnded = true;
                    if (EnableDebugLog)
                    {
                        // Debug.Log($"[SlideAnimTrigger] Entity {entity.Index} stopped sliding");
                    }
                }

                // Update tracking
                _lastIsSliding[entity] = slide.IsSliding;
                if (slide.IsSliding)
                    _lastStartTick[entity] = slide.StartTick;

                // Get the presentation GameObject
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null)
                    continue;

                var bridge = presentation.GetComponentInChildren<Player.Bridges.SlideAnimatorBridge>();
                if (bridge == null)
                {
                    if (isNewSlide && EnableDebugLog)
                    {
                        // Debug.LogWarning($"[SlideAnimationTriggerSystem] ✗ No SlideAnimatorBridge on entity {entity.Index}");
                    }
                    continue;
                }

                if (isNewSlide)
                {
                    bridge.StartSlide(slide.CurrentSpeed, (int)slide.TriggerType);
                    if (EnableDebugLog)
                    {
                        // Debug.Log($"[SlideAnimationTriggerSystem] ✓ Started slide for entity {entity.Index}, speed={slide.CurrentSpeed:F2}, triggerType={slide.TriggerType}");
                    }
                }
                else if (slide.IsSliding)
                {
                    // Update speed while sliding
                    bridge.UpdateSlideSpeed(slide.CurrentSpeed);
                }
                else if (slideEnded)
                {
                    bridge.EndSlide();
                    if (EnableDebugLog)
                    {
                        // Debug.Log($"[SlideAnimationTriggerSystem] ✓ Ended slide for entity {entity.Index}");
                    }
                }
            }

            // Clean up tracking for entities that no longer exist
            var entitiesToRemove = new List<Entity>();
            foreach (var entity in _lastIsSliding.Keys)
            {
                if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<SlideState>(entity))
                {
                    entitiesToRemove.Add(entity);
                }
            }
            foreach (var entity in entitiesToRemove)
            {
                _lastIsSliding.Remove(entity);
                _lastStartTick.Remove(entity);
            }
            
            if (EnableDebugLog && entityCount == 0)
            {
                // Debug.LogWarning($"[SlideAnimTrigger] No entities with SlideState + PlayerTag found!");
            }
        }
    }
}
