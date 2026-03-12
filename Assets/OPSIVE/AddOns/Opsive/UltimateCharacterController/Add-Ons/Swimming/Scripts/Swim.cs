/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

namespace Opsive.UltimateCharacterController.AddOns.Swimming
{
    using Opsive.Shared.Events;
    using Opsive.Shared.Game;
    using Opsive.Shared.Input;
    using Opsive.Shared.StateSystem;
    using Opsive.Shared.Utility;
    using Opsive.UltimateCharacterController.Character;
    using Opsive.UltimateCharacterController.Character.Abilities;
    using Opsive.UltimateCharacterController.Utility;
    using UnityEngine;

    /// <summary>
    /// The Swim ability will allow the character to traverse through water and is activated on collision with water layer triggers.
    /// </summary>
    [DefaultAbilityIndex(301)]
    [DefaultUseGravity(AbilityBoolOverride.False)]
    [DefaultUseRootMotionPosition(AbilityBoolOverride.True)]
    [DefaultObjectDetection(ObjectDetectionMode.Trigger)]
    [DefaultDetectGroundCollisions(AbilityBoolOverride.False)]
    [DefaultDetectLayers(1 << 4)] // 4 is the Water layer.
    [DefaultMaxTriggerObjectCount(5)]
    [DefaultState("Swim")]
    [Group("Swimming Pack")]
    public class Swim : DetectObjectAbilityBase
    {
        /// <summary>
        /// Specifies the method for detecting the water height.
        /// </summary>
        public enum WaterHeightDetection
        {
            Collider,   // Detect the water height based on the collider above the character.
            Custom      // A custom water height can be set with the SetWaterHeight method.
        };

        [Tooltip("The water depth that the character can start to swim at.")]
        [SerializeField] protected float m_StartSwimDepth = 1.5f;
        [Tooltip("The water depth that the character should swim and the surface at.")]
        [SerializeField] protected float m_SurfaceSwimDepth = 1.2f;
        [Tooltip("Modifies the default height that the character will swim on the surface at.")]
        [SerializeField] protected float m_SurfaceHeightAdjustment = 0f;
        [Tooltip("The percentage that the gravity force is retained after the character enters the water.")]
        [Range(0, 1)][SerializeField] protected float m_RetainedGravityAmount = 0.48f;
        [Tooltip("Can the character swim underwater?")]
        [SerializeField] protected bool m_CanSwimUnderwater = true;
        [Tooltip("Specifies the camera pitch that the character will transition from a surface swim to underwater swim.")]
        [SerializeField] protected float m_StartUnderwaterSwimPitch = 50f;
        [Tooltip("The minimum surface depth that the character can swim underwater.")]
        [SerializeField] protected float m_MinUnderwaterSwimDepth = 2f;
        [Tooltip("The button mapping to start swimming underwater. An empty value will prevent the mapping from being used.")]
        [SerializeField] protected string m_StartUnderwaterSwimName = "Action";
        [Tooltip("The amount of bouyancy that should be applied to the character while on the surface.")]
        [SerializeField] protected float m_SurfaceBuoyancyAmount = 0.05f;
        [Tooltip("The amount of bouyancy that should be applied to the character while underwater.")]
        [SerializeField] protected float m_UnderwaterBuoyancyAmount = 0.01f;
        [Tooltip("Set to 'Collider' to detected the water height based on the collider, 'Custom' for custom water heights")]
        [SerializeField] protected WaterHeightDetection m_WaterHeightDetectionMethod;
        [Tooltip("The breath attribute that should be modified when the character is underwater.")]
        [SerializeField] protected Traits.AttributeModifier m_BreathModifier = new Traits.AttributeModifier("Breath", -0.3f, Traits.Attribute.AutoUpdateValue.Decrease);
        [Tooltip("Effect that should play when the character enters the water from the air.")]
        [SerializeField] protected WaterEffectVelocity m_EntranceSplash = new WaterEffectVelocity();
        [Tooltip("Effect that should play when the character is swimming on the surface.")]
        [SerializeField] protected WaterEffectVelocityEventDistance[] m_SurfaceSwimSplash;
        [Tooltip("Effect that should play when the character is swimming underwater.")]
        [SerializeField] protected WaterEffectVelocityEvent[] m_UnderwaterSwimMovement;
        [Tooltip("The bubbles that should be spawned when the character is swimming underwater.")]
        [SerializeField] protected WaterEffect m_UnderwaterBubbles;
        [Tooltip("The state that should activate when the character is underwater.")]
        [SerializeField] protected string m_UnderwaterStateName = "Underwater";

        public float StartSwimDepth { get { return m_StartSwimDepth; } set { m_StartSwimDepth = value; } }
        public float SurfaceSwimDepth { get { return m_SurfaceSwimDepth; } set { m_SurfaceSwimDepth = value; } }
        public float SurfaceHeightAdjustment { get { return m_SurfaceHeightAdjustment; } set { m_SurfaceHeightAdjustment = value; } }
        public float RetainedGravityAmount { get { return m_RetainedGravityAmount; } set { m_RetainedGravityAmount = value; } }
        public bool CanSwimUnderwater { get { return m_CanSwimUnderwater; } set { m_CanSwimUnderwater = value; } }
        public float StartUnderwaterSwimPitch { get { return m_StartUnderwaterSwimPitch; } set { m_StartUnderwaterSwimPitch = value; } }
        public float MinUnderwaterSwimDepth { get { return m_MinUnderwaterSwimDepth; } set { m_MinUnderwaterSwimDepth = value; } }
        public float SurfaceBuoyancyAmount { get { return m_SurfaceBuoyancyAmount; } set { m_SurfaceBuoyancyAmount = value; } }
        public float UnderwaterBuoyancyAmount { get { return m_UnderwaterBuoyancyAmount; } set { m_UnderwaterBuoyancyAmount = value; } }
        public WaterHeightDetection WaterHeightDetectionMethod { get { return m_WaterHeightDetectionMethod; } set { m_WaterHeightDetectionMethod = value; } }
        public Traits.AttributeModifier BreathModifier { get { return m_BreathModifier; } set { m_BreathModifier = value; } }
        public string UnderwaterStateName { get { return m_UnderwaterStateName; } set { m_UnderwaterStateName = value; } }

        private UltimateCharacterLocomotionHandler m_Handler;
        private Transform m_Neck;
        private ActiveInputEvent m_UnderwaterSwimInput;
        private BoxCollider m_WaterCollider;

        private bool m_ManualStart;
        private bool m_StartStickToGround;
        private bool m_AirbourneEntrance;
        private int m_DiveDeactivated;
        private float m_WaterSurfaceVerticalPosition;
        private float m_WaterDepth;
        private bool m_TransitionToUnderwaterSwim;

        /// <summary>
        /// Specifies the current swim state that the character is in.
        /// </summary>
        private enum SwimStates { 
            EnterWaterFromAir,  // The character entered the water from the air.
            SurfaceSwim,        // The character is swimming on the surface.
            UnderwaterSwim,     // The character is swimming underwater.
            ExitWaterMoving,    // The character is exiting the water while moving.
            ExitWaterIdle       // The character is exiting the water while idle.
        };

        private SwimStates m_SwimState;
        private SwimStates SwimState { get { return m_SwimState; }
            set {
                if (m_SwimState == value) {
                    return;
                }

                if (m_SwimState == SwimStates.UnderwaterSwim && !string.IsNullOrEmpty(m_UnderwaterStateName)) {
                    StateManager.SetState(m_GameObject, m_UnderwaterStateName, false);
                }

                m_SwimState = value;
                SetAbilityIntDataParameter((int)m_SwimState);
                if (m_SwimState == SwimStates.SurfaceSwim) {
                    EnableDisableBreathModifier(false);
                    if (m_UnderwaterBubbles != null) {
                        m_UnderwaterBubbles.Stop();
                    }
                } else if (m_SwimState == SwimStates.UnderwaterSwim) {
                    m_TransitionToUnderwaterSwim = true;
                    EnableDisableBreathModifier(true);
                    if (!string.IsNullOrEmpty(m_UnderwaterStateName)) {
                        StateManager.SetState(m_GameObject, m_UnderwaterStateName, true);
                    }
                } else if (m_SwimState == SwimStates.ExitWaterMoving || m_SwimState == SwimStates.ExitWaterIdle) {
                    m_CharacterLocomotion.StickToGround = m_StartStickToGround;
                }
            }
        }

        public override int AbilityIntData { get { return (int)m_SwimState; } }
        public override float AbilityFloatData { get { return m_LookSource.Pitch * (m_CharacterLocomotion.RawInputVector.y >= 0 ? 1 : -1); } }
        public BoxCollider WaterCollider { get { return m_WaterCollider; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            if (m_AnimationMonitor != null) {
                var animator = m_AnimationMonitor.gameObject.GetCachedComponent<Animator>();
                m_Neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            }
            if (m_BreathModifier != null) {
                m_BreathModifier.Initialize(m_GameObject);
            }
            if (!string.IsNullOrEmpty(m_StartUnderwaterSwimName)) {
                m_Handler = m_GameObject.GetCachedComponent<UltimateCharacterLocomotionHandler>();
                if (m_Handler != null) {
                    m_UnderwaterSwimInput = GenericObjectPool.Get<ActiveInputEvent>();
                    m_UnderwaterSwimInput.Initialize(ActiveInputEvent.Type.ButtonDown, m_StartUnderwaterSwimName, "OnSwimAbilityStartUnderwaterSwimInput");
                    m_Handler.RegisterInputEvent(m_UnderwaterSwimInput);
                }
                EventHandler.RegisterEvent(m_GameObject, "OnSwimAbilityStartUnderwaterSwimInput", OnStartUnderwaterSwimInput);
            }

            // The underwater bubbles should continue to play until they are stopped.
            if (m_UnderwaterBubbles != null) {
                m_UnderwaterBubbles.Continuous = true;
            }

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorSwimEnteredWater", OnEnteredWater);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorSwimExitedWater", OnExitedWater);
            EventHandler.RegisterEvent<Ability, bool>(m_GameObject, "OnCharacterAbilityActive", OnAbilityActive);

            m_EntranceSplash.Initialize(m_CharacterLocomotion);
            if (m_SurfaceSwimSplash != null) {
                for (int i = 0; i < m_SurfaceSwimSplash.Length; ++i) {
                    if (m_SurfaceSwimSplash[i] == null) {
                        continue;
                    }
                    m_SurfaceSwimSplash[i].Initialize(m_CharacterLocomotion);
                    EventHandler.RegisterEvent(m_GameObject, m_SurfaceSwimSplash[i].EventName, m_SurfaceSwimSplash[i].OnEvent);
                }
            }
            if (m_UnderwaterSwimMovement != null) {
                for (int i = 0; i < m_UnderwaterSwimMovement.Length; ++i) {
                    if (m_UnderwaterSwimMovement[i] == null) {
                        continue;
                    }
                    m_UnderwaterSwimMovement[i].Initialize(m_CharacterLocomotion);
                    EventHandler.RegisterEvent(m_GameObject, m_UnderwaterSwimMovement[i].EventName, m_UnderwaterSwimMovement[i].OnEvent);
                }
            }
        }

        /// <summary>
        /// Tries to start or stop the swim ability without a Box Collider trigger.
        /// </summary>
        /// <param name="start">Should the swim ability be started? If false the ability will be stopped.</param>
        /// <returns>True if the swim ability was able to be started or stopped.</returns>
        public bool TryStartStopSwim(bool start)
        {
            if (start) {
                m_ManualStart = true;
                if (m_CharacterLocomotion.TryStartAbility(this)) {
                    return true;
                }
                m_ManualStart = false;
                return false;
            }
            return m_CharacterLocomotion.TryStopAbility(this);
        }

        /// <summary>
        /// Called when the ablity is tried to be started. If false is returned then the ability will not be started.
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            if (!m_ManualStart) {
                if (!base.CanStartAbility() || m_DetectedTriggerObjectsCount == 0) {
                    return false;
                }

                m_WaterCollider = m_DetectedTriggerObjects[0].GetCachedComponent<BoxCollider>();
                if (m_WaterCollider == null) {
                    Debug.LogWarning("Unable to start the Swim ability. The water collider must have a box collider.");
                    return false;
                }
            } else {
                if (!CanStartAbilityInternal()) {
                    return false;
                }
            }

            // The swim ability can start if the character is in deep enough water or is horizontal and is in shallow water. The latter case will happen
            // when the character dives in.
            float neckWaterDepth;
            var waterDepth = GetDepthInWater(false);
            return waterDepth >= m_StartSwimDepth || 
                    (!m_CharacterLocomotion.Grounded && waterDepth - (neckWaterDepth = GetDepthInWater(true)) < m_CharacterLocomotion.Radius && neckWaterDepth >= m_SurfaceSwimDepth);
        }

        /// <summary>
        /// Sets the local y value of the water position.
        /// </summary>
        /// <param name="verticalPosition">The vertical position to set the water surface level to.</param>
        public void SetWaterSurfacePosition(float verticalPosition)
        {
            m_WaterSurfaceVerticalPosition = verticalPosition;
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();
            
            if (m_Neck == null && m_CharacterLocomotion.PreviousAccelerationInfluence > 0) {
                Debug.LogWarning("For best results the Ultimate Character Locomotion's Previous Acceleration Influence parameter should be set to 0.");
            }

            m_TransitionToUnderwaterSwim = false;
            m_AirbourneEntrance = false;
            if (m_CharacterLocomotion.Grounded) {
                SwimState = SwimStates.SurfaceSwim;
            } else if (GetDepthInWater(true) > m_SurfaceSwimDepth) {
                SwimState = SwimStates.UnderwaterSwim;
            } else {
                SwimState = SwimStates.EnterWaterFromAir;
            }

            // The character won't start to play a swimming animation until the water is at a specified depth.
            m_StartStickToGround = m_CharacterLocomotion.StickToGround;
            m_CharacterLocomotion.StickToGround = false;

            // Play a splashing animation if the character entered the water from the air. Do not play if the character is already underwater or the dive ability recently ended.
            if (m_SwimState != SwimStates.UnderwaterSwim && m_EntranceSplash != null && GetDepthInWater(true) < m_StartSwimDepth && Time.frameCount > m_DiveDeactivated + 1) {
                m_EntranceSplash.Play(m_CharacterLocomotion.TransformPoint(new Vector3(0, GetDepthInWater(false) - m_CharacterLocomotion.SkinWidth, 0)));
                m_AirbourneEntrance = true;
            }
        }

        /// <summary>
        /// The character has entered the water from a fall.
        /// </summary>
        private void OnEnteredWater()
        {
            if (GetDepthInWater(true) > m_SurfaceSwimDepth) {
                SwimState = SwimStates.UnderwaterSwim;
            } else {
                SwimState = SwimStates.SurfaceSwim;
            }
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
        /// Enables or disables the breath modifier.
        /// </summary>
        /// <param name="enable">Should the breath modifier be enabled?</param>
        private void EnableDisableBreathModifier(bool enable)
        {
            if (m_BreathModifier != null) {
                m_BreathModifier.EnableModifier(enable);
            }
        }

        /// <summary>
        /// The character should start to swim underwater.
        /// </summary>
        private void OnStartUnderwaterSwimInput()
        {
            if (m_SwimState != SwimStates.SurfaceSwim) {
                return;
            }

            m_TransitionToUnderwaterSwim = true;
        }

        /// <summary>
        /// Updates the ability.
        /// </summary>
        public override void Update()
        {
            base.Update();

            // Prevent any input when the character is exiting.
            if (m_SwimState == SwimStates.ExitWaterMoving || m_SwimState == SwimStates.ExitWaterIdle) {
                m_CharacterLocomotion.InputVector = Vector3.zero;
                return;
            }

            m_WaterDepth = GetDepthInWater(false);
            // The character can exit the water while moving or while idle. When the character is moving the collider is more horizontal than vertical so the ground must be closer when the character
            // is idle versus moving. When the character is idle they will exit the swim ability when the character is able to stand on the surface.
            if (m_WaterDepth < m_SurfaceSwimDepth && m_CharacterLocomotion.Moving && m_CharacterLocomotion.LocalVelocity.z > 0 &&
                Physics.Raycast(m_CharacterLocomotion.Position + m_WaterDepth * m_CharacterLocomotion.Up, -m_CharacterLocomotion.Up, out var hitSurface, m_SurfaceSwimDepth + m_CharacterLocomotion.Radius,
                m_CharacterLayerManager.SolidObjectLayers, QueryTriggerInteraction.Ignore) &&
                Vector3.Angle(m_CharacterLocomotion.Up, hitSurface.normal) <= m_CharacterLocomotion.SlopeLimit
                ) {
                // No exit animations can play if the character doesn't have an animator.
                if (m_Neck == null) {
                    StopAbility();
                    return;
                }
                SwimState = SwimStates.ExitWaterMoving;
            } else if (m_SwimState == SwimStates.SurfaceSwim && (!m_CharacterLocomotion.Moving || m_CharacterLocomotion.LocalVelocity.z < 0) && m_WaterDepth < m_StartSwimDepth && 
                                Physics.Raycast(m_CharacterLocomotion.Position + m_WaterDepth * m_CharacterLocomotion.Up, -m_CharacterLocomotion.Up, m_StartSwimDepth, m_CharacterLayerManager.SolidObjectLayers, QueryTriggerInteraction.Ignore)) {
                // No exit animations can play if the character doesn't have an animator.
                if (m_Neck == null) {
                    StopAbility();
                    return;
                }
                SwimState = SwimStates.ExitWaterIdle;
            } else {
                // The character can swim underwater if the water is deep enough.
                if (m_SwimState != SwimStates.UnderwaterSwim) {
                    if (m_SwimState == SwimStates.SurfaceSwim && m_CanSwimUnderwater && (m_CharacterLocomotion.Moving && m_LookSource.Pitch > m_StartUnderwaterSwimPitch || m_TransitionToUnderwaterSwim)) {
                        // Don't swim underwater if there isn't enough room.
                        if (!m_CharacterLocomotion.SingleCast(-m_CharacterLocomotion.Up, Vector3.zero, m_MinUnderwaterSwimDepth, m_CharacterLayerManager.SolidObjectLayers, ref m_RaycastResult)) {
                            SwimState = SwimStates.UnderwaterSwim;
                        }
                    } else if (m_WaterDepth > m_MinUnderwaterSwimDepth) {
                        SwimState = SwimStates.UnderwaterSwim;
                    }
                } else if (m_SwimState == SwimStates.UnderwaterSwim && m_UnderwaterBubbles != null && m_UnderwaterBubbles.IsPlaying) {
                    // Keep the bubbles going in the opposite direction of gravity.
                    m_UnderwaterBubbles.Location.up = -m_CharacterLocomotion.GravityDirection;
                }

                // Swim on the surface if the character is near the surface.
                if (m_TransitionToUnderwaterSwim && m_WaterDepth >= m_SurfaceSwimDepth) {
                    m_TransitionToUnderwaterSwim = false;
                    if (m_UnderwaterBubbles != null && !m_UnderwaterBubbles.IsPlaying) {
                        m_UnderwaterBubbles.Play();
                    }
                } else if (!m_TransitionToUnderwaterSwim && m_WaterDepth < m_SurfaceSwimDepth && m_SwimState != SwimStates.SurfaceSwim) {
                    SwimState = SwimStates.SurfaceSwim;
                }
            }

            // The character will move underwater based on the float data. The character should move towards the camera when moving backwards.
            SetAbilityFloatDataParameter(AbilityFloatData);
        }

        /// <summary>
        /// Returns the depth of the water.
        /// </summary>
        /// <param name="useNeck">Should the character's neck be used when determining the position?</param>
        /// <returns>The depth of the water.</returns>
        public float GetDepthInWater(bool useNeck)
        {
            return GetDepthInWater(useNeck && m_Neck != null ? m_Neck : m_Transform);
        }

        /// <summary>
        /// Returns the depth of the water.
        /// </summary>
        /// <param name="location">The location to get the depth of.</param>
        /// <returns>The depth of the water.</returns>
        public float GetDepthInWater(Transform location)
        {
            if (m_WaterCollider == null) {
                return m_WaterSurfaceVerticalPosition;
            }

            if (m_WaterHeightDetectionMethod == WaterHeightDetection.Collider) {
                m_WaterSurfaceVerticalPosition = m_WaterCollider.transform.TransformPoint(m_WaterCollider.center + m_WaterCollider.size / 2).y;
            }

            return MathUtility.InverseTransformPoint(location.position, m_WaterCollider.transform.rotation, m_WaterSurfaceVerticalPosition * Vector3.up).y;
        }

        /// <summary>
        /// Update the controller's position values.
        /// </summary>
        public override void UpdatePosition()
        {
            if (m_SwimState == SwimStates.EnterWaterFromAir || m_SwimState == SwimStates.SurfaceSwim || m_SwimState == SwimStates.UnderwaterSwim) {
                ApplyBouyancy();
                // If the character doesn't have an animator then the ability motor has to move the character in the vertical direction.
                if (m_Neck == null && m_SwimState == SwimStates.UnderwaterSwim) {
                    var verticalDirection = Vector3.ProjectOnPlane(m_LookSource.LookDirection(true), m_CharacterLocomotion.Rotation * Vector3.forward);
                    m_CharacterLocomotion.DesiredMovement += m_CharacterLocomotion.InputVector.y * Time.timeScale * m_CharacterLocomotion.TimeScale * Time.deltaTime * verticalDirection;
                }
            }
            if (m_AirbourneEntrance) {
                var gravityAccumulation = m_CharacterLocomotion.GravityAccumulation;
                m_CharacterLocomotion.DesiredMovement += m_CharacterLocomotion.GravityDirection * gravityAccumulation;
                gravityAccumulation *= m_RetainedGravityAmount;
                m_CharacterLocomotion.GravityAccumulation = Mathf.Max(gravityAccumulation, 0);
                if (m_CharacterLocomotion.GravityAccumulation <= 0.001f) {
                    m_AirbourneEntrance = false;
                    m_CharacterLocomotion.GravityAccumulation = 0;
                }
            }
        }

        /// <summary>
        /// Applies bouyancy to the character.
        /// </summary>
        private void ApplyBouyancy()
        {
            if (m_SwimState == SwimStates.EnterWaterFromAir || m_SwimState == SwimStates.SurfaceSwim) {
                // Move with the water surface. Move the character down immediately, but add a delta time for moving up to act as buoyancy.
                var adjustment = GetDepthInWater(true) + m_SurfaceHeightAdjustment;
                if (adjustment > 0) {
                    adjustment *= m_SurfaceBuoyancyAmount * m_CharacterLocomotion.TimeScale * Time.timeScale;
                }
                m_CharacterLocomotion.DesiredMovement = adjustment * m_CharacterLocomotion.Up;
            } else if (m_SwimState == SwimStates.UnderwaterSwim && !m_CharacterLocomotion.Moving) {
                // Character is underwater - apply a slight buoyancy.
                m_CharacterLocomotion.DesiredMovement = m_UnderwaterBuoyancyAmount * m_CharacterLocomotion.TimeScale * Time.timeScale * m_CharacterLocomotion.Up;
            }
        }

        /// <summary>
        /// The character has exited a trigger.
        /// </summary>
        /// <param name="other">The trigger collider that the character exited.</param>
        public override void OnTriggerExit(Collider other)
        {
            base.OnTriggerExit(other);

            // Stop the ability when the character is no longer in a water trigger.
            if (other.gameObject.layer == Game.LayerManager.Water) {
                if (m_DetectedTriggerObjectsCount == 0) {
                    StopAbility();
                    return;
                }

                m_WaterCollider = m_DetectedTriggerObjects[0].GetCachedComponent<BoxCollider>();
                if (m_WaterCollider == null) {
                    StopAbility();
                }
            }
        }

        /// <summary>
        /// The exit out animation has stopped the ability.
        /// </summary>
        private void OnExitedWater()
        {
			StopAbility();
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="force">Was the ability force stopped?</param>
        protected override void AbilityStopped(bool force)
        {
            m_ManualStart = false;
            m_WaterCollider = null;
            m_CharacterLocomotion.StickToGround = m_StartStickToGround;
            EnableDisableBreathModifier(false);
            if (m_UnderwaterBubbles != null) {
                m_UnderwaterBubbles.Stop();
            }
            if (!string.IsNullOrEmpty(m_UnderwaterStateName)) {
                StateManager.SetState(m_GameObject, m_UnderwaterStateName, false);
            }

            base.AbilityStopped(force);
        }

        /// <summary>
        /// The character's ability has been started or stopped.
        /// </summary>
        /// <param name="ability">The ability which was started or stopped.</param>
        /// <param name="active">True if the ability was started, false if it was stopped.</param>
        private void OnAbilityActive(Ability ability, bool active)
        {
            if (active || !(ability is Dive)) {
                return;
            }

            m_DiveDeactivated = Time.frameCount;
        }

        /// <summary>
        /// The character's model has switched.
        /// </summary>
        /// <param name="activeModel">The active character model.</param>
        protected override void OnSwitchModels(GameObject activeModel)
        {
            base.OnSwitchModels(activeModel);

            if (m_AnimationMonitor != null) {
                var animator = m_AnimationMonitor.gameObject.GetCachedComponent<Animator>();
                m_Neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            }
        }

        /// <summary>
        /// The character has been destroyed.
        /// </summary>
		public override void OnDestroy()
        {
            base.OnDestroy();

            if (m_UnderwaterSwimInput != null) {
                m_Handler.UnregisterInputEvent(m_UnderwaterSwimInput);
                GenericObjectPool.Return(m_UnderwaterSwimInput);
            }
            
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorSwimEnteredWater", OnEnteredWater);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorSwimExitedWater", OnExitedWater);
            EventHandler.UnregisterEvent(m_GameObject, "OnSwimAbilityStartUnderwaterSwimInput", OnStartUnderwaterSwimInput);

            if (m_SurfaceSwimSplash != null) {
                for (int i = 0; i < m_SurfaceSwimSplash.Length; ++i) {
                    if (m_SurfaceSwimSplash[i] == null) {
                        continue;
                    }
                    EventHandler.UnregisterEvent(m_GameObject, m_SurfaceSwimSplash[i].EventName, m_SurfaceSwimSplash[i].OnEvent);
                }
            }
            if (m_UnderwaterSwimMovement != null) {
                for (int i = 0; i < m_UnderwaterSwimMovement.Length; ++i) {
                    if (m_UnderwaterSwimMovement[i] == null) {
                        continue;
                    }
                    EventHandler.UnregisterEvent(m_GameObject, m_UnderwaterSwimMovement[i].EventName, m_UnderwaterSwimMovement[i].OnEvent);
                }
            }
        }
    }
}
