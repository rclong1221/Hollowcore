using Unity.Burst;
using Unity.Entities;
using DIG.Player.Abilities;
using DIG.Interaction;

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// EPIC 13.17.5: Blocks abilities during interaction based on InteractAbility settings.
    ///
    /// This system runs before AbilityPrioritySystem to set CanStart = false on abilities
    /// that should be blocked during an active interaction.
    ///
    /// Blocking is determined by:
    /// - AllowHeightChange: If false, blocks crouch/prone abilities
    /// - AllowAim: If false, blocks aim abilities
    /// - IsConcurrent: If false, blocks locomotion/movement abilities
    /// - BlockedAbilitiesMask: Custom bitmask for specific ability blocking
    ///
    /// Burst-compiled for performance.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateBefore(typeof(AbilityPrioritySystem))]
    public partial struct InteractionAbilityBlockingSystem : ISystem
    {
        // Known ability type IDs (should match your ability definitions)
        // These are bitmask positions, not the actual IDs
        private const int AbilityBit_Crouch = 0;
        private const int AbilityBit_Prone = 1;
        private const int AbilityBit_Aim = 2;
        private const int AbilityBit_Sprint = 3;
        private const int AbilityBit_Jump = 4;
        private const int AbilityBit_Roll = 5;
        private const int AbilityBit_Climb = 6;
        private const int AbilityBit_Swim = 7;

        // Pre-computed masks for common blocking scenarios
        private const int HeightChangeMask = (1 << AbilityBit_Crouch) | (1 << AbilityBit_Prone);
        private const int AimMask = (1 << AbilityBit_Aim);
        private const int LocomotionMask = (1 << AbilityBit_Sprint) | (1 << AbilityBit_Jump) |
                                           (1 << AbilityBit_Roll) | (1 << AbilityBit_Climb);

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AbilityState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new BlockAbilitiesDuringInteractionJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct BlockAbilitiesDuringInteractionJob : IJobEntity
        {
            public void Execute(
                in InteractAbility interactAbility,
                ref DynamicBuffer<AbilityDefinition> abilities)
            {
                // Only process if currently interacting
                if (!interactAbility.IsInteracting)
                    return;

                // Build the complete block mask from interaction settings
                int blockMask = interactAbility.BlockedAbilitiesMask;

                // Add height change abilities if not allowed
                if (!interactAbility.AllowHeightChange)
                {
                    blockMask |= HeightChangeMask;
                }

                // Add aim abilities if not allowed
                if (!interactAbility.AllowAim)
                {
                    blockMask |= AimMask;
                }

                // Add locomotion abilities if not concurrent
                if (!interactAbility.IsConcurrent)
                {
                    blockMask |= LocomotionMask;
                }

                // If nothing to block, early exit
                if (blockMask == 0)
                    return;

                // Apply blocking to matching abilities
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[i];

                    // Check if this ability type should be blocked
                    // AbilityTypeId maps to bit position for this check
                    int abilityBit = 1 << ability.AbilityTypeId;

                    if ((blockMask & abilityBit) != 0)
                    {
                        // Block this ability from starting
                        ability.CanStart = false;
                        abilities[i] = ability;
                    }
                }
            }
        }
    }

    /// <summary>
    /// EPIC 13.17.5: Extended blocking system that also handles blocking by ability priority.
    /// This system can force-stop lower priority abilities when interaction starts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateAfter(typeof(InteractionAbilityBlockingSystem))]
    [UpdateBefore(typeof(AbilityPrioritySystem))]
    public partial struct InteractionAbilityStopSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AbilityState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new StopBlockedAbilitiesJob().ScheduleParallel();
        }

        /// <summary>
        /// Force-stops abilities that are blocked by an active interaction.
        /// Only stops abilities that are currently active and match the block mask.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(AbilitySystemTag))]
        public partial struct StopBlockedAbilitiesJob : IJobEntity
        {
            public void Execute(
                in InteractAbility interactAbility,
                ref AbilityState abilityState,
                ref DynamicBuffer<AbilityDefinition> abilities)
            {
                // Only process if currently interacting
                if (!interactAbility.IsInteracting)
                    return;

                // No active ability to stop
                if (abilityState.ActiveAbilityIndex < 0 ||
                    abilityState.ActiveAbilityIndex >= abilities.Length)
                    return;

                // Build the complete block mask
                int blockMask = interactAbility.BlockedAbilitiesMask;

                if (!interactAbility.AllowHeightChange)
                {
                    blockMask |= (1 << 0) | (1 << 1); // Crouch, Prone
                }

                if (!interactAbility.AllowAim)
                {
                    blockMask |= (1 << 2); // Aim
                }

                if (!interactAbility.IsConcurrent)
                {
                    blockMask |= (1 << 3) | (1 << 4) | (1 << 5) | (1 << 6); // Sprint, Jump, Roll, Climb
                }

                // Check if active ability should be stopped
                var activeAbility = abilities[abilityState.ActiveAbilityIndex];
                int activeBit = 1 << activeAbility.AbilityTypeId;

                if ((blockMask & activeBit) != 0)
                {
                    // Force the ability to stop
                    activeAbility.CanStop = true;
                    abilities[abilityState.ActiveAbilityIndex] = activeAbility;
                }
            }
        }
    }
}
