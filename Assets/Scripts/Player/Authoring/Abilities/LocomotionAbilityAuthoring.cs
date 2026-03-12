using Unity.Entities;
using UnityEngine;
using DIG.Player.Abilities;

namespace DIG.Player.Authoring.Abilities
{
    public class LocomotionAbilityAuthoring : MonoBehaviour
    {
        [Header("Jump - Basic")]
        public float JumpForce = 5f; // Initial impulse
        public int MaxJumps = 1;
        public float JumpGravityMultiplier = 0.5f; // For variable height
        
        [Header("Jump - Ceiling & Slope")]
        [Tooltip("Distance to check above head for ceiling. 0 = disabled")]
        public float MinCeilingJumpHeight = 0f;
        [Tooltip("Block jump on slopes steeper than SlopeLimit")]
        public bool PreventSlopeLimitJump = false;
        [Tooltip("Max slope angle (degrees) for jumping")]
        public float SlopeLimit = 45f;
        
        [Header("Jump - Directional")]
        [Tooltip("Jump height multiplier when moving sideways")]
        public float SidewaysForceMultiplier = 0.8f;
        [Tooltip("Jump height multiplier when moving backwards")]
        public float BackwardsForceMultiplier = 0.7f;
        
        [Header("Jump - Timing")]
        [Tooltip("Grace period after leaving ground to still jump")]
        public float CoyoteTime = 0.15f;
        [Tooltip("Seconds after landing before next jump allowed. 0 = disabled")]
        public float RecurrenceDelay = 0f;
        
        [Header("Jump - Hold For Height")]
        [Tooltip("Extra force per frame while jump held")]
        public float ForceHold = 0.003f;
        [Tooltip("Damping for hold force")]
        public float ForceDampingHold = 0.5f;
        
        [Header("Jump - Airborne (Double/Triple)")]
        [Tooltip("0 = disabled, 1 = double jump, 2 = triple jump")]
        public int MaxAirborneJumps = 0;
        [Tooltip("Multiplier for airborne jump force (0.6 = 60% of normal)")]
        public float AirborneJumpForce = 0.6f;

        [Header("Jump - Advanced Features")]
        [Tooltip("Number of frames to distribute jump force over (1 = instant)")]
        public int JumpFrames = 1;
        [Tooltip("Use dampening for multi-frame jumps")]
        public float ForceDamping = 0f;
        
        [Tooltip("If true, wait for OnAnimatorJump event before applying force")]
        public bool WaitForAnimationEvent = false;
        [Tooltip("Max time to wait for event before forcing jump")]
        public float JumpEventTimeout = 0.15f;
        
        [Tooltip("If true, triggers Surface Impact effects (Audio/VFX) on jump start")]
        public bool SpawnSurfaceEffect = true;

        [Header("Crouch")]
        public float CrouchHeight = 1.0f;
        public float CrouchRadius = 0.35f;
        public Vector3 CrouchCenter = new Vector3(0, 0.5f, 0);
        public float CrouchTransitionSpeed = 10f;
        public float CrouchSpeedMultiplier = 0.5f;
        
        [Header("Crouch - Height Change")]
        [Tooltip("Original standing height. 0 = auto-detect from collider")]
        public float StandingHeight = 1.8f;
        [Tooltip("Spacing for obstruction overlap checks")]
        public float CrouchColliderSpacing = 0.02f;
        [Tooltip("Allow sprinting while crouched")]
        public bool AllowSprintWhileCrouched = false;
        
        [Header("Sprint")]
        public float SprintSpeedMultiplier = 1.5f;

        public class Baker : Baker<LocomotionAbilityAuthoring>
        {
            public override void Bake(LocomotionAbilityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Jump
                AddComponent(entity, new JumpAbility());
                if (authoring.WaitForAnimationEvent)
                {
                    AddComponent(entity, new JumpEventTrigger());
                }
                
                AddComponent(entity, new JumpSettings
                {
                    JumpForce = authoring.JumpForce,
                    MaxJumps = authoring.MaxJumps,
                    GravityMultiplier = authoring.JumpGravityMultiplier,
                    // EPIC 13.13 - New fields
                    MinCeilingJumpHeight = authoring.MinCeilingJumpHeight,
                    PreventSlopeLimitJump = authoring.PreventSlopeLimitJump,
                    SlopeLimit = authoring.SlopeLimit,
                    SidewaysForceMultiplier = authoring.SidewaysForceMultiplier,
                    BackwardsForceMultiplier = authoring.BackwardsForceMultiplier,
                    CoyoteTime = authoring.CoyoteTime,
                    RecurrenceDelay = authoring.RecurrenceDelay,
                    ForceHold = authoring.ForceHold,
                    ForceDampingHold = authoring.ForceDampingHold,
                    MaxAirborneJumps = authoring.MaxAirborneJumps,
                    AirborneJumpForce = authoring.AirborneJumpForce,
                    
                    // Advanced
                    JumpFrames = authoring.JumpFrames,
                    ForceDamping = authoring.ForceDamping,
                    WaitForAnimationEvent = authoring.WaitForAnimationEvent,
                    JumpEventTimeout = authoring.JumpEventTimeout,
                    SpawnSurfaceEffect = authoring.SpawnSurfaceEffect
                });

                // Crouch
                AddComponent(entity, new CrouchAbility());
                AddComponent(entity, new CrouchSettings
                {
                    CrouchHeight = authoring.CrouchHeight,
                    CrouchRadius = authoring.CrouchRadius,
                    CrouchCenter = authoring.CrouchCenter,
                    TransitionSpeed = authoring.CrouchTransitionSpeed,
                    MovementSpeedMultiplier = authoring.CrouchSpeedMultiplier,
                    // 13.15
                    StandingHeight = authoring.StandingHeight,
                    ColliderSpacing = authoring.CrouchColliderSpacing,
                    AllowSpeedChange = authoring.AllowSprintWhileCrouched
                });

                // Sprint
                AddComponent(entity, new SprintAbility());
                AddComponent(entity, new SprintSettings
                {
                    SpeedMultiplier = authoring.SprintSpeedMultiplier
                });

                // Root Motion
                AddComponent(entity, new RootMotionDelta());
            }
        }
    }
}

