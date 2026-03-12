using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Player.Abilities;
using Player.Components; // PlayerInput

namespace DIG.Player.Systems.Abilities
{
    /// <summary>
    /// Sprint system.
    /// 13.15.4: Optionally blocks sprinting while crouched.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AbilitySystemGroup))]
    [UpdateAfter(typeof(CrouchSystem))] // Must run after crouch to read IsCrouching
    public partial struct SprintSystem : ISystem
    {
        private const int SPRINT_MODIFIER_ID = 100;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (sprintAbility, sprintSettings, input, modifiers, entity) in 
                     SystemAPI.Query<RefRW<SprintAbility>, RefRO<SprintSettings>, RefRO<PlayerInput>, DynamicBuffer<SpeedModifier>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                var buffer = modifiers;
                bool wantsToSprint = input.ValueRO.Sprint.IsSet; // Assuming Hold to Sprint for now
                sprintAbility.ValueRW.SprintPressed = wantsToSprint;

                // ========================================================
                // 13.15.4: Block Sprint While Crouched
                // ========================================================
                if (wantsToSprint)
                {
                    // Check if crouching and sprint is blocked
                    if (SystemAPI.HasComponent<CrouchAbility>(entity) && SystemAPI.HasComponent<CrouchSettings>(entity))
                    {
                        var crouchAbility = SystemAPI.GetComponent<CrouchAbility>(entity);
                        var crouchSettings = SystemAPI.GetComponent<CrouchSettings>(entity);
                        
                        if (crouchAbility.IsCrouching && !crouchSettings.AllowSpeedChange)
                        {
                            // Block sprint while crouched
                            wantsToSprint = false;
                        }
                    }
                }

                sprintAbility.ValueRW.IsSprinting = wantsToSprint;

                // Handle Speed Modifier
                int foundIndex = -1;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].SourceId == SPRINT_MODIFIER_ID)
                    {
                        foundIndex = i;
                        break;
                    }
                }

                if (wantsToSprint)
                {
                    // Add or Update
                    var mod = new SpeedModifier
                    {
                        SourceId = SPRINT_MODIFIER_ID,
                        Multiplier = sprintSettings.ValueRO.SpeedMultiplier,
                        Duration = -1f, // Permanent while active
                        ElapsedTime = 0f
                    };

                    if (foundIndex >= 0)
                    {
                        buffer[foundIndex] = mod;
                    }
                    else
                    {
                        buffer.Add(mod);
                    }
                }
                else
                {
                    // Remove if exists
                    if (foundIndex >= 0)
                    {
                        buffer.RemoveAt(foundIndex);
                    }
                }
            }
        }
    }
}

