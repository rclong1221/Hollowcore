using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// EPIC 13.17: Processes player interaction requests and manages interaction state.
    ///
    /// Features:
    /// - 13.17.1: Animation event synchronization (WaitForAnimStart, WaitForAnimComplete)
    /// - 13.17.4: ID filtering (RequiredInteractableID, InteractableTypeMask)
    /// - Basic interaction flow for all InteractableTypes
    ///
    /// This system is Burst-compiled and runs in the PredictedSimulationSystemGroup
    /// for proper network prediction support.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(InteractableDetectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InteractAbilitySystem : ISystem
    {
        private const float DefaultAnimEventTimeout = 2.0f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Process entities with InteractAbilityState (animation-aware interactions)
            foreach (var (ability, abilityState, request, entity) in
                     SystemAPI.Query<RefRW<InteractAbility>, RefRW<InteractAbilityState>, RefRW<InteractRequest>>()
                     .WithAll<Simulate, CanInteract>()
                     .WithEntityAccess())
            {
                ProcessInteractionWithAnimState(ref state, ref ability.ValueRW, ref abilityState.ValueRW,
                    ref request.ValueRW, entity, deltaTime);
            }

            // Process entities without InteractAbilityState (legacy/simple interactions)
            foreach (var (ability, request, entity) in
                     SystemAPI.Query<RefRW<InteractAbility>, RefRW<InteractRequest>>()
                     .WithNone<InteractAbilityState>()
                     .WithAll<Simulate, CanInteract>()
                     .WithEntityAccess())
            {
                ProcessSimpleInteraction(ref state, ref ability.ValueRW, ref request.ValueRW, entity, deltaTime);
            }
        }

        /// <summary>
        /// Process interaction for entities with animation state support (EPIC 13.17.1).
        /// </summary>
        [BurstCompile]
        private void ProcessInteractionWithAnimState(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, ref InteractRequest request, Entity entity, float deltaTime)
        {
            // Handle cancel
            if (request.CancelInteract && ability.IsInteracting)
            {
                CancelInteraction(ref state, ref ability, ref abilityState, entity);
                request.CancelInteract = false;
                return;
            }

            // Handle start interaction
            if (request.StartInteract && !ability.IsInteracting)
            {
                Entity target = request.TargetEntity != Entity.Null ?
                                request.TargetEntity : ability.TargetEntity;

                if (target != Entity.Null && SystemAPI.HasComponent<Interactable>(target))
                {
                    var interactable = SystemAPI.GetComponent<Interactable>(target);

                    // EPIC 13.17.4: ID Filtering (inlined for Burst compatibility)
                    bool passesFilter = true;
                    if (ability.RequiredInteractableID != 0 &&
                        interactable.InteractableID != ability.RequiredInteractableID)
                    {
                        passesFilter = false;
                    }
                    if (passesFilter && ability.InteractableTypeMask != 0)
                    {
                        int typeBit = 1 << (int)interactable.Type;
                        if ((ability.InteractableTypeMask & typeBit) == 0)
                        {
                            passesFilter = false;
                        }
                    }

                    if (passesFilter && interactable.CanInteract)
                    {
                        StartInteractionWithAnimState(ref state, ref ability, ref abilityState, target, interactable, entity);
                    }
                }
                request.StartInteract = false;
            }

            // Process animation event waiting phases (EPIC 13.17.1)
            ProcessAnimationEventPhases(ref state, ref ability, ref abilityState, entity, deltaTime);

            // Update ongoing interaction
            if (ability.IsInteracting && ability.TargetEntity != Entity.Null)
            {
                if (!SystemAPI.HasComponent<Interactable>(ability.TargetEntity))
                {
                    CancelInteraction(ref state, ref ability, ref abilityState, entity);
                    return;
                }

                var interactable = SystemAPI.GetComponent<Interactable>(ability.TargetEntity);

                // Only update progress for non-animation-waiting phases
                if (abilityState.Phase == InteractionPhase.InProgress)
                {
                    UpdateInteractionProgress(ref state, ref ability, ref abilityState, ref request,
                        entity, interactable, deltaTime);
                }
            }

            // Clear consumed request
            request.StartInteract = false;
        }

        /// <summary>
        /// Process simple interaction for entities without animation state (legacy path).
        /// </summary>
        [BurstCompile]
        private void ProcessSimpleInteraction(ref SystemState state, ref InteractAbility ability,
            ref InteractRequest request, Entity entity, float deltaTime)
        {
            // Handle cancel
            if (request.CancelInteract && ability.IsInteracting)
            {
                CancelInteractionSimple(ref state, ref ability, entity);
                request.CancelInteract = false;
                return;
            }

            // Handle start interaction
            if (request.StartInteract && !ability.IsInteracting)
            {
                Entity target = request.TargetEntity != Entity.Null ?
                                request.TargetEntity : ability.TargetEntity;

                if (target != Entity.Null && SystemAPI.HasComponent<Interactable>(target))
                {
                    var interactable = SystemAPI.GetComponent<Interactable>(target);

                    // EPIC 13.17.4: ID Filtering (inlined for Burst compatibility)
                    bool passesFilter = true;
                    if (ability.RequiredInteractableID != 0 &&
                        interactable.InteractableID != ability.RequiredInteractableID)
                    {
                        passesFilter = false;
                    }
                    if (passesFilter && ability.InteractableTypeMask != 0)
                    {
                        int typeBit = 1 << (int)interactable.Type;
                        if ((ability.InteractableTypeMask & typeBit) == 0)
                        {
                            passesFilter = false;
                        }
                    }

                    if (passesFilter && interactable.CanInteract)
                    {
                        StartInteractionSimple(ref state, ref ability, target, interactable, entity);
                    }
                }
                request.StartInteract = false;
            }

            // Update ongoing interaction
            if (ability.IsInteracting && ability.TargetEntity != Entity.Null)
            {
                if (!SystemAPI.HasComponent<Interactable>(ability.TargetEntity))
                {
                    CancelInteractionSimple(ref state, ref ability, entity);
                    return;
                }

                var interactable = SystemAPI.GetComponent<Interactable>(ability.TargetEntity);

                switch (interactable.Type)
                {
                    case InteractableType.Instant:
                        // EPIC 16.1 Phase 6: Complete ranged instant once projectile arrives
                        if (SystemAPI.HasComponent<RangedInteractionState>(ability.TargetEntity))
                        {
                            var rangedState = SystemAPI.GetComponent<RangedInteractionState>(ability.TargetEntity);
                            if (rangedState.IsConnecting)
                                break; // Still in flight

                            TriggerInteractionEffect(ref state, ability.TargetEntity, entity);
                            ability.IsInteracting = false;
                            ability.InteractionProgress = 0f;
                            ability.TargetEntity = Entity.Null;
                        }
                        break;

                    case InteractableType.Timed:
                        // EPIC 16.1 Phase 5: If a minigame is active, gate completion on its result
                        if (SystemAPI.HasComponent<MinigameState>(ability.TargetEntity))
                        {
                            var mgState = SystemAPI.GetComponent<MinigameState>(ability.TargetEntity);
                            if (mgState.IsActive)
                                break;
                            if (mgState.Failed)
                            {
                                var mgConfig = SystemAPI.GetComponent<MinigameConfig>(ability.TargetEntity);
                                if (mgConfig.FailEndsInteraction)
                                {
                                    CancelInteractionSimple(ref state, ref ability, entity);
                                    break;
                                }
                            }
                        }

                        ability.InteractionProgress += deltaTime / interactable.HoldDuration;
                        if (ability.InteractionProgress >= 1f)
                        {
                            CompleteInteractionSimple(ref state, ref ability, entity);
                        }
                        else if (SystemAPI.HasComponent<InteractableState>(ability.TargetEntity))
                        {
                            var interactState = SystemAPI.GetComponentRW<InteractableState>(ability.TargetEntity);
                            interactState.ValueRW.Progress = ability.InteractionProgress;
                        }
                        break;

                    case InteractableType.Continuous:
                        if (!request.StartInteract)
                        {
                            CancelInteractionSimple(ref state, ref ability, entity);
                        }
                        break;

                    case InteractableType.Toggle:
                    case InteractableType.Animated:
                    case InteractableType.MultiPhase:
                        // Handled by specific systems
                        break;
                }
            }

            request.StartInteract = false;
        }

        /// <summary>
        /// EPIC 13.17.1: Process animation event waiting phases.
        /// </summary>
        [BurstCompile]
        private void ProcessAnimationEventPhases(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, Entity entity, float deltaTime)
        {
            if (!ability.IsInteracting)
                return;

            switch (abilityState.Phase)
            {
                case InteractionPhase.WaitingForAnimStart:
                    // Waiting for OnAnimatorInteract event
                    abilityState.AnimEventTimeout += deltaTime;

                    if (abilityState.AnimatorInteractReceived ||
                        abilityState.AnimEventTimeout >= abilityState.MaxAnimEventTimeout)
                    {
                        // Proceed to InProgress
                        abilityState.Phase = InteractionPhase.InProgress;
                        abilityState.WaitingForAnimatorInteract = false;
                        abilityState.AnimatorInteractReceived = false;

                        // Trigger the actual interaction effect
                        TriggerInteractionEffect(ref state, ability.TargetEntity, entity);
                    }
                    break;

                case InteractionPhase.WaitingForAnimEnd:
                    // Waiting for OnAnimatorInteractComplete event
                    abilityState.AnimEventTimeout += deltaTime;

                    if (abilityState.AnimatorCompleteReceived ||
                        abilityState.AnimEventTimeout >= abilityState.MaxAnimEventTimeout)
                    {
                        // Proceed to completion
                        abilityState.Phase = InteractionPhase.Completing;
                        abilityState.WaitingForAnimatorComplete = false;
                        abilityState.AnimatorCompleteReceived = false;

                        FinalizeInteraction(ref state, ref ability, ref abilityState, entity);
                    }
                    break;
            }
        }

        /// <summary>
        /// Update interaction progress for timed/continuous interactions.
        /// </summary>
        [BurstCompile]
        private void UpdateInteractionProgress(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, ref InteractRequest request, Entity entity,
            Interactable interactable, float deltaTime)
        {
            switch (interactable.Type)
            {
                case InteractableType.Instant:
                    // EPIC 16.1 Phase 6: Complete ranged instant interaction once projectile arrives
                    if (SystemAPI.HasComponent<RangedInteractionState>(ability.TargetEntity))
                    {
                        var rangedState = SystemAPI.GetComponent<RangedInteractionState>(ability.TargetEntity);
                        if (rangedState.IsConnecting)
                            break; // Still in flight

                        // Projectile arrived — complete now
                        TriggerInteractionEffect(ref state, ability.TargetEntity, entity);
                        CompleteInteraction(ref state, ref ability, ref abilityState, entity);
                    }
                    break;

                case InteractableType.Timed:
                    // EPIC 16.1 Phase 5: If a minigame is active, gate completion on its result
                    if (SystemAPI.HasComponent<MinigameState>(ability.TargetEntity))
                    {
                        var mgState = SystemAPI.GetComponent<MinigameState>(ability.TargetEntity);
                        if (mgState.IsActive)
                        {
                            // Minigame running — don't advance hold timer, wait for result
                            break;
                        }
                        if (mgState.Failed)
                        {
                            var mgConfig = SystemAPI.GetComponent<MinigameConfig>(ability.TargetEntity);
                            if (mgConfig.FailEndsInteraction)
                            {
                                CancelInteraction(ref state, ref ability, ref abilityState, entity);
                                break;
                            }
                        }
                    }

                    ability.InteractionProgress += deltaTime / interactable.HoldDuration;
                    if (ability.InteractionProgress >= 1f)
                    {
                        // Check if we need to wait for animation complete
                        bool waitForAnimComplete = false;
                        if (SystemAPI.HasComponent<InteractableAnimationConfig>(ability.TargetEntity))
                        {
                            var animConfig = SystemAPI.GetComponent<InteractableAnimationConfig>(ability.TargetEntity);
                            waitForAnimComplete = animConfig.WaitForAnimComplete;
                        }

                        if (waitForAnimComplete)
                        {
                            abilityState.Phase = InteractionPhase.WaitingForAnimEnd;
                            abilityState.WaitingForAnimatorComplete = true;
                            abilityState.AnimEventTimeout = 0f;
                        }
                        else
                        {
                            CompleteInteraction(ref state, ref ability, ref abilityState, entity);
                        }
                    }
                    else if (SystemAPI.HasComponent<InteractableState>(ability.TargetEntity))
                    {
                        var interactState = SystemAPI.GetComponentRW<InteractableState>(ability.TargetEntity);
                        interactState.ValueRW.Progress = ability.InteractionProgress;
                    }
                    break;

                case InteractableType.Continuous:
                    if (!request.StartInteract)
                    {
                        CancelInteraction(ref state, ref ability, ref abilityState, entity);
                    }
                    break;

                case InteractableType.Toggle:
                case InteractableType.Animated:
                    // State managed elsewhere
                    break;

                // EPIC 16.1 Phase 3: Multi-phase sequence progress
                case InteractableType.MultiPhase:
                    if (SystemAPI.HasComponent<InteractionPhaseState>(ability.TargetEntity))
                    {
                        var phaseState = SystemAPI.GetComponent<InteractionPhaseState>(ability.TargetEntity);
                        if (phaseState.SequenceComplete)
                        {
                            TriggerInteractionEffect(ref state, ability.TargetEntity, entity);
                            CompleteInteraction(ref state, ref ability, ref abilityState, entity);
                        }
                        else if (!phaseState.IsActive)
                        {
                            // Cancelled by PhaseSequenceSystem (failure without ResetOnFail)
                            CancelInteraction(ref state, ref ability, ref abilityState, entity);
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Start interaction with animation state support.
        /// </summary>
        private void StartInteractionWithAnimState(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, Entity target, Interactable interactable, Entity interactor)
        {
            ability.IsInteracting = true;
            ability.TargetEntity = target;
            ability.InteractionProgress = 0f;

            // Reset animation state
            abilityState.AnimatorInteractReceived = false;
            abilityState.AnimatorCompleteReceived = false;
            abilityState.AnimEventTimeout = 0f;
            abilityState.MaxAnimEventTimeout = DefaultAnimEventTimeout;

            // Get animation config if present
            bool waitForAnimStart = false;
            if (SystemAPI.HasComponent<InteractableAnimationConfig>(target))
            {
                var animConfig = SystemAPI.GetComponent<InteractableAnimationConfig>(target);
                waitForAnimStart = animConfig.WaitForAnimStart;
                abilityState.MaxAnimEventTimeout = animConfig.AnimEventTimeout > 0 ?
                    animConfig.AnimEventTimeout : DefaultAnimEventTimeout;
                abilityState.AnimatorIntData = animConfig.AnimatorIntData;
            }

            // Update interactable state
            if (SystemAPI.HasComponent<InteractableState>(target))
            {
                var interactState = SystemAPI.GetComponentRW<InteractableState>(target);
                interactState.ValueRW.IsBeingInteracted = true;
                interactState.ValueRW.Progress = 0f;
            }

            // Determine initial phase
            if (waitForAnimStart && interactable.Type == InteractableType.Animated)
            {
                // Wait for animation event before triggering effect
                abilityState.Phase = InteractionPhase.WaitingForAnimStart;
                abilityState.WaitingForAnimatorInteract = true;
            }
            else
            {
                // Proceed directly to effect
                abilityState.Phase = InteractionPhase.InProgress;

                // Handle instant/toggle types
                if (interactable.Type == InteractableType.Instant ||
                    interactable.Type == InteractableType.Toggle)
                {
                    // EPIC 16.1 Phase 6: If ranged projectile still in flight, wait
                    if (SystemAPI.HasComponent<RangedInteractionState>(target))
                    {
                        var rangedState = SystemAPI.GetComponent<RangedInteractionState>(target);
                        if (rangedState.IsConnecting)
                            return; // Will complete once projectile arrives
                    }

                    TriggerInteractionEffect(ref state, target, interactor);
                    CompleteInteraction(ref state, ref ability, ref abilityState, interactor);
                }
                // EPIC 16.1 Phase 3: Activate multi-phase sequence on target
                else if (interactable.Type == InteractableType.MultiPhase)
                {
                    ActivateMultiPhaseSequence(ref state, target, interactor);
                }
            }
        }

        /// <summary>
        /// Trigger the interaction effect on the target.
        /// EPIC 13.17.6-13.17.9: Enhanced with audio, animator, and toggle support.
        /// </summary>
        private void TriggerInteractionEffect(ref SystemState state, Entity target, Entity interactor)
        {
            if (target == Entity.Null)
                return;

            // Toggle animated interactable state (with EPIC 13.17.6-13.17.9 enhancements)
            if (SystemAPI.HasComponent<AnimatedInteractable>(target))
            {
                var animated = SystemAPI.GetComponentRW<AnimatedInteractable>(target);
                ref var animRef = ref animated.ValueRW;

                // EPIC 13.17.7: Check single interact restriction
                if (animRef.SingleInteract && animRef.HasInteracted)
                {
                    return; // Already used once
                }

                // EPIC 13.17.8: Handle multi-switch group logic
                // If in a group with ToggleBoolValue, check for active switch
                if (animRef.SwitchGroupID != 0 && animRef.ToggleBoolValue)
                {
                    // If another switch is active, that one should handle it
                    // Otherwise, we become the active one
                    animRef.IsActiveBoolInteractable = true;
                }

                // Toggle state
                bool newOpenState = !animRef.IsOpen;

                // EPIC 13.17.8: Apply toggle behavior
                if (animRef.ToggleBoolValue)
                {
                    animRef.IsOpen = newOpenState;
                    animRef.IsActiveBoolInteractable = newOpenState;
                }
                else
                {
                    // Non-toggle: always set to true (one-shot behavior)
                    animRef.IsOpen = true;
                }

                animRef.IsAnimating = true;
                animRef.CurrentTime = 0f;

                // EPIC 13.17.6: Advance audio clip index (managed bridge will play it)
                if (SystemAPI.HasComponent<InteractableAudioConfig>(target))
                {
                    var audioConfig = SystemAPI.GetComponent<InteractableAudioConfig>(target);
                    if (audioConfig.ClipCount > 0)
                    {
                        if (audioConfig.SequentialCycle)
                        {
                            animRef.AudioClipIndex = (animRef.AudioClipIndex + 1) % audioConfig.ClipCount;
                        }
                        else
                        {
                            // Random selection will be handled by managed bridge
                            animRef.AudioClipIndex = -2; // Signal random selection
                        }
                    }
                }

                // EPIC 13.17.7: Mark as interacted
                animRef.HasInteracted = true;
            }

            // Toggle lever state
            if (SystemAPI.HasComponent<LeverInteractable>(target))
            {
                var lever = SystemAPI.GetComponentRW<LeverInteractable>(target);
                lever.ValueRW.IsActivated = !lever.ValueRW.IsActivated;
            }

            // EPIC 16.1: Enter station session
            if (interactor != Entity.Null && SystemAPI.HasComponent<InteractionSession>(target))
            {
                var session = SystemAPI.GetComponentRW<InteractionSession>(target);
                if (!session.ValueRW.IsOccupied || session.ValueRW.AllowConcurrentUsers)
                {
                    session.ValueRW.IsOccupied = true;
                    session.ValueRW.OccupantEntity = interactor;

                    if (SystemAPI.HasComponent<StationSessionState>(interactor))
                    {
                        var sessionState = SystemAPI.GetComponentRW<StationSessionState>(interactor);
                        sessionState.ValueRW.IsInSession = true;
                        sessionState.ValueRW.SessionEntity = target;
                    }
                }
            }

            // EPIC 16.1 Phase 4: Enter mount
            if (interactor != Entity.Null && SystemAPI.HasComponent<MountPoint>(target))
            {
                var mount = SystemAPI.GetComponentRW<MountPoint>(target);
                if (!mount.ValueRO.IsOccupied)
                {
                    mount.ValueRW.IsOccupied = true;
                    mount.ValueRW.OccupantEntity = interactor;

                    if (SystemAPI.HasComponent<MountState>(interactor))
                    {
                        var mountState = SystemAPI.GetComponentRW<MountState>(interactor);
                        mountState.ValueRW.MountedOn = target;
                        mountState.ValueRW.IsTransitioning = true;
                        mountState.ValueRW.TransitionProgress = 0;
                        mountState.ValueRW.ActiveMountType = mount.ValueRO.Type;

                        if (SystemAPI.HasComponent<LocalTransform>(interactor))
                            mountState.ValueRW.PreMountPosition =
                                SystemAPI.GetComponent<LocalTransform>(interactor).Position;
                    }
                }
            }

            // EPIC 16.1 Phase 5: Activate minigame
            if (interactor != Entity.Null && SystemAPI.HasComponent<MinigameConfig>(target))
            {
                if (SystemAPI.HasComponent<MinigameState>(target))
                {
                    var mgState = SystemAPI.GetComponentRW<MinigameState>(target);
                    mgState.ValueRW.IsActive = true;
                    mgState.ValueRW.Succeeded = false;
                    mgState.ValueRW.Failed = false;
                    mgState.ValueRW.PerformingEntity = interactor;
                    var mgConfig = SystemAPI.GetComponent<MinigameConfig>(target);
                    mgState.ValueRW.TimeRemaining = mgConfig.TimeLimit;
                    mgState.ValueRW.Score = 0;
                }
            }

            // EPIC 16.1 Phase 7: Join coop interaction
            if (interactor != Entity.Null && SystemAPI.HasComponent<CoopInteraction>(target))
            {
                var coopSlots = SystemAPI.GetBuffer<CoopSlot>(target);
                for (int i = 0; i < coopSlots.Length; i++)
                {
                    if (!coopSlots[i].IsOccupied)
                    {
                        var slot = coopSlots[i];
                        slot.IsOccupied = true;
                        slot.PlayerEntity = interactor;
                        coopSlots[i] = slot;

                        if (SystemAPI.HasComponent<CoopParticipantState>(interactor))
                        {
                            var participantState = SystemAPI.GetComponentRW<CoopParticipantState>(interactor);
                            participantState.ValueRW.CoopEntity = target;
                            participantState.ValueRW.AssignedSlot = slot.SlotIndex;
                            participantState.ValueRW.IsInCoop = true;
                            participantState.ValueRW.HasConfirmed = false;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Complete interaction with animation state support.
        /// </summary>
        private void CompleteInteraction(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, Entity interactor)
        {
            // Clear interactable state
            if (ability.TargetEntity != Entity.Null &&
                SystemAPI.HasComponent<InteractableState>(ability.TargetEntity))
            {
                var interactState = SystemAPI.GetComponentRW<InteractableState>(ability.TargetEntity);
                interactState.ValueRW.IsBeingInteracted = false;
                interactState.ValueRW.Progress = 0f;
            }

            // Reset ability state
            ability.IsInteracting = false;
            ability.InteractionProgress = 0f;
            ability.TargetEntity = Entity.Null;

            // Reset animation state
            abilityState.Phase = InteractionPhase.None;
            abilityState.WaitingForAnimatorInteract = false;
            abilityState.WaitingForAnimatorComplete = false;
            abilityState.AnimatorInteractReceived = false;
            abilityState.AnimatorCompleteReceived = false;
            abilityState.AnimEventTimeout = 0f;
        }

        /// <summary>
        /// Finalize interaction after animation complete event.
        /// </summary>
        private void FinalizeInteraction(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, Entity interactor)
        {
            CompleteInteraction(ref state, ref ability, ref abilityState, interactor);
        }

        /// <summary>
        /// Cancel interaction with animation state support.
        /// </summary>
        private void CancelInteraction(ref SystemState state, ref InteractAbility ability,
            ref InteractAbilityState abilityState, Entity interactor)
        {
            if (ability.TargetEntity != Entity.Null &&
                SystemAPI.HasComponent<InteractableState>(ability.TargetEntity))
            {
                var interactState = SystemAPI.GetComponentRW<InteractableState>(ability.TargetEntity);
                interactState.ValueRW.IsBeingInteracted = false;
                interactState.ValueRW.Progress = 0f;
            }

            // EPIC 16.1 Phase 3: Deactivate multi-phase if present
            DeactivateMultiPhase(ref state, ability.TargetEntity);

            // EPIC 16.1 Phase 7: Leave coop interaction
            LeaveCoop(ref state, interactor);

            ability.IsInteracting = false;
            ability.InteractionProgress = 0f;

            // Reset animation state
            abilityState.Phase = InteractionPhase.None;
            abilityState.WaitingForAnimatorInteract = false;
            abilityState.WaitingForAnimatorComplete = false;
            abilityState.AnimatorInteractReceived = false;
            abilityState.AnimatorCompleteReceived = false;
            abilityState.AnimEventTimeout = 0f;
        }

        // --- Simple interaction methods (no animation state) ---

        private void StartInteractionSimple(ref SystemState state, ref InteractAbility ability,
            Entity target, Interactable interactable, Entity interactor)
        {
            ability.IsInteracting = true;
            ability.TargetEntity = target;
            ability.InteractionProgress = 0f;

            if (SystemAPI.HasComponent<InteractableState>(target))
            {
                var interactState = SystemAPI.GetComponentRW<InteractableState>(target);
                interactState.ValueRW.IsBeingInteracted = true;
                interactState.ValueRW.Progress = 0f;
            }

            if (interactable.Type == InteractableType.Instant)
            {
                // EPIC 16.1 Phase 6: If ranged projectile still in flight, stay in progress
                if (SystemAPI.HasComponent<RangedInteractionState>(target))
                {
                    var rangedState = SystemAPI.GetComponent<RangedInteractionState>(target);
                    if (rangedState.IsConnecting)
                        return; // Will complete via update loop once projectile arrives
                }

                TriggerInteractionEffect(ref state, target, interactor);
                ability.IsInteracting = false;
                ability.InteractionProgress = 0f;
            }
            else if (interactable.Type == InteractableType.Toggle)
            {
                TriggerInteractionEffect(ref state, target, interactor);
                ability.IsInteracting = false;
                ability.InteractionProgress = 0f;
            }
            // EPIC 16.1 Phase 3: Activate multi-phase sequence
            else if (interactable.Type == InteractableType.MultiPhase)
            {
                ActivateMultiPhaseSequence(ref state, target, interactor);
            }
        }

        private void CompleteInteractionSimple(ref SystemState state, ref InteractAbility ability, Entity interactor)
        {
            if (ability.TargetEntity != Entity.Null)
            {
                TriggerInteractionEffect(ref state, ability.TargetEntity, interactor);

                if (SystemAPI.HasComponent<InteractableState>(ability.TargetEntity))
                {
                    var interactState = SystemAPI.GetComponentRW<InteractableState>(ability.TargetEntity);
                    interactState.ValueRW.IsBeingInteracted = false;
                    interactState.ValueRW.Progress = 0f;
                }
            }

            ability.IsInteracting = false;
            ability.InteractionProgress = 0f;
            ability.TargetEntity = Entity.Null;
        }

        private void CancelInteractionSimple(ref SystemState state, ref InteractAbility ability,
            Entity interactor)
        {
            if (ability.TargetEntity != Entity.Null &&
                SystemAPI.HasComponent<InteractableState>(ability.TargetEntity))
            {
                var interactState = SystemAPI.GetComponentRW<InteractableState>(ability.TargetEntity);
                interactState.ValueRW.IsBeingInteracted = false;
                interactState.ValueRW.Progress = 0f;
            }

            // EPIC 16.1 Phase 3: Deactivate multi-phase if present
            DeactivateMultiPhase(ref state, ability.TargetEntity);

            // EPIC 16.1 Phase 7: Leave coop interaction
            LeaveCoop(ref state, interactor);

            ability.IsInteracting = false;
            ability.InteractionProgress = 0f;
        }

        // --- EPIC 16.1 Phase 7: Coop helpers ---

        private void LeaveCoop(ref SystemState state, Entity interactor)
        {
            if (interactor == Entity.Null || !SystemAPI.HasComponent<CoopParticipantState>(interactor))
                return;

            var participantState = SystemAPI.GetComponentRW<CoopParticipantState>(interactor);
            if (!participantState.ValueRO.IsInCoop || participantState.ValueRO.CoopEntity == Entity.Null)
                return;

            Entity coopEntity = participantState.ValueRO.CoopEntity;
            if (SystemAPI.HasBuffer<CoopSlot>(coopEntity))
            {
                var slots = SystemAPI.GetBuffer<CoopSlot>(coopEntity);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].PlayerEntity == interactor)
                    {
                        var slot = slots[i];
                        slot.IsOccupied = false;
                        slot.PlayerEntity = Entity.Null;
                        slot.IsReady = false;
                        slots[i] = slot;
                        break;
                    }
                }
            }

            participantState.ValueRW.IsInCoop = false;
            participantState.ValueRW.CoopEntity = Entity.Null;
            participantState.ValueRW.AssignedSlot = -1;
            participantState.ValueRW.HasConfirmed = false;
        }

        // --- EPIC 16.1 Phase 3: Multi-phase helpers ---

        /// <summary>
        /// Activate a multi-phase sequence on the target interactable.
        /// Sets InteractionPhaseState.IsActive, CurrentPhase=0, PerformingEntity.
        /// If the first phase is InputSequence, also initializes InputSequenceState.
        /// </summary>
        private void ActivateMultiPhaseSequence(ref SystemState state, Entity target, Entity interactor)
        {
            if (!SystemAPI.HasComponent<InteractionPhaseState>(target))
                return;

            var phaseState = SystemAPI.GetComponentRW<InteractionPhaseState>(target);
            phaseState.ValueRW.IsActive = true;
            phaseState.ValueRW.CurrentPhase = 0;
            phaseState.ValueRW.PhaseTimeElapsed = 0;
            phaseState.ValueRW.TotalTimeElapsed = 0;
            phaseState.ValueRW.PhaseFailed = false;
            phaseState.ValueRW.SequenceComplete = false;
            phaseState.ValueRW.PerformingEntity = interactor;

            // If the first phase is an InputSequence, initialize InputSequenceState
            if (SystemAPI.HasComponent<InteractionPhaseConfig>(target))
            {
                ref var blob = ref SystemAPI.GetComponent<InteractionPhaseConfig>(target).PhaseBlob.Value;
                if (blob.Phases.Length > 0 && blob.Phases[0].Type == PhaseType.InputSequence &&
                    blob.Phases[0].InputSequenceIndex >= 0)
                {
                    if (SystemAPI.HasComponent<InputSequenceState>(target))
                    {
                        var seqState = SystemAPI.GetComponentRW<InputSequenceState>(target);
                        seqState.ValueRW.ActiveSequenceIndex = blob.Phases[0].InputSequenceIndex;
                        seqState.ValueRW.CurrentInputIndex = 0;
                        seqState.ValueRW.TimeSinceLastInput = 0;
                        seqState.ValueRW.SequenceComplete = false;
                        seqState.ValueRW.SequenceFailed = false;
                        seqState.ValueRW.PreviousInput = SequenceInput.None;
                    }
                }
            }
        }

        /// <summary>
        /// Deactivate multi-phase state on a target entity (on cancel).
        /// </summary>
        private void DeactivateMultiPhase(ref SystemState state, Entity target)
        {
            if (target == Entity.Null)
                return;

            if (SystemAPI.HasComponent<InteractionPhaseState>(target))
            {
                var phaseState = SystemAPI.GetComponentRW<InteractionPhaseState>(target);
                phaseState.ValueRW.IsActive = false;
                phaseState.ValueRW.CurrentPhase = -1;
                phaseState.ValueRW.PerformingEntity = Entity.Null;
            }

            if (SystemAPI.HasComponent<InputSequenceState>(target))
            {
                var seqState = SystemAPI.GetComponentRW<InputSequenceState>(target);
                seqState.ValueRW.ActiveSequenceIndex = -1;
                seqState.ValueRW.SequenceComplete = false;
                seqState.ValueRW.SequenceFailed = false;
            }
        }
    }
}
