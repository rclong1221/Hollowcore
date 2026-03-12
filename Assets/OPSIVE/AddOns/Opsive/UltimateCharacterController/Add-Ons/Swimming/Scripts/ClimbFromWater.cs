/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Character.Abilities;
    using Opsive.UltimateCharacterController.Utility;
    using UnityEngine;

    /// <summary>
    /// The Climb From Water ability allows the character to climb out of the water onto a horizontal platform.
    /// </summary>
    [DefaultAbilityIndex(303)]
    [DefaultStartType(AbilityStartType.ButtonDown)]
    [DefaultInputName("Jump")]
    [DefaultAllowRotationalInput(false)]
    [DefaultAllowPositionalInput(false)]
    [DefaultUseRootMotionPosition(AbilityBoolOverride.True)]
    [DefaultDetectHorizontalCollisions(AbilityBoolOverride.False)]
    [DefaultDetectVerticalCollisions(AbilityBoolOverride.False)]
    [DefaultUseGravity(AbilityBoolOverride.False)]
    [DefaultUseLookDirection(false)]
    [DefaultCastOffset(0, 0, 0)]
    [DefaultEquippedSlots(0)]
    [Shared.Utility.Group("Swimming Pack")]
    public class ClimbFromWater : DetectObjectAbilityBase
    {
        [Tooltip("The maximum water depth that the character can climb from.")]
        [SerializeField] protected float m_MaxWaterDepth = 0.25f;
        [Tooltip("The maximum height of the surface from the water.")]
        [SerializeField] protected float m_MaxSurfaceHeight = 1.8f;
        [Tooltip("The offset that the character should start climbing from.")]
        [SerializeField] protected Vector3 m_ClimbOffset = new Vector3(0, -1.6f, -0.45f);
        [Tooltip("The speed that the character should move towards the target when getting into position.")]
        [SerializeField] protected float m_MoveToPositionSpeed = 0.05f;

        public float MaxWaterDepth { get { return m_MaxWaterDepth; } set { m_MaxWaterDepth = value; } }
        public float MaxSurfaceHeight { get { return m_MaxSurfaceHeight; } set { m_MaxSurfaceHeight = value; } }
        public Vector3 ClimbOffset { get { return m_ClimbOffset; } set { m_ClimbOffset = value; } }
        public float MoveToPositionSpeed { get { return m_MoveToPositionSpeed; } set { m_MoveToPositionSpeed = value; } }

        private Swim m_SwimAbility;
        private Vector3 m_DetectedObjectNormal;
        private Vector3 m_TopClimbPosition;
        private bool m_InPosition;
        private bool m_Moving;

        public override int AbilityIntData { get { if (!m_InPosition) { return 0; } return m_Moving ? 2 : 1; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_SwimAbility = m_CharacterLocomotion.GetAbility<Swim>();
            if (m_SwimAbility == null) {
                Debug.LogError("Error: The Swim ability must be added in order for the character to be able to climb from the water.");
                Enabled = false;
                return;
            }

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorClimbComplete", OnClimbComplete);
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            if (!m_SwimAbility.IsActive || m_SwimAbility.GetDepthInWater(true) > m_MaxWaterDepth) {
                return false;
            }

            return base.CanStartAbility();
        }

        /// <summary>
        /// Validates the object to ensure it is valid for the current ability.
        /// </summary>
        /// <param name="obj">The object being validated.</param>
        /// <param name="raycastHit">The raycast hit of the detected object. Will be null for trigger detections.</param>
        /// <returns>True if the object is valid. The object may not be valid if it doesn't have an ability-specific component attached.</returns>
        protected override bool ValidateObject(GameObject obj, RaycastHit? raycastHit)
        {
            if (!base.ValidateObject(obj, raycastHit)) {
                return false;
            }

            if (!m_CharacterLocomotion.SingleCast(m_CharacterLocomotion.Rotation * Vector3.forward, Vector3.zero, m_CastDistance, m_CharacterLayerManager.SolidObjectLayers, ref m_RaycastResult)) {
                return false;
            }

            m_DetectedObjectNormal = Vector3.ProjectOnPlane(m_RaycastResult.normal, m_CharacterLocomotion.Up).normalized;

            // The character should be positioned relative to the top of the hit object.
            if (!Physics.Raycast(m_RaycastResult.point - m_DetectedObjectNormal * 0.02f + m_CharacterLocomotion.Up * m_CharacterLocomotion.Height * 2, -m_CharacterLocomotion.Up, out m_RaycastResult,
                m_CharacterLocomotion.Height * 3, 1 << m_RaycastResult.collider.gameObject.layer, QueryTriggerInteraction.Ignore)) {
                return false;
            };

            // Ensure the point is less than the max surface height.
            if (m_CharacterLocomotion.InverseTransformPoint(m_RaycastResult.point).y > m_MaxSurfaceHeight) {
                return false;
            }

            // The ability can only pull up on a flat surface.
            if (Vector3.Dot(m_RaycastResult.normal, m_CharacterLocomotion.Up) < 0.99f) {
                return false;
            }

            m_TopClimbPosition = MathUtility.TransformPoint(m_RaycastResult.point, m_CharacterLocomotion.Rotation, m_ClimbOffset);

            return true;
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_InPosition = false;
            m_Moving = m_CharacterLocomotion.Moving;
            SetAbilityIntDataParameter(AbilityIntData);
        }

        /// <summary>
        /// Called when another ability is attempting to start and the current ability is active.
        /// Returns true or false depending on if the new ability should be blocked from starting.
        /// </summary>
        /// <param name="startingAbility">The ability that is starting.</param>
        /// <returns>True if the ability should be blocked.</returns>
        public override bool ShouldBlockAbilityStart(Ability startingAbility)
        {
            return startingAbility is HeightChange;
        }

        /// <summary>
        /// Called when the current ability is attempting to start and another ability is active.
        /// Returns true or false depending on if the active ability should be stopped.
        /// </summary>
        /// <param name="activeAbility">The ability that is currently active.</param>
        /// <returns>True if the ability should be stopped.</returns>
        public override bool ShouldStopActiveAbility(Ability activeAbility)
        {
            return activeAbility is HeightChange;
        }

        /// <summary>
        /// Update the controller's rotation values.
        /// </summary>
        public override void UpdateRotation()
        {
            // Keep the character facing the object that they are climbing out on.
            var targetRotation = Quaternion.LookRotation(-m_DetectedObjectNormal, m_CharacterLocomotion.Up);
            m_CharacterLocomotion.DesiredRotation = Quaternion.Inverse(m_CharacterLocomotion.Rotation) * targetRotation;
        }

        /// <summary>
        /// Update the controller's position values.
        /// </summary>
        public override void UpdatePosition()
        {
            if (m_InPosition) {
                return;
            }

            // Move the character into the starting position. The animation will put up as soon as the character is in position.
            var direction = m_TopClimbPosition - m_CharacterLocomotion.Position;
            if (direction.magnitude < 0.01f) {
                m_CharacterLocomotion.DesiredMovement = Vector3.zero;
                m_InPosition = true;
                UpdateAbilityAnimatorParameters();
            } else {
                m_CharacterLocomotion.DesiredMovement = Vector3.MoveTowards(Vector3.zero, direction, m_MoveToPositionSpeed * m_CharacterLocomotion.TimeScale * Time.timeScale);
            }
        }

        /// <summary>
        /// The climb animation has completed.
        /// </summary>
        private void OnClimbComplete()
        {
            StopAbility();
        }

        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorClimbComplete", OnClimbComplete);
        }
    }
}