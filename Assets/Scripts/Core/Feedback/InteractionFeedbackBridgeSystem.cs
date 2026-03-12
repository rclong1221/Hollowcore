using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using DIG.Core.Feedback;
using DIG.CameraSystem;
using DIG.Interaction;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DIG.Core.Feedback
{
    /// <summary>
    /// EPIC 15.23: Bridges ECS interaction state to managed feedback systems.
    ///
    /// Runs in PresentationSystemGroup (ClientSimulation only). Each frame, reads the local
    /// player's InteractAbility and detects state transitions to trigger:
    /// - Hover haptic: brief vibration pulse when target changes
    /// - Hold haptic: increasing vibration during timed interactions
    /// - Complete feedback: sharp pop vibration + FEEL feedback on completion
    /// - Camera shake: during Breach-type hold interactions
    /// - Cancel: stops vibration when interaction is cancelled
    ///
    /// Lives in Core/Feedback (Assembly-CSharp) rather than DIG.Interaction asmdef
    /// because it needs access to GameplayFeedbackManager and CameraShakeEffect.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InteractionFeedbackBridgeSystem : SystemBase
    {
        // Previous frame state for transition detection
        private Entity _previousTarget;
        private bool _previousIsInteracting;
        private float _previousProgress;
        private InteractionVerb _activeVerb;

        // Hover pulse timer (reset motors after 1 frame)
        private float _hoverPulseTimer;
        private const float HoverPulseDuration = 0.08f;

        // Haptic intensity settings
        private const float HoverLowMotor = 0.05f;
        private const float HoldLowMotorMax = 0.15f;
        private const float HoldHighMotorMax = 0.05f;
        private const float CompleteHighMotor = 0.4f;
        private const float CompletePulseDuration = 0.12f;

        // Camera shake settings for Breach-type interactions
        private const float BreachShakeIntensityMax = 0.15f;
        private const float BreachShakeDuration = 0.1f;

        protected override void OnCreate()
        {
            RequireForUpdate<GhostOwnerIsLocal>();
        }

        protected override void OnUpdate()
        {
#if ENABLE_INPUT_SYSTEM
            // Tick hover pulse timer
            if (_hoverPulseTimer > 0f)
            {
                _hoverPulseTimer -= SystemAPI.Time.DeltaTime;
                if (_hoverPulseTimer <= 0f)
                {
                    SetMotorSpeeds(0f, 0f);
                }
            }
#endif

            // Find local player's interaction state
            foreach (var (ability, entity) in
                     SystemAPI.Query<RefRO<InteractAbility>>()
                     .WithAll<CanInteract, GhostOwnerIsLocal>()
                     .WithEntityAccess())
            {
                Entity currentTarget = ability.ValueRO.TargetEntity;
                bool isInteracting = ability.ValueRO.IsInteracting;
                float progress = ability.ValueRO.InteractionProgress;

                // --- Detect hover (target changed to a new non-null entity) ---
                if (currentTarget != Entity.Null && currentTarget != _previousTarget && !isInteracting)
                {
                    OnHover();
                }

                // --- Detect interaction start ---
                if (isInteracting && !_previousIsInteracting)
                {
                    OnInteractionStart(currentTarget);
                }

                // --- Detect interaction complete (was interacting, now not, progress reached 1.0) ---
                if (!isInteracting && _previousIsInteracting && _previousProgress >= 0.95f)
                {
                    OnInteractionComplete();
                }

                // --- Detect interaction cancel (was interacting, now not, progress didn't reach 1.0) ---
                if (!isInteracting && _previousIsInteracting && _previousProgress < 0.95f)
                {
                    OnInteractionCancel();
                }

                // --- Hold haptics (during timed interaction) ---
                if (isInteracting && progress > 0f)
                {
                    OnHoldProgress(progress);
                }

                // Update previous state
                _previousTarget = currentTarget;
                _previousIsInteracting = isInteracting;
                _previousProgress = progress;

                // Only process first local player
                break;
            }
        }

        private void OnHover()
        {
#if ENABLE_INPUT_SYSTEM
            SetMotorSpeeds(HoverLowMotor, 0f);
            _hoverPulseTimer = HoverPulseDuration;
#endif
        }

        private void OnInteractionStart(Entity targetEntity)
        {
            // Cache verb for camera shake decision during hold
            _activeVerb = InteractionVerb.Interact;
            if (targetEntity != Entity.Null && SystemAPI.HasComponent<InteractableContext>(targetEntity))
            {
                _activeVerb = SystemAPI.GetComponent<InteractableContext>(targetEntity).Verb;
            }
        }

        private void OnInteractionComplete()
        {
            // Sharp high-frequency pop
#if ENABLE_INPUT_SYSTEM
            SetMotorSpeeds(0f, CompleteHighMotor);
            _hoverPulseTimer = CompletePulseDuration;
#endif

            // Trigger FEEL feedback
            GameplayFeedbackManager.TriggerInteract();
        }

        private void OnInteractionCancel()
        {
            // Stop all vibration
#if ENABLE_INPUT_SYSTEM
            SetMotorSpeeds(0f, 0f);
            _hoverPulseTimer = 0f;
#endif
        }

        private void OnHoldProgress(float progress)
        {
#if ENABLE_INPUT_SYSTEM
            // Increasing vibration as progress fills
            float low = progress * HoldLowMotorMax;
            float high = progress * HoldHighMotorMax;
            SetMotorSpeeds(low, high);
#endif

            // Camera shake for Breach-type interactions during hold
            if (_activeVerb == InteractionVerb.Breach)
            {
                CameraShakeEffect.TriggerShake(progress * BreachShakeIntensityMax, BreachShakeDuration);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static void SetMotorSpeeds(float low, float high)
        {
            var pad = Gamepad.current;
            if (pad != null)
            {
                pad.SetMotorSpeeds(low, high);
            }
        }
#endif

        protected override void OnDestroy()
        {
#if ENABLE_INPUT_SYSTEM
            SetMotorSpeeds(0f, 0f);
#endif
        }
    }
}
