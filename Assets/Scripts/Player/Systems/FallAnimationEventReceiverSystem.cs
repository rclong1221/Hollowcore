using System.Collections.Generic;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.Hybrid;
using UnityEngine;
using Player.Bridges;
using DIG.Player.Abilities;

namespace Player.Systems
{
    /// <summary>
    /// EPIC 13.14.3: Fall Animation Event Receiver System
    ///
    /// Bridges animation events from FallAnimatorBridge (MonoBehaviour) to DOTS entities.
    /// When the animator fires OnAnimatorFallComplete, this system enables the
    /// FallAnimationComplete component on the corresponding entity.
    ///
    /// This uses the static event pattern to avoid per-entity polling.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(PlayerAnimatorBridgeSystem))]
    public partial class FallAnimationEventReceiverSystem : SystemBase
    {
        private GhostPresentationGameObjectSystem _presentationSystem;
        private readonly Queue<GameObject> _pendingCompletions = new();

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkStreamInGame>();

            // Subscribe to static event from FallAnimatorBridge
            FallAnimatorBridge.OnFallAnimationCompleteEvent += OnFallAnimationComplete;
        }

        protected override void OnDestroy()
        {
            // Unsubscribe to prevent memory leaks
            FallAnimatorBridge.OnFallAnimationCompleteEvent -= OnFallAnimationComplete;
        }

        private void OnFallAnimationComplete(GameObject presentationObject)
        {
            // Queue for processing in OnUpdate (thread safety)
            _pendingCompletions.Enqueue(presentationObject);
        }

        protected override void OnUpdate()
        {
            if (_presentationSystem == null)
            {
                _presentationSystem = World.GetExistingSystemManaged<GhostPresentationGameObjectSystem>();
                if (_presentationSystem == null)
                    return;
            }

            // Process any pending animation completions
            while (_pendingCompletions.Count > 0)
            {
                var go = _pendingCompletions.Dequeue();
                if (go == null) continue;

                ProcessAnimationComplete(go);
            }
        }

        private void ProcessAnimationComplete(GameObject presentationObject)
        {
            // Find the entity that owns this presentation object
            foreach (var (animState, entity) in
                     SystemAPI.Query<RefRO<PlayerAnimationState>>()
                     .WithAll<PlayerTag>()
                     .WithEntityAccess())
            {
                var presentation = _presentationSystem.GetGameObjectForEntity(EntityManager, entity);
                if (presentation == null) continue;

                // Check if this is the right entity (presentation object matches or is parent/child)
                if (presentation == presentationObject ||
                    presentation.transform.IsChildOf(presentationObject.transform) ||
                    presentationObject.transform.IsChildOf(presentation.transform))
                {
                    // Enable the FallAnimationComplete component to signal the FallDetectionSystem
                    if (EntityManager.HasComponent<FallAnimationComplete>(entity))
                    {
                        EntityManager.SetComponentEnabled<FallAnimationComplete>(entity, true);
                    }

                    return;
                }
            }
        }
    }
}
