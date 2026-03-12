using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using DIG.Combat.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Defender aggro for towers, guardian NPCs, etc.
    /// Entities with DefenderAggro flag monitor nearby allied entities for damage.
    /// When an ally is attacked, the attacker gets a massive threat boost on the defender.
    ///
    /// Priority: entity attacking an ally > proximity > highest threat.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AllyDeathReactionSystem))]
    [BurstCompile]
    public partial struct DefenderAggroSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SocialAggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Collect entities in combat (recently damaged allies to defend)
            var combatEntities = new NativeList<CombatAllyData>(16, Allocator.Temp);

            foreach (var (combatState, threatBuffer, transform, entity) in
                SystemAPI.Query<RefRO<CombatState>, DynamicBuffer<ThreatEntry>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (!combatState.ValueRO.IsInCombat)
                    continue;
                if (threatBuffer.Length == 0)
                    continue;

                // Find highest damage-threat attacker
                Entity topAttacker = Entity.Null;
                float topDmg = 0f;
                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].DamageThreat > topDmg)
                    {
                        topDmg = threatBuffer[t].DamageThreat;
                        topAttacker = threatBuffer[t].SourceEntity;
                    }
                }

                if (topAttacker != Entity.Null)
                {
                    combatEntities.Add(new CombatAllyData
                    {
                        AllyEntity = entity,
                        Position = transform.ValueRO.Position,
                        Attacker = topAttacker
                    });
                }
            }

            if (combatEntities.Length == 0)
            {
                combatEntities.Dispose();
                return;
            }

            // Defenders: boost threat for attackers of nearby allies
            foreach (var (social, config, transform, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRO<AggroConfig>, RefRO<LocalTransform>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                if ((social.ValueRO.Flags & SocialAggroFlags.DefenderAggro) == 0)
                    continue;

                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);
                float3 myPos = transform.ValueRO.Position;
                float defendRadius = config.ValueRO.AggroShareRadius;
                int maxTargets = config.ValueRO.MaxTrackedTargets;

                for (int c = 0; c < combatEntities.Length; c++)
                {
                    var ally = combatEntities[c];
                    if (ally.AllyEntity == entity) continue;

                    float distance = math.distance(myPos, ally.Position);
                    if (distance > defendRadius) continue;

                    // Massive threat boost for entity attacking our ally
                    float defenderBonus = 100f;

                    int existingIndex = -1;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == ally.Attacker)
                        {
                            existingIndex = t;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        var entry = threatBuffer[existingIndex];
                        entry.ThreatValue += defenderBonus;
                        entry.SourceFlags |= ThreatSourceFlags.Social;
                        threatBuffer[existingIndex] = entry;
                    }
                    else if (threatBuffer.Length < maxTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = ally.Attacker,
                            ThreatValue = defenderBonus,
                            LastKnownPosition = ally.Position,
                            TimeSinceVisible = 999f,
                            IsCurrentlyVisible = false,
                            SourceFlags = ThreatSourceFlags.Social
                        });
                    }
                }
            }

            combatEntities.Dispose();
        }

        private struct CombatAllyData
        {
            public Entity AllyEntity;
            public float3 Position;
            public Entity Attacker;
        }
    }
}
