using UnityEngine;
using Unity.Entities;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Authoring component for slide system.
    /// Attach to player prefab to configure slide behavior.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Player/Slide Authoring")]
    public class SlideAuthoring : MonoBehaviour
    {
        [Header("Slide Duration & Speed")]
        [Tooltip("Maximum duration of slide in seconds")]
        public float Duration = 1.5f;
        
        [Tooltip("Minimum speed required to start/maintain slide (m/s)")]
        public float MinSpeed = 0.5f;
        
        [Tooltip("Maximum speed cap during slide (m/s)")]
        public float MaxSpeed = 12.0f;

        [Header("Slide Physics")]
        [Tooltip("Acceleration applied in slide direction (m/s²)")]
        public float Acceleration = 8.0f;
        
        [Tooltip("Friction applied to slow down slide (m/s²)")]
        public float Friction = 2.0f;

        [Header("Costs & Cooldowns")]
        [Tooltip("Stamina cost for manual slide activation")]
        public float StaminaCost = 5.0f;
        
        [Tooltip("Cooldown before another slide can be triggered (seconds)")]
        public float Cooldown = 1.0f;

        [Header("Auto-Slide Triggers")]
        [Tooltip("Minimum slope angle to auto-trigger slide (degrees)")]
        [Range(0f, 90f)]
        public float MinSlopeAngle = 15.0f;
        
        [Tooltip("Friction multiplier on slippery surfaces (0-1, lower = more slippery)")]
        [Range(0f, 1f)]
        public float SlipperyFrictionMultiplier = 0.1f;

        class Baker : Baker<SlideAuthoring>
        {
            public override void Bake(SlideAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new SlideComponent
                {
                    Duration = authoring.Duration,
                    MinSpeed = authoring.MinSpeed,
                    MaxSpeed = authoring.MaxSpeed,
                    Acceleration = authoring.Acceleration,
                    Friction = authoring.Friction,
                    StaminaCost = authoring.StaminaCost,
                    Cooldown = authoring.Cooldown,
                    MinSlopeAngle = authoring.MinSlopeAngle,
                    SlipperyFrictionMultiplier = authoring.SlipperyFrictionMultiplier
                });
                
                // Add default slide state (inactive)
                AddComponent(entity, new SlideState
                {
                    IsSliding = false,
                    SlideProgress = 0f,
                    CurrentSpeed = 0f,
                    SlideDirection = Unity.Mathematics.float3.zero,
                    TriggerType = SlideTriggerType.Manual,
                    StartTick = 0,
                    CooldownRemaining = 0f,
                    Duration = authoring.Duration,
                    MinSpeed = authoring.MinSpeed,
                    MaxSpeed = authoring.MaxSpeed,
                    Acceleration = authoring.Acceleration,
                    Friction = authoring.Friction
                });
            }
        }
    }
}
