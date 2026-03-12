using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using DIG.Player.Abilities;
using Player.Components;

namespace DIG.Player.Authoring.Abilities
{
    public class MovementPolishAuthoring : MonoBehaviour
    {
        [Header("Quick Start")]
        public float QuickStartDuration = 0.3f;
        public float QuickStartMultiplier = 2.0f;
        public float MinInputMagnitude = 0.3f;
        public float VelocityThreshold = 0.1f;

        [Header("Quick Stop")]
        public float QuickStopDuration = 0.2f;
        public float QuickStopDeceleration = 3.0f;
        public float MinVelocityToStop = 0.5f;

        [Header("Quick Turn")]
        public float TurnSpeed = 10f;
        public float TurnDuration = 0.3f;
        public float MomentumRetention = 0.2f;

        [Header("Fall")]
        [Tooltip("Minimum height above ground to trigger fall ability. 0 = any height.")]
        public float MinFallHeight = 0.2f;
        [Tooltip("Minimum height for fall animation to play")]
        public float MinFallHeightForAnimation = 1.5f;
        [Tooltip("Height threshold for hard landing effects")]
        public float HardLandingHeight = 3.0f;
        [Tooltip("Maximum height without damage")]
        public float MaxSafeFallHeight = 6.0f;

        [Header("Fall - Surface Impact")]
        [Tooltip("ID for surface impact preset (0 = default dust/thud)")]
        public int LandSurfaceImpactId = 0;
        [Tooltip("Minimum downward velocity for surface impact to play (negative value)")]
        public float MinSurfaceImpactVelocity = -4f;

        [Header("Fall - Animation Events")]
        [Tooltip("Wait for OnAnimatorFallComplete event before ending fall ability")]
        public bool WaitForLandEvent = true;
        [Tooltip("Timeout in seconds if animation event doesn't fire")]
        public float LandEventTimeout = 1.0f;

        [Header("Fall - Physics")]
        [Tooltip("Layers considered solid ground for min height raycast")]
        public LayerMask SolidObjectLayers = 1; // Default layer
        
        [Header("Idle")]
        public int VariationCount = 3;
        public float MinIdleInterval = 5f;
        public float MaxIdleInterval = 10f;

        [Header("Restrictions")]
        public bool RestrictPosition;
        public Vector3 MinPosition = new Vector3(-100, -50, -100);
        public Vector3 MaxPosition = new Vector3(100, 50, 100);
        public bool RestrictX = true;
        public bool RestrictY = true;
        public bool RestrictZ = true;

        public class Baker : Baker<MovementPolishAuthoring>
        {
            public override void Bake(MovementPolishAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Quick Start
                AddComponent(entity, new QuickStartAbility());
                AddComponent(entity, new QuickStartSettings
                {
                    Duration = authoring.QuickStartDuration,
                    AccelerationMultiplier = authoring.QuickStartMultiplier,
                    MinInputMagnitude = authoring.MinInputMagnitude,
                    VelocityThreshold = authoring.VelocityThreshold
                });

                // Quick Stop
                AddComponent(entity, new QuickStopAbility());
                AddComponent(entity, new QuickStopSettings
                {
                    Duration = authoring.QuickStopDuration,
                    DecelerationMultiplier = authoring.QuickStopDeceleration,
                    MinVelocityToTrigger = authoring.MinVelocityToStop
                });

                // Quick Turn
                AddComponent(entity, new QuickTurnAbility());
                AddComponent(entity, new QuickTurnSettings
                {
                    TurnSpeed = authoring.TurnSpeed,
                    Duration = authoring.TurnDuration,
                    MomentumRetention = authoring.MomentumRetention,
                    DirectionThreshold = -0.5f // Default
                });

                // Fall - 13.14: Full Opsive parity
                AddComponent(entity, new FallAbility
                {
                    IsFalling = false,
                    FallStartHeight = 0f,
                    FallDuration = 0f,
                    StateIndex = 0,
                    Landed = false,
                    WaitingForAnimationEvent = false,
                    AnimationEventTimeout = authoring.LandEventTimeout,
                    AnimationEventTimer = 0f,
                    PendingImmediateTransformChange = false
                });

                // 13.14: New FallSettings component with full configuration
                AddComponent(entity, new FallSettings
                {
                    MinFallHeight = authoring.MinFallHeight,
                    LandSurfaceImpactId = authoring.LandSurfaceImpactId,
                    MinSurfaceImpactVelocity = authoring.MinSurfaceImpactVelocity,
                    WaitForLandEvent = authoring.WaitForLandEvent,
                    LandEventTimeout = authoring.LandEventTimeout,
                    MinFallHeightForAnimation = authoring.MinFallHeightForAnimation,
                    HardLandingHeight = authoring.HardLandingHeight,
                    MaxSafeFallHeight = authoring.MaxSafeFallHeight,
                    SolidObjectLayerMask = (uint)authoring.SolidObjectLayers.value
                });

                // 13.14.2: Surface impact request (disabled by default, enabled when landing)
                AddComponent(entity, new SurfaceImpactRequest());
                SetComponentEnabled<SurfaceImpactRequest>(entity, false);

                // 13.14.3: Fall animation complete flag (disabled by default, enabled by animator bridge)
                AddComponent(entity, new FallAnimationComplete());
                SetComponentEnabled<FallAnimationComplete>(entity, false);

                // 13.14.5: Teleport event (disabled by default, enabled when teleporting)
                AddComponent(entity, new TeleportEvent());
                SetComponentEnabled<TeleportEvent>(entity, false);

                AddComponent(entity, new LandingEffect());

                // Idle
                AddComponent(entity, new IdleAbility
                {
                    MinVariationInterval = authoring.MinIdleInterval,
                    MaxVariationInterval = authoring.MaxIdleInterval,
                    VariationCount = authoring.VariationCount
                });

                // Restrictions
                if (authoring.RestrictPosition)
                {
                    AddComponent(entity, new RestrictPosition
                    {
                        Min = authoring.MinPosition,
                        Max = authoring.MaxPosition,
                        AxesEnabled = new bool3(authoring.RestrictX, authoring.RestrictY, authoring.RestrictZ)
                    });
                }
                
                // Speed Modifiers
                AddBuffer<SpeedModifier>(entity);
                AddComponent(entity, new SpeedModifierState { CombinedMultiplier = 1.0f });
            }
        }
    }
}
