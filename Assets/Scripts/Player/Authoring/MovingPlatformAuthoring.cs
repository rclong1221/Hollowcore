using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Makes any object a moving platform that carries the player.
    /// Add to elevators, rotating platforms, moving floors, etc.
    /// <para>
    /// <b>Setup:</b>
    /// 1. Add this component to the platform object
    /// 2. Ensure the platform has a collider on the correct layer
    /// 3. Animate or move the platform via any method (animation, script, etc.)
    /// </para>
    /// </summary>
    public class MovingPlatformAuthoring : MonoBehaviour
    {
        [Header("Momentum Settings")]
        [Tooltip("Transfer platform velocity to player when jumping off or falling off")]
        public bool InheritMomentumOnDisconnect = true;
        
        [Tooltip("Threshold for detecting sudden stops (m/s). If platform decelerates faster than this, player launches off.")]
        [Range(0f, 50f)]
        public float SuddenStopThreshold = 20f;
        
        [Header("Debug")]
        [Tooltip("Show debug gizmos in editor")]
        public bool ShowDebugGizmos = true;
        
        private void OnDrawGizmosSelected()
        {
            if (!ShowDebugGizmos) return;
            
            // Draw platform bounds
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (collider is BoxCollider box)
                {
                    Gizmos.DrawCube(box.center, box.size);
                }
                else if (collider is CapsuleCollider capsule)
                {
                    Gizmos.DrawWireSphere(capsule.center, capsule.radius);
                }
            }
        }
        
        class Baker : Baker<MovingPlatformAuthoring>
        {
            public override void Bake(MovingPlatformAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Moving platform component
                AddComponent(entity, new MovingPlatform
                {
                    LastPosition = authoring.transform.position,
                    LastRotation = authoring.transform.rotation,
                    Velocity = float3.zero,
                    AngularVelocity = float3.zero,
                    InheritMomentumOnDisconnect = (byte)(authoring.InheritMomentumOnDisconnect ? 1 : 0),
                    SuddenStopThreshold = authoring.SuddenStopThreshold
                });
            }
        }
    }
    
    /// <summary>
    /// Add to player prefab to enable moving platform support.
    /// Required for the player to properly attach to and ride platforms.
    /// </summary>
    public class MovingPlatformPlayerAuthoring : MonoBehaviour
    {
        [Header("Platform Settings")]
        [Tooltip("How long momentum from platforms lasts after jumping/falling off (seconds)")]
        [Range(0f, 2f)]
        public float MomentumDecayDuration = 0.5f;
        
        [Tooltip("Minimum platform velocity to inherit (m/s). Ignores tiny movements.")]
        [Range(0f, 2f)]
        public float MinVelocityForMomentum = 0.5f;
        
        [Tooltip("Rotate player with platform (for rotating platforms)")]
        public bool RotateWithPlatform = true;
        
        class Baker : Baker<MovingPlatformPlayerAuthoring>
        {
            public override void Bake(MovingPlatformPlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Platform settings on player
                AddComponent(entity, new MovingPlatformSettings
                {
                    MomentumDecayDuration = authoring.MomentumDecayDuration,
                    MinVelocityForMomentum = authoring.MinVelocityForMomentum,
                    RotateWithPlatform = (byte)(authoring.RotateWithPlatform ? 1 : 0)
                });
                
                // Platform momentum (for inheriting velocity)
                AddComponent(entity, new PlatformMomentum());
                
                // OnMovingPlatform component (disabled by default - enabled when on platform)
                AddComponent(entity, new OnMovingPlatform());
                SetComponentEnabled<OnMovingPlatform>(entity, false);
                
                // Check flag (enableable component)
                AddComponent(entity, new NeedsPlatformCheck());
                SetComponentEnabled<NeedsPlatformCheck>(entity, false);
            }
        }
    }
}
