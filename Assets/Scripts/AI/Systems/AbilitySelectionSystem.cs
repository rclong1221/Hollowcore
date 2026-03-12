using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Transforms;
using DIG.AI.Components;
using DIG.Combat.Systems;
using DIG.Combat.Resources;
using Health = Player.Components.Health;

namespace DIG.AI.Systems
{
    /// <summary>
    /// EPIC 15.32: AI ability selection system.
    /// Picks the next ability when AbilityExecutionState.Phase == Idle.
    /// Supports Priority mode (first valid wins) and Utility mode (weighted scoring).
    /// Respects per-ability, global, and group cooldowns, charges, phase, range, HP conditions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AIStateTransitionSystem))]
    [UpdateBefore(typeof(AbilityExecutionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct AbilitySelectionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourcePool> _resourcePoolLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AIBrain>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourcePoolLookup = state.GetComponentLookup<ResourcePool>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _resourcePoolLookup.Update(ref state);

            foreach (var (execState, aiState, brain, transform, health, entity) in
                SystemAPI.Query<
                    RefRW<AbilityExecutionState>,
                    RefRO<AIState>,
                    RefRO<AIBrain>,
                    RefRO<LocalTransform>,
                    RefRO<Health>>()
                .WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0f) continue;

                // Only select when idle and in combat
                if (execState.ValueRO.Phase != AbilityCastPhase.Idle) continue;
                if (aiState.ValueRO.CurrentState != AIBehaviorState.Combat) continue;

                // Must have ability buffer
                if (!SystemAPI.HasBuffer<AbilityDefinition>(entity)) continue;
                if (!SystemAPI.HasBuffer<AbilityCooldownState>(entity)) continue;

                var abilities = SystemAPI.GetBuffer<AbilityDefinition>(entity);
                var cooldowns = SystemAPI.GetBuffer<AbilityCooldownState>(entity);
                if (abilities.Length == 0) continue;

                // Get target info
                Entity targetEntity = Entity.Null;
                float distanceToTarget = float.MaxValue;
                if (SystemAPI.HasComponent<DIG.Targeting.TargetData>(entity))
                {
                    var targetData = SystemAPI.GetComponent<DIG.Targeting.TargetData>(entity);
                    targetEntity = targetData.TargetEntity;
                    if (targetEntity != Entity.Null && _transformLookup.HasComponent(targetEntity))
                    {
                        float3 selfPos = transform.ValueRO.Position;
                        float3 targetPos = _transformLookup[targetEntity].Position;
                        float3 diff = targetPos - selfPos;
                        diff.y = 0f;
                        distanceToTarget = math.length(diff);
                    }
                }

                if (targetEntity == Entity.Null) continue;

                // Get current encounter phase (default 0)
                byte currentPhase = 0;
                bool isTransitioning = false;
                if (SystemAPI.HasComponent<EncounterState>(entity))
                {
                    var encounter = SystemAPI.GetComponent<EncounterState>(entity);
                    currentPhase = encounter.CurrentPhase;
                    isTransitioning = encounter.IsTransitioning;
                }

                // Skip selection during phase transition (invulnerability)
                if (isTransitioning) continue;

                // Get current HP%
                float hpPercent = 1f;
                if (health.ValueRO.Max > 0)
                    hpPercent = health.ValueRO.Current / health.ValueRO.Max;

                // Priority mode: iterate in order, select first valid
                int selectedIndex = -1;

                for (int i = 0; i < abilities.Length && i < cooldowns.Length; i++)
                {
                    var ability = abilities[i];
                    var cooldown = cooldowns[i];

                    // Cooldown checks
                    if (cooldown.CooldownRemaining > 0f) continue;
                    if (cooldown.GlobalCooldownRemaining > 0f) continue;
                    if (cooldown.CooldownGroupRemaining > 0f) continue;

                    // Charge check
                    if (ability.MaxCharges > 0 && cooldown.ChargesRemaining <= 0) continue;

                    // Range check
                    if (distanceToTarget > ability.Range) continue;

                    // Phase check
                    if (currentPhase < ability.PhaseMin || currentPhase > ability.PhaseMax) continue;

                    // HP threshold check
                    if (hpPercent < ability.HPThresholdMin || hpPercent > ability.HPThresholdMax) continue;

                    // Resource check (EPIC 16.8)
                    if (ability.ResourceCostType != ResourceType.None)
                    {
                        if (!_resourcePoolLookup.HasComponent(entity)) continue;
                        var pool = _resourcePoolLookup[entity];
                        if (!pool.HasResource(ability.ResourceCostType, ability.ResourceCostAmount)) continue;
                    }

                    // All conditions met
                    selectedIndex = i;
                    break;
                }

                if (selectedIndex >= 0)
                {
                    var selectedAbility = abilities[selectedIndex];

                    ref var exec = ref execState.ValueRW;
                    exec.SelectedAbilityIndex = selectedIndex;
                    exec.TargetEntity = targetEntity;
                    exec.DamageDealt = false;
                    exec.TicksDelivered = 0;
                    exec.PhaseTimer = 0f;

                    if (_transformLookup.HasComponent(targetEntity))
                    {
                        float3 selfPos = transform.ValueRO.Position;
                        float3 targetPos = _transformLookup[targetEntity].Position;
                        float3 dir = targetPos - selfPos;
                        dir.y = 0f;
                        float len = math.length(dir);
                        exec.CastDirection = len > 0.01f ? dir / len : new float3(0, 0, 1);
                        exec.TargetPosition = targetPos;
                    }

                    // Start at Telegraph if duration > 0, otherwise Casting
                    exec.Phase = selectedAbility.TelegraphDuration > 0f
                        ? AbilityCastPhase.Telegraph
                        : AbilityCastPhase.Casting;

                    // Enable movement override if locked
                    if (selectedAbility.MovementDuringCast == AbilityMovement.Locked &&
                        SystemAPI.HasComponent<MovementOverride>(entity))
                    {
                        SystemAPI.SetComponentEnabled<MovementOverride>(entity, true);
                    }
                }
            }
        }
    }
}
