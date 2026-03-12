/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Agility
{
    using Opsive.Shared.Events;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Character.Abilities;
    using UnityEngine;

    /// <summary>
    /// The Balance ability will keep the character oriented in the forward/backward direction of a narrow platform. The ability will restrict the character's
    /// rotation so they can only move forward or backwards along the object.
    /// </summary>
    [DefaultAbilityIndex(107)]
    [DefaultUseRootMotionPosition(AbilityBoolOverride.True)]
    [Opsive.Shared.Utility.Group("Agility Pack")]
    public class Balance : DetectGroundAbilityBase
    {
        [Tooltip("A (0-1) value indicating how much influence the character should rotate towards the balance direction. Set to 1 to always face the balance direction.")]
        [Range(0, 1)] [SerializeField] protected float m_ForceDirectionInfluence = 0.8f;

        public float ForceDirectionInfluence { get { return m_ForceDirectionInfluence; } set { m_ForceDirectionInfluence = value; } }

        public override bool ImmediateStartItemVerifier { get { return true; } }

        private ILookSource m_LookSource;

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            EventHandler.RegisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
        }

        /// <summary>
        /// A new ILookSource object has been attached to the character.
        /// </summary>
        /// <param name="lookSource">The ILookSource object attached to the character.</param>
        private void OnAttachLookSource(ILookSource lookSource)
        {
            m_LookSource = lookSource;
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            // Force independent look so the movement type won't try to rotate the character.
            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterForceIndependentLook", true);
        }

        /// <summary>
        /// Called when another ability is attempting to start and the current ability is active.
        /// Returns true or false depending on if the new ability should be blocked from starting.
        /// </summary>
        /// <param name="startingAbility">The ability that is starting.</param>
        /// <returns>True if the ability should be blocked.</returns>
        public override bool ShouldBlockAbilityStart(Ability startingAbility)
        {
            return startingAbility is SpeedChange || startingAbility is HeightChange;
        }

        /// <summary>
        /// Called when the current ability is attempting to start and another ability is active.
        /// Returns true or false depending on if the active ability should be stopped.
        /// </summary>
        /// <param name="activeAbility">The ability that is currently active.</param>
        /// <returns>True if the ability should be stopped.</returns>
        public override bool ShouldStopActiveAbility(Ability activeAbility)
        {
            return activeAbility is SpeedChange || activeAbility is HeightChange;
        }

        /// <summary>
        /// Update the ability's Animator parameters.
        /// </summary>
        public override void Update()
        {
            base.Update();

            var inputVector = m_CharacterLocomotion.RawInputVector;
            if (!m_CharacterLocomotion.FirstPersonPerspective) {
                // The third person move direction can use the forward movement depending on the look source direction.
                if (Mathf.Abs(inputVector.y) > 0) {
                    inputVector.y *= (Vector3.Dot(m_LookSource.LookDirection(true), m_CharacterLocomotion.Rotation * Vector3.forward) < 0 ? -1 : 1);
                }
            }
            m_CharacterLocomotion.InputVector = inputVector;
        }

        /// <summary>
        /// Update the controller's rotation values.
        /// </summary>
        public override void UpdateRotation()
        {
            if (m_GroundTransform == null) {
                return;
            }

            // The character should orient towards the direction of the balance object.
            var targetRotation = Quaternion.Slerp(m_CharacterLocomotion.Rotation, 
                                Quaternion.LookRotation(m_GroundTransform.forward * (Vector3.Dot(m_GroundTransform.forward, m_CharacterLocomotion.Rotation * Vector3.forward) < 0 ? -1 : 1), m_CharacterLocomotion.Up),
                                m_ForceDirectionInfluence);
            m_CharacterLocomotion.DesiredRotation = Quaternion.Inverse(m_CharacterLocomotion.Rotation) * targetRotation;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="force">Was the ability force stopped?</param>
        protected override void AbilityStopped(bool force)
        {
            base.AbilityStopped(force);

            EventHandler.ExecuteEvent(m_GameObject, "OnCharacterForceIndependentLook", false);
        }

        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            EventHandler.UnregisterEvent<ILookSource>(m_GameObject, "OnCharacterAttachLookSource", OnAttachLookSource);
        }
    }
}