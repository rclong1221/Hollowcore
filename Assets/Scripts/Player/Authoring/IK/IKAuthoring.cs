using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;
using DIG.Player.IK;

namespace DIG.Player.Authoring.IK
{
    public class IKAuthoring : MonoBehaviour
    {
        [Header("Foot IK")]
        [Tooltip("How far down to raycast to find ground")]
        public float FootRayLength = 0.6f;
        [Tooltip("Distance to keep foot above ground surface")]
        public float FootOffset = 0.05f;
        [Tooltip("Maximum amount hips can lower to reach ground")]
        public float BodyHeightAdjustment = 0.3f;
        [Tooltip("IK weight for foot placement (0-1)")]
        [Range(0f, 1f)]
        public float FootIKWeight = 1.0f;
        [Tooltip("IK weight for body height adjustment (0-1)")]
        [Range(0f, 1f)]
        public float BodyIKWeight = 1.0f;
        [Tooltip("How fast IK blends in/out")]
        public float FootBlendSpeed = 10f;
        [Tooltip("Left foot offset from root (usually negative X for left side)")]
        public Vector3 LeftFootOffset = new Vector3(-0.1f, 0f, 0f);
        [Tooltip("Right foot offset from root (usually positive X for right side)")]
        public Vector3 RightFootOffset = new Vector3(0.1f, 0f, 0f);

        [Header("Look At IK")]
        public LookAtMode LookAtMode = LookAtMode.MouseAim;
        [Tooltip("Maximum head rotation angle in degrees")]
        public float MaxHeadAngle = 70f;
        [Tooltip("Maximum spine contribution angle in degrees")]
        public float MaxSpineAngle = 30f;
        [Tooltip("Maximum total angle from forward before look fades out")]
        public float MaxTotalAngle = 120f;
        public float HeadWeight = 1.0f;
        public float BodyWeight = 0.3f;
        public float EyesWeight = 0.5f;
        public float ClampWeight = 0.5f;
        public float LookAtBlendSpeed = 10f;
        public float AimLagAmount = 0.1f;
        public float SpeedReductionStart = 5f;
        public float SpeedReductionEnd = 10f;
        public float MaxAimDistance = 100f;
        
        [Header("Hand IK (Weapons/Aiming)")]
        [Tooltip("Weight for hand IK when aiming (0-1)")]
        [Range(0f, 1f)]
        public float HandWeight = 1.0f;
        [Tooltip("Speed at which hand IK blends in/out")]
        public float HandAdjustmentSpeed = 10f;
        [Tooltip("Local position offset for hands")]
        public Vector3 HandPositionOffset = Vector3.zero;
        
        [Header("Upper Arm IK")]
        [Tooltip("Weight for upper arm rotation toward aim (0-1)")]
        [Range(0f, 1f)]
        public float UpperArmWeight = 1.0f;
        [Tooltip("Speed at which upper arm IK blends")]
        public float UpperArmAdjustmentSpeed = 10f;
        
        [Header("Hand Spring (Recoil)")]
        [Tooltip("Spring stiffness for hand recoil")]
        public float SpringStiffness = 0.2f;
        [Tooltip("Spring damping for hand recoil")]
        public float SpringDamping = 0.25f;
        
        [Header("Interaction Hand IK")]
        [Tooltip("Speed for interpolating hands to interaction targets")]
        public float InterpolationSpeed = 8f;
        [Tooltip("Maximum reach distance for interactions")]
        public float MaxReachDistance = 1.5f;

        public class Baker : Baker<IKAuthoring>
        {
            public override void Bake(IKAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // Foot IK
                AddComponent(entity, new FootIKSettings
                {
                    FootRayLength = authoring.FootRayLength,
                    FootOffset = authoring.FootOffset,
                    BodyHeightAdjustment = authoring.BodyHeightAdjustment,
                    FootIKWeight = authoring.FootIKWeight,
                    BodyIKWeight = authoring.BodyIKWeight,
                    BlendSpeed = authoring.FootBlendSpeed,
                    LeftFootPosOffset = authoring.LeftFootOffset,
                    RightFootPosOffset = authoring.RightFootOffset
                });
                
                AddComponent(entity, new FootIKState());

                // Look At IK
                AddComponent(entity, new LookAtIKSettings
                {
                    Mode = authoring.LookAtMode,
                    MaxHeadAngle = authoring.MaxHeadAngle,
                    MaxSpineAngle = authoring.MaxSpineAngle,
                    MaxTotalAngle = authoring.MaxTotalAngle,
                    HeadWeight = authoring.HeadWeight,
                    BodyWeight = authoring.BodyWeight,
                    EyesWeight = authoring.EyesWeight,
                    ClampWeight = authoring.ClampWeight,
                    BlendSpeed = authoring.LookAtBlendSpeed,
                    AimLagAmount = authoring.AimLagAmount,
                    SpeedReductionStart = authoring.SpeedReductionStart,
                    SpeedReductionEnd = authoring.SpeedReductionEnd,
                    MaxAimDistance = authoring.MaxAimDistance
                });
                
                AddComponent(entity, new LookAtIKState());
                AddComponent(entity, new AimDirection());

                // Hand IK (weapons/aiming)
                AddComponent(entity, new HandIKSettings
                {
                    HandWeight = authoring.HandWeight,
                    HandAdjustmentSpeed = authoring.HandAdjustmentSpeed,
                    HandPositionOffset = authoring.HandPositionOffset,
                    UpperArmWeight = authoring.UpperArmWeight,
                    UpperArmAdjustmentSpeed = authoring.UpperArmAdjustmentSpeed,
                    InterpolationSpeed = authoring.InterpolationSpeed,
                    MaxReachDistance = authoring.MaxReachDistance,
                    BlendSpeed = authoring.HandAdjustmentSpeed,
                    SpringStiffness = authoring.SpringStiffness,
                    SpringDamping = authoring.SpringDamping
                });
                
                AddComponent(entity, new HandIKState
                {
                    LeftHandRotation = quaternion.identity,
                    RightHandRotation = quaternion.identity,
                    IsRightHandDominant = true // Default to right-handed
                });
                
                // IK Target Override buffer for ability-driven IK
                AddBuffer<IKTargetOverride>(entity);

                // Misc
                AddComponent(entity, new PlayerIKBridgeLink());
            }
        }
    }
}
