using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using DIG.AI.Components;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: Ticks all ability cooldowns each frame.
    /// Handles per-ability, global, and group cooldowns, plus charge regeneration.
    /// Also ticks legacy AIState.AttackCooldownRemaining for backward compatibility.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AIStateTransitionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AbilityCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (cooldownsBuf, abilitiesBuf) in
                SystemAPI.Query<
                    DynamicBuffer<AbilityCooldownState>,
                    DynamicBuffer<AbilityDefinition>>())
            {
                var cooldowns = cooldownsBuf;
                var abilities = abilitiesBuf;

                for (int i = 0; i < cooldowns.Length && i < abilities.Length; i++)
                {
                    var cd = cooldowns[i];
                    var ability = abilities[i];

                    // Tick cooldowns
                    cd.CooldownRemaining = math.max(0f, cd.CooldownRemaining - deltaTime);
                    cd.GlobalCooldownRemaining = math.max(0f, cd.GlobalCooldownRemaining - deltaTime);
                    cd.CooldownGroupRemaining = math.max(0f, cd.CooldownGroupRemaining - deltaTime);

                    // Charge regeneration
                    if (ability.MaxCharges > 0 && cd.ChargesRemaining < cd.MaxCharges)
                    {
                        cd.ChargeRegenTimer -= deltaTime;
                        if (cd.ChargeRegenTimer <= 0f)
                        {
                            cd.ChargesRemaining = math.min(cd.ChargesRemaining + 1, cd.MaxCharges);
                            if (cd.ChargesRemaining < cd.MaxCharges)
                                cd.ChargeRegenTimer = ability.ChargeRegenTime;
                            else
                                cd.ChargeRegenTimer = 0f;
                        }
                    }

                    cooldowns[i] = cd;
                }
            }
        }
    }
}
