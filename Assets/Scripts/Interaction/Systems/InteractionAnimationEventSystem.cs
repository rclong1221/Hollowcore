using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Interaction.Bridges;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 13.17.1: Receives animation events from InteractionAnimatorBridge.
    ///
    /// This system bridges the gap between MonoBehaviour animation events and ECS.
    /// It subscribes to static events from InteractionAnimatorBridge and queues them
    /// for processing in the ECS update loop.
    ///
    /// Performance Considerations:
    /// - Uses NativeQueue for thread-safe event queuing
    /// - Processes events in batch during OnUpdate
    /// - Client-side only (presentation layer)
    /// - Cannot be Burst-compiled due to static event subscription
    ///
    /// Flow:
    /// 1. Animation clip fires event -> InteractionAnimatorBridge.OnAnimatorInteract()
    /// 2. Bridge fires static event -> This system queues it
    /// 3. OnUpdate processes queue -> Sets flags on InteractAbilityState component
    /// 4. InteractAbilitySystem reads flags and proceeds with interaction
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InteractionAnimationEventSystem : SystemBase
    {
        /// <summary>
        /// Type of animation event received.
        /// </summary>
        public enum InteractionAnimEventType : byte
        {
            Interact = 0,    // OnAnimatorInteract fired
            Complete = 1     // OnAnimatorInteractComplete fired
        }

        /// <summary>
        /// Queued animation event data.
        /// </summary>
        public struct InteractionAnimEvent
        {
            public int GameObjectInstanceId;
            public InteractionAnimEventType EventType;
        }

        private NativeQueue<InteractionAnimEvent> _eventQueue;
        private bool _isSubscribed;

        protected override void OnCreate()
        {
            base.OnCreate();
            _eventQueue = new NativeQueue<InteractionAnimEvent>(Allocator.Persistent);
            SubscribeToEvents();
        }

        protected override void OnDestroy()
        {
            UnsubscribeFromEvents();
            if (_eventQueue.IsCreated)
                _eventQueue.Dispose();
            base.OnDestroy();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            if (!_isSubscribed)
                SubscribeToEvents();
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            // Clear any pending events when system stops
            while (_eventQueue.TryDequeue(out _)) { }
        }

        private void SubscribeToEvents()
        {
            if (_isSubscribed) return;

            InteractionAnimatorBridge.OnAnimatorInteractEvent += OnInteractReceived;
            InteractionAnimatorBridge.OnAnimatorInteractCompleteEvent += OnCompleteReceived;
            _isSubscribed = true;
        }

        private void UnsubscribeFromEvents()
        {
            if (!_isSubscribed) return;

            InteractionAnimatorBridge.OnAnimatorInteractEvent -= OnInteractReceived;
            InteractionAnimatorBridge.OnAnimatorInteractCompleteEvent -= OnCompleteReceived;
            _isSubscribed = false;
        }

        /// <summary>
        /// Called when OnAnimatorInteract animation event fires.
        /// Queues the event for processing in ECS.
        /// </summary>
        private void OnInteractReceived(GameObject go)
        {
            if (go == null) return;

            _eventQueue.Enqueue(new InteractionAnimEvent
            {
                GameObjectInstanceId = go.GetInstanceID(),
                EventType = InteractionAnimEventType.Interact
            });
        }

        /// <summary>
        /// Called when OnAnimatorInteractComplete animation event fires.
        /// Queues the event for processing in ECS.
        /// </summary>
        private void OnCompleteReceived(GameObject go)
        {
            if (go == null) return;

            _eventQueue.Enqueue(new InteractionAnimEvent
            {
                GameObjectInstanceId = go.GetInstanceID(),
                EventType = InteractionAnimEventType.Complete
            });
        }

        protected override void OnUpdate()
        {
            // Skip if no events to process
            if (_eventQueue.Count == 0)
                return;

            // Process all queued events
            // Note: We set flags on ALL InteractAbilityState components because
            // we don't have a direct entity mapping from GameObject.
            // The InteractAbilitySystem will consume these flags.
            while (_eventQueue.TryDequeue(out var animEvent))
            {
                ProcessAnimationEvent(animEvent);
            }
        }

        private void ProcessAnimationEvent(InteractionAnimEvent animEvent)
        {
            // Set the appropriate flag on all InteractAbilityState components
            // The owning player entity is determined by whether they're currently interacting
            foreach (var (abilityState, ability) in
                     SystemAPI.Query<RefRW<InteractAbilityState>, RefRO<InteractAbility>>())
            {
                // Only process for entities currently interacting
                if (!ability.ValueRO.IsInteracting)
                    continue;

                switch (animEvent.EventType)
                {
                    case InteractionAnimEventType.Interact:
                        abilityState.ValueRW.AnimatorInteractReceived = true;
                        break;

                    case InteractionAnimEventType.Complete:
                        abilityState.ValueRW.AnimatorCompleteReceived = true;
                        break;
                }
            }
        }
    }
}
