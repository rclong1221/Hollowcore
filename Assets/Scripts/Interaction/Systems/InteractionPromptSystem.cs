using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DIG.Interaction.Systems
{
    /// <summary>
    /// Updates interaction prompt state for UI display.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InteractableDetectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class InteractionPromptSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Find local player's interaction state
            foreach (var (ability, prompt, entity) in 
                     SystemAPI.Query<RefRO<InteractAbility>, RefRW<InteractionPrompt>>()
                     .WithAll<CanInteract>()
                     .WithEntityAccess())
            {
                ref var promptRef = ref prompt.ValueRW;
                
                // Check if there's a valid target
                if (ability.ValueRO.TargetEntity != Entity.Null && 
                    !ability.ValueRO.IsInteracting &&
                    SystemAPI.HasComponent<Interactable>(ability.ValueRO.TargetEntity))
                {
                    var interactable = SystemAPI.GetComponent<Interactable>(ability.ValueRO.TargetEntity);

                    promptRef.IsVisible = true;
                    promptRef.InteractableEntity = ability.ValueRO.TargetEntity;
                    promptRef.HoldProgress = 0f;

                    // EPIC 16.1 + 15.23: Use InteractableContext for rich prompts if available
                    if (SystemAPI.HasComponent<InteractableContext>(ability.ValueRO.TargetEntity))
                    {
                        var context = SystemAPI.GetComponent<InteractableContext>(ability.ValueRO.TargetEntity);
                        if (context.ActionNameKey.Length > 0)
                        {
                            // Explicit localization key takes highest priority
                            promptRef.Message = context.ActionNameKey;
                        }
                        else if (context.Verb != InteractionVerb.Interact || interactable.Message.Length == 0)
                        {
                            // EPIC 15.23: Derive display name from verb (localization hook)
                            promptRef.Message = InteractionVerbUtility.GetVerbDisplayName(context.Verb);
                        }
                        else
                        {
                            // Fall back to raw message
                            promptRef.Message = interactable.Message;
                        }
                    }
                    else
                    {
                        promptRef.Message = interactable.Message;
                    }

                    // Get world position for screen projection
                    if (SystemAPI.HasComponent<LocalTransform>(ability.ValueRO.TargetEntity))
                    {
                        var targetTransform = SystemAPI.GetComponent<LocalTransform>(ability.ValueRO.TargetEntity);
                        // Screen position will be calculated by UI MonoBehaviour
                    }
                }
                else if (ability.ValueRO.IsInteracting && ability.ValueRO.TargetEntity != Entity.Null)
                {
                    // Show progress during interaction
                    promptRef.IsVisible = true;
                    promptRef.HoldProgress = ability.ValueRO.InteractionProgress;

                    // EPIC 16.1 Phase 3: Show phase-specific prompt for multi-phase interactions
                    Entity target = ability.ValueRO.TargetEntity;
                    if (SystemAPI.HasComponent<InteractionPhaseState>(target) &&
                        SystemAPI.HasComponent<InteractionPhaseConfig>(target))
                    {
                        var phaseState = SystemAPI.GetComponent<InteractionPhaseState>(target);
                        if (phaseState.IsActive && phaseState.CurrentPhase >= 0)
                        {
                            ref var blob = ref SystemAPI.GetComponent<InteractionPhaseConfig>(target).PhaseBlob.Value;
                            if (phaseState.CurrentPhase < blob.Phases.Length)
                            {
                                ref var phaseDef = ref blob.Phases[phaseState.CurrentPhase];
                                if (phaseDef.PromptKey.Length > 0)
                                {
                                    promptRef.Message = phaseDef.PromptKey;
                                }
                            }
                        }
                    }
                }
                else
                {
                    promptRef.IsVisible = false;
                    promptRef.InteractableEntity = Entity.Null;
                    promptRef.HoldProgress = 0f;
                }
            }
        }
    }
}
