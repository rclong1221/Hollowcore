using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Player.Components;
using Player.Systems;

namespace DIG.Combat.Systems
{
    /// <summary>
    /// EPIC 15.15: Sets combat state for attackers when they deal damage to players.
    /// Runs after DamageApplySystem to catch player damage events before they're cleared.
    /// 
    /// This handles the case where an enemy attacks a player - the enemy should enter combat.
    /// SimpleDamageApplySystem handles the case where a player attacks an enemy.
    /// 
    /// Performance: Burst-compiled IJobEntity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DamageSystemGroup))]
    [UpdateAfter(typeof(DamageApplySystem))]
    [UpdateBefore(typeof(SimpleDamageApplySystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct CombatStateFromDamageSystem : ISystem
    {
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
            _combatStateLookup.Update(ref state);
            
            // Run single-threaded (writes to ComponentLookup require exclusive access)
            state.Dependency = new UpdateAttackerCombatStateJob
            {
                CombatStateLookup = _combatStateLookup
            }.Schedule(state.Dependency);
        }
        
        [BurstCompile]
        partial struct UpdateAttackerCombatStateJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public ComponentLookup<DIG.Combat.Components.CombatState> CombatStateLookup;
            
            void Execute(in DynamicBuffer<DamageEvent> damageBuffer, in CombatState playerCombat)
            {
                // For each damage event, put the attacker into combat
                for (int i = 0; i < damageBuffer.Length; i++)
                {
                    Entity attacker = damageBuffer[i].SourceEntity;
                    if (attacker == Entity.Null) continue;
                    
                    // Put attacker into DIG.Combat combat state
                    if (CombatStateLookup.HasComponent(attacker))
                    {
                        var attackerCombat = CombatStateLookup[attacker];
                        attackerCombat.IsInCombat = true;
                        attackerCombat.TimeSinceLastCombatAction = 0f;
                        CombatStateLookup[attacker] = attackerCombat;
                    }
                }
            }
        }
    }
}
