using Unity.Entities;
using UnityEngine;
using Player.Components;

namespace Player.Authoring
{
    /// <summary>
    /// Inspector configuration for locomotion polish settings.
    /// Add to player prefab (Warrok_Server) to enable movement polish features.
    /// <para>
    /// <b>Setup:</b>
    /// 1. Add this component to the player prefab
    /// 2. Choose a preset or configure custom values
    /// 3. The baker will add all required ECS components
    /// </para>
    /// </summary>
    public class LocomotionPolishAuthoring : MonoBehaviour
    {
        [Header("Preset Selection")]
        [Tooltip("Quick preset selection. Choose Custom to use values below.")]
        public PolishPreset Preset = PolishPreset.Default;
        
        [Header("Motor Polish")]
        [Tooltip("Speed multiplier when walking backwards (0.6 = 60% speed)")]
        [Range(0.3f, 1f)]
        public float MotorBackwardsMultiplier = 0.7f;
        
        [Tooltip("How much previous momentum influences movement (0 = snappy, 1 = heavy)")]
        [Range(0f, 1f)]
        public float PreviousAccelerationInfluence = 0.5f;
        
        [Header("Slope Handling")]
        [Tooltip("Adjust speed on slopes")]
        public bool AdjustMotorForceOnSlope = true;
        
        [Tooltip("Speed multiplier going uphill (0.8 = 20% slower)")]
        [Range(0.5f, 1.2f)]
        public float MotorSlopeForceUp = 1.0f;
        
        [Tooltip("Speed multiplier going downhill (1.25 = 25% faster)")]
        [Range(0.8f, 1.5f)]
        public float MotorSlopeForceDown = 1.15f;
        
        [Tooltip("Distance to check below for ground sticking")]
        [Range(0.01f, 0.3f)]
        public float GroundStickDistance = 0.1f;
        
        [Header("Wall Collision")]
        [Tooltip("Wall friction multiplier")]
        [Range(0f, 2f)]
        public float WallFrictionModifier = 1.0f;
        
        [Tooltip("Wall bounce multiplier (0 = no bounce)")]
        [Range(0f, 3f)]
        public float WallBounceModifier = 0f;
        
        [Tooltip("Ground friction multiplier")]
        [Range(0.5f, 2f)]
        public float GroundFrictionModifier = 1.0f;
        
        [Header("Collision Polish")]
        [Tooltip("Check collisions even when stationary")]
        public bool ContinuousCollisionDetection = true;
        
        [Tooltip("Max iterations for penetration resolution")]
        [Range(1, 10)]
        public int MaxPenetrationChecks = 5;
        
        [Tooltip("Max iterations for movement collision")]
        [Range(1, 10)]
        public int MaxMovementCollisionChecks = 5;
        
        [Tooltip("Cancel upward force when hitting ceiling")]
        public bool CancelForceOnCeiling = true;
        
        [Header("Soft Forces")]
        [Tooltip("Max frames to distribute impulses across")]
        [Range(1, 60)]
        public int MaxSoftForceFrames = 30;
        
        public enum PolishPreset
        {
            Custom,
            Default,
            Arcade,
            Realistic
        }
        
        private void OnValidate()
        {
            ApplyPreset();
        }
        
        [ContextMenu("Apply Preset")]
        public void ApplyPreset()
        {
            switch (Preset)
            {
                case PolishPreset.Default:
                    ApplyDefaultPreset();
                    break;
                case PolishPreset.Arcade:
                    ApplyArcadePreset();
                    break;
                case PolishPreset.Realistic:
                    ApplyRealisticPreset();
                    break;
            }
        }
        
        private void ApplyDefaultPreset()
        {
            var d = MotorPolishSettings.Default;
            MotorBackwardsMultiplier = d.MotorBackwardsMultiplier;
            PreviousAccelerationInfluence = d.PreviousAccelerationInfluence;
            AdjustMotorForceOnSlope = d.AdjustMotorForceOnSlope == 1;
            MotorSlopeForceUp = d.MotorSlopeForceUp;
            MotorSlopeForceDown = d.MotorSlopeForceDown;
            GroundStickDistance = d.GroundStickDistance;
        }
        
        private void ApplyArcadePreset()
        {
            var a = MotorPolishSettings.Arcade;
            MotorBackwardsMultiplier = a.MotorBackwardsMultiplier;
            PreviousAccelerationInfluence = a.PreviousAccelerationInfluence;
            AdjustMotorForceOnSlope = a.AdjustMotorForceOnSlope == 1;
            MotorSlopeForceUp = a.MotorSlopeForceUp;
            MotorSlopeForceDown = a.MotorSlopeForceDown;
            GroundStickDistance = a.GroundStickDistance;
        }
        
        private void ApplyRealisticPreset()
        {
            var r = MotorPolishSettings.Realistic;
            MotorBackwardsMultiplier = r.MotorBackwardsMultiplier;
            PreviousAccelerationInfluence = r.PreviousAccelerationInfluence;
            AdjustMotorForceOnSlope = r.AdjustMotorForceOnSlope == 1;
            MotorSlopeForceUp = r.MotorSlopeForceUp;
            MotorSlopeForceDown = r.MotorSlopeForceDown;
            GroundStickDistance = r.GroundStickDistance;
        }
        
        class Baker : Baker<LocomotionPolishAuthoring>
        {
            public override void Bake(LocomotionPolishAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Motor polish settings
                AddComponent(entity, new MotorPolishSettings
                {
                    MotorBackwardsMultiplier = authoring.MotorBackwardsMultiplier,
                    PreviousAccelerationInfluence = authoring.PreviousAccelerationInfluence,
                    AdjustMotorForceOnSlope = (byte)(authoring.AdjustMotorForceOnSlope ? 1 : 0),
                    MotorSlopeForceUp = authoring.MotorSlopeForceUp,
                    MotorSlopeForceDown = authoring.MotorSlopeForceDown,
                    GroundStickDistance = authoring.GroundStickDistance
                });
                
                // Motor polish state
                AddComponent(entity, new MotorPolishState());
                
                // Wall slide settings
                AddComponent(entity, new WallSlideSettings
                {
                    WallFrictionModifier = authoring.WallFrictionModifier,
                    WallBounceModifier = authoring.WallBounceModifier,
                    GroundFrictionModifier = authoring.GroundFrictionModifier
                });
                
                // Collision polish settings
                AddComponent(entity, new CollisionPolishSettings
                {
                    ContinuousCollisionDetection = (byte)(authoring.ContinuousCollisionDetection ? 1 : 0),
                    MaxPenetrationChecks = authoring.MaxPenetrationChecks,
                    MaxMovementCollisionChecks = authoring.MaxMovementCollisionChecks,
                    ColliderSpacing = 0.01f,
                    CancelForceOnCeiling = (byte)(authoring.CancelForceOnCeiling ? 1 : 0)
                });
                
                // Soft force settings
                AddComponent(entity, new SoftForceSettings
                {
                    MaxSoftForceFrames = authoring.MaxSoftForceFrames,
                    CurrentFrameIndex = 0
                });
                
                // Soft force buffer
                AddBuffer<SoftForceFrame>(entity);
                
                // External force state
                AddComponent(entity, new ExternalForceState());
                AddComponent(entity, new ExternalForceSettings
                {
                    MaxForceMagnitude = 50f,
                    DefaultDecay = 5f,
                    ForceResistance = 1.0f,
                    SoftForceFrames = authoring.MaxSoftForceFrames
                });
                AddBuffer<ExternalForceElement>(entity);
                
                // Force request component (disabled by default)
                AddComponent(entity, new AddExternalForceRequest());
                SetComponentEnabled<AddExternalForceRequest>(entity, false);
            }
        }
    }
}
