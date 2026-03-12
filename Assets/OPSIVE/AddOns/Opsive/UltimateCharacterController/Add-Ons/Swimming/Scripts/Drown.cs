/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.UltimateCharacterController.Character.Abilities;
    using Opsive.UltimateCharacterController.Traits;
    using UnityEngine;

    /// <summary>
    /// Plays a drowning animation when the character is out of breath.
    /// </summary>
    [DefaultStartType(AbilityStartType.Manual)]
    [DefaultAbilityIndex(304)]
    [DefaultAllowPositionalInput(false)]
    [DefaultAllowRotationalInput(false)]
    [DefaultUseGravity(AbilityBoolOverride.False)]
    [DefaultEquippedSlots(0)]
    [Shared.Utility.Group("Swimming Pack")]
    public class Drown : Ability
    {
        [Tooltip("The name of the breath attribute.")]
        [SerializeField] protected string m_BreathAttributeName = "Breath";

        public string BreathAttributeName { get { return m_BreathAttributeName; } set { m_BreathAttributeName = value; } }

        private Attribute m_Attribute;
        private Attribute.AutoUpdateValue m_AutoUpdateValue;
        private Health m_Health;
        private Respawner m_Respawner;

        public override bool CanStayActivatedOnDeath { get { return true; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            if (string.IsNullOrEmpty(m_BreathAttributeName)) {
                Debug.LogError("Error: A breath attribute name on the suffocate ability must be specified.");
                return;
            }

            var attributeManager = m_GameObject.GetCachedComponent<AttributeManager>();
            if (attributeManager == null) {
                Debug.LogError("Error: The character must have an Attribute Manager.");
                return;
            }

            m_Attribute = attributeManager.GetAttribute(m_BreathAttributeName);
            if (m_Attribute == null) {
                Debug.LogError($"Error: Unable to find the attribute with name {m_BreathAttributeName}.");
                return;
            }

            m_Health = m_GameObject.GetCachedComponent<Health>();
            m_Respawner = m_GameObject.GetCachedComponent<Respawner>();

            EventHandler.RegisterEvent(m_Attribute, "OnAttributeReachedDestinationValue", OnOutOfBreath);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorDrownComplete", OnDrownComplete);
        }

        /// <summary>
        /// The character no longer hs any breath left.
        /// </summary>
        private void OnOutOfBreath()
        {
            if (m_Attribute.Value != m_Attribute.MinValue) {
                return;
            }

            StartAbility();
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_AutoUpdateValue = m_Attribute.AutoUpdateValueType;
            m_Attribute.AutoUpdateValueType = Attribute.AutoUpdateValue.None;

            if (m_Health != null) {
                m_Health.ImmediateDeath();
            }
        }

        /// <summary>
        /// The character has finished drowning.
        /// </summary>
        private void OnDrownComplete()
        {
            if (IsActive && m_Respawner != null) {
                m_Respawner.Respawn();
            }

            StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="force">Was the ability force stopped?</param>
        protected override void AbilityStopped(bool force)
        {
            base.AbilityStopped(force);

            m_Attribute.ResetValue();
            m_Attribute.AutoUpdateValueType = m_AutoUpdateValue;
        }

        /// <summary>
        /// The character has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            if (m_Attribute != null) {
                EventHandler.UnregisterEvent(m_Attribute, "OnAttributeReachedDestinationValue", OnOutOfBreath);
                EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorDrownComplete", OnDrownComplete);
            }
        }
    }
}