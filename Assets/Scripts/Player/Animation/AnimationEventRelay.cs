using UnityEngine;
using Player.Bridges;
using Player.Components;

namespace Player.Animation
{
    /// <summary>
    /// Relay component for animation events.
    /// 
    /// Place this component on the SAME GameObject as the Animator component.
    /// It receives animation events from animation clips and forwards them
    /// to the ClimbAnimatorBridge (which may be on a parent/different GameObject).
    /// 
    /// Unity Animation Events only call methods on components attached to the same
    /// GameObject as the Animator. This relay bridges that gap.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class AnimationEventRelay : MonoBehaviour
    {
        [Tooltip("Reference to the ClimbAnimatorBridge (auto-found if not set)")]
        [SerializeField] private ClimbAnimatorBridge climbBridge;
        
        [Tooltip("Enable debug logging for animation events")]
        [SerializeField] private bool debugLogging = false;
        
        private void Awake()
        {
            // Auto-find ClimbAnimatorBridge in parent hierarchy
            if (climbBridge == null)
            {
                climbBridge = GetComponentInParent<ClimbAnimatorBridge>();
            }
            
            if (climbBridge == null)
            {
                Debug.LogWarning($"[AnimationEventRelay] No ClimbAnimatorBridge found in hierarchy of {gameObject.name}");
            }
        }
        
        // ====================================================
        // MAIN OPSIVE EVENT DISPATCHER
        // Opsive clips call ExecuteEvent(string) with event name as parameter
        // ====================================================
        
        /// <summary>
        /// Main event dispatcher called by Opsive animation clips.
        /// Routes to specific handlers based on event name.
        /// </summary>
        public void ExecuteEvent(string eventName)
        {
            if (debugLogging)
                Debug.Log($"[OpsiveAnimEventRelay] ExecuteEvent received: {eventName}");
            
            switch (eventName)
            {
                case "OnAnimatorFreeClimbStartInPosition":
                    OnAnimatorFreeClimbStartInPosition();
                    break;
                case "OnAnimatorFreeClimbComplete":
                    OnAnimatorFreeClimbComplete();
                    break;
                case "OnAnimatorFreeClimbTurnComplete":
                    OnAnimatorFreeClimbTurnComplete();
                    break;
                case "OnAnimatorHangStartInPosition":
                    OnAnimatorHangStartInPosition();
                    break;
                case "OnAnimatorHangComplete":
                    OnAnimatorHangComplete();
                    break;
                default:
                    if (debugLogging)
                        Debug.Log($"[OpsiveAnimEventRelay] Unhandled event: {eventName}");
                    break;
            }
        }
        
        // ====================================================
        // OPSIVE FREE CLIMB ANIMATION EVENTS
        // ====================================================
        
        /// <summary>
        /// Called by Opsive animation event when mount animation is complete.
        /// </summary>
        public void OnAnimatorFreeClimbStartInPosition()
        {
            if (debugLogging)
                Debug.Log("[OpsiveAnimEventRelay] OnAnimatorFreeClimbStartInPosition");
            
            // Forward to ClimbAnimatorBridge
            if (climbBridge != null)
                climbBridge.OnAnimatorFreeClimbStartInPosition();
            
            // Also queue directly to ECS as backup
            FreeClimbAnimationEvents.QueueEvent(FreeClimbAnimationEvents.EventType.StartInPosition);
        }
        
        /// <summary>
        /// Called by Opsive animation event when dismount animation is complete.
        /// </summary>
        public void OnAnimatorFreeClimbComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveAnimEventRelay] OnAnimatorFreeClimbComplete");
            
            if (climbBridge != null)
                climbBridge.OnAnimatorFreeClimbComplete();
            
            FreeClimbAnimationEvents.QueueEvent(FreeClimbAnimationEvents.EventType.Complete);
        }
        
        /// <summary>
        /// Called by Opsive animation event when corner turn animation is complete.
        /// </summary>
        public void OnAnimatorFreeClimbTurnComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveAnimEventRelay] OnAnimatorFreeClimbTurnComplete");
            
            if (climbBridge != null)
                climbBridge.OnAnimatorFreeClimbTurnComplete();
            
            FreeClimbAnimationEvents.QueueEvent(FreeClimbAnimationEvents.EventType.TurnComplete);
        }
        
        // ====================================================
        // OPSIVE HANG ANIMATION EVENTS
        // ====================================================
        
        /// <summary>
        /// Called by Opsive animation event when hang start animation is complete.
        /// </summary>
        public void OnAnimatorHangStartInPosition()
        {
            if (debugLogging)
                Debug.Log("[OpsiveAnimEventRelay] OnAnimatorHangStartInPosition");
            
            if (climbBridge != null)
                climbBridge.OnAnimatorHangStartInPosition();
            
            FreeClimbAnimationEvents.QueueEvent(FreeClimbAnimationEvents.EventType.HangStartInPosition);
        }
        
        /// <summary>
        /// Called by Opsive animation event when hang ability is complete (pull-up).
        /// </summary>
        public void OnAnimatorHangComplete()
        {
            if (debugLogging)
                Debug.Log("[OpsiveAnimEventRelay] OnAnimatorHangComplete");
            
            if (climbBridge != null)
                climbBridge.OnAnimatorHangComplete();
            
            FreeClimbAnimationEvents.QueueEvent(FreeClimbAnimationEvents.EventType.HangComplete);
        }
    }
}
