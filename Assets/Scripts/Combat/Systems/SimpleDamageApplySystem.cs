using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;
using Player.Systems;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 15.9: Simple damage apply system for non-player damageables.
    /// 
    /// This handles entities that have DamageableAuthoring but NOT the full player
    /// component set (ShieldComponent, CombatState, PlayerBlockingState, etc.).
    /// 
    /// Examples: Enemies (BoxingJoe), NPCs, Destructible Objects
    /// 
    /// Pipeline: DamageEvent buffer → Health reduction → Clear buffer → Enter Combat
    /// 
    /// Performance: Burst-compiled, uses IJobEntity for cache efficiency.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateAfter(typeof(DamageApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [RequireMatchingQueriesForUpdate]
    public partial struct SimpleDamageApplySystem : ISystem
    {
        private const int MaxEventsPerTick = 16;
        
        private ComponentLookup<DIG.Combat.Components.CombatState> _combatStateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            _combatStateLookup = state.GetComponentLookup<DIG.Combat.Components.CombatState>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            _combatStateLookup.Update(ref state);
            
            // Run single-threaded job (writes to ComponentLookup require exclusive access)
            state.Dependency = new ApplySimpleDamageJob
            {
                CurrentTime = currentTime,
                MaxEvents = MaxEventsPerTick,
                CombatStateLookup = _combatStateLookup
            }.Schedule(state.Dependency);
        }
        
        [BurstCompile]
        [WithNone(typeof(ShieldComponent))]
        [WithNone(typeof(PlayerBlockingState))]
        partial struct ApplySimpleDamageJob : IJobEntity
        {
            public float CurrentTime;
            public int MaxEvents;
            
            [NativeDisableParallelForRestriction]
            public ComponentLookup<DIG.Combat.Components.CombatState> CombatStateLookup;
            
            void Execute(
                Entity entity,
                ref Health health,
                ref DynamicBuffer<DamageEvent> damageBuffer,
                in DeathState deathState,
                in DamageResistance resistance)
            {
                if (damageBuffer.Length == 0)
                    return;
                    
                // Skip if dead
                if (deathState.Phase != DeathPhase.Alive)
                {
                    damageBuffer.Clear();
                    return;
                }
                
                // Skip if invulnerable
                if (deathState.IsInvulnerable(CurrentTime))
                {
                    damageBuffer.Clear();
                    return;
                }
                
                float totalDamage = 0f;
                int eventsToProcess = math.min(damageBuffer.Length, MaxEvents);
                
                // First pass: calculate damage and collect unique attackers
                // Use fixed-size buffer to avoid allocation (max 16 unique attackers per frame)
                var attackers = new NativeList<Entity>(eventsToProcess, Allocator.Temp);
                
                for (int i = 0; i < eventsToProcess; i++)
                {
                    var damage = damageBuffer[i];
                    if (math.isnan(damage.Amount) || math.isinf(damage.Amount) || damage.Amount <= 0f)
                        continue;
                    
                    float resistMult = GetResistanceMultiplier(damage.Type, resistance);
                    totalDamage += damage.Amount * resistMult;
                    
                    // Track unique attackers
                    if (damage.SourceEntity != Entity.Null)
                    {
                        bool found = false;
                        for (int j = 0; j < attackers.Length; j++)
                        {
                            if (attackers[j] == damage.SourceEntity)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            attackers.Add(damage.SourceEntity);
                        }
                    }
                }
                
                if (totalDamage > 0f)
                {
                    health.Current = math.max(0f, health.Current - totalDamage);
                    
                    // Enter combat state (target)
                    if (CombatStateLookup.HasComponent(entity))
                    {
                        var combatState = CombatStateLookup[entity];
                        combatState.IsInCombat = true;
                        combatState.TimeSinceLastCombatAction = 0f;
                        CombatStateLookup[entity] = combatState;
                    }
                    
                    // Enter combat state (attackers)
                    for (int a = 0; a < attackers.Length; a++)
                    {
                        Entity attacker = attackers[a];
                        if (CombatStateLookup.HasComponent(attacker))
                        {
                            var attackerCombat = CombatStateLookup[attacker];
                            attackerCombat.IsInCombat = true;
                            attackerCombat.TimeSinceLastCombatAction = 0f;
                            CombatStateLookup[attacker] = attackerCombat;
                        }
                    }
                }
                
                attackers.Dispose();
                damageBuffer.Clear();
            }
            
            [BurstCompile]
            private static float GetResistanceMultiplier(DamageType type, in DamageResistance resistance)
            {
                return type switch
                {
                    DamageType.Physical => resistance.PhysicalMult,
                    DamageType.Heat => resistance.HeatMult,
                    DamageType.Radiation => resistance.RadiationMult,
                    DamageType.Suffocation => resistance.SuffocationMult,
                    DamageType.Explosion => resistance.ExplosionMult,
                    DamageType.Toxic => resistance.ToxicMult,
                    _ => 1f
                };
            }
        }
    }
}
