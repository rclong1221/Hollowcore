using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using DIG.AI.Components;
using DIG.Combat.Systems;
using Health = Player.Components.Health;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: Boss phase transition system.
    /// Checks HP thresholds and trigger-based PendingPhase to advance encounter phases.
    /// Handles invulnerability windows, phase modifiers, and transition abilities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EncounterTriggerSystem))]
    [UpdateBefore(typeof(AbilitySelectionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct PhaseTransitionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EncounterState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (encounterState, phases, entity) in
                SystemAPI.Query<
                    RefRW<EncounterState>,
                    DynamicBuffer<PhaseDefinition>>()
                .WithEntityAccess())
            {
                ref var encounter = ref encounterState.ValueRW;

                // Handle ongoing transition (invulnerability window)
                if (encounter.IsTransitioning)
                {
                    encounter.TransitionTimer += deltaTime;
                    if (encounter.TransitionTimer >= encounter.TransitionDuration)
                    {
                        encounter.IsTransitioning = false;
                    }
                    continue; // Skip phase checks during transition
                }

                // Get current HP%
                float hpPercent = 1f;
                if (SystemAPI.HasComponent<Health>(entity))
                {
                    var health = SystemAPI.GetComponent<Health>(entity);
                    if (health.Max > 0)
                        hpPercent = health.Current / health.Max;
                }

                // Determine target phase from HP thresholds
                byte hpBasedPhase = 0;
                for (int i = 0; i < phases.Length; i++)
                {
                    var phase = phases[i];
                    if (phase.HPThresholdEntry < 0f) continue; // Trigger-only phase
                    if (hpPercent <= phase.HPThresholdEntry && phase.PhaseIndex > hpBasedPhase)
                    {
                        hpBasedPhase = phase.PhaseIndex;
                    }
                }

                // Combine HP-based and trigger-based (PendingPhase)
                byte targetPhase = (byte)math.max((int)hpBasedPhase, (int)encounter.PendingPhase);

                // Advance to target phase if higher than current
                if (targetPhase > encounter.CurrentPhase)
                {
                    // Find the phase definition for the target phase
                    for (int i = 0; i < phases.Length; i++)
                    {
                        if (phases[i].PhaseIndex != targetPhase) continue;

                        var phaseDef = phases[i];

                        // Start invulnerability window if configured
                        if (phaseDef.InvulnerableDuration > 0f)
                        {
                            encounter.IsTransitioning = true;
                            encounter.TransitionTimer = 0f;
                            encounter.TransitionDuration = phaseDef.InvulnerableDuration;
                        }

                        // Update phase
                        encounter.CurrentPhase = targetPhase;
                        encounter.PendingPhase = targetPhase;
                        encounter.PhaseTimer = 0f;
                        encounter.AbilityCastCount0 = 0;
                        encounter.AbilityCastCount1 = 0;

                        break;
                    }
                }

                // Tick enrage timer
                if (encounter.EnrageTimer > 0f && !encounter.IsEnraged)
                {
                    encounter.EnrageTimer -= deltaTime;
                    if (encounter.EnrageTimer <= 0f)
                    {
                        encounter.IsEnraged = true;
                        encounter.EnrageTimer = 0f;
                    }
                }
            }
        }
    }
}
