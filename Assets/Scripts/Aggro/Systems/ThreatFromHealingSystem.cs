using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;
using Player.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Generates threat on enemies when a healer heals an entity
    /// that is in combat with those enemies. Classic MMO "healing aggro" mechanic.
    ///
    /// For each HealEvent with a valid SourceEntity: find all enemies whose ThreatEntry
    /// buffer contains the healed entity, then add threat for the healer.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ThreatFromDamageSystem))]
    [BurstCompile]
    public partial struct ThreatFromHealingSystem : ISystem
    {
        ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HealingThreatConfig>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);

            // Collect heal events this frame
            var heals = new NativeList<HealData>(16, Allocator.Temp);

            foreach (var (healBuffer, healConfig, entity) in
                SystemAPI.Query<DynamicBuffer<HealEvent>, RefRO<HealingThreatConfig>>()
                .WithEntityAccess())
            {
                float threatPerHP = healConfig.ValueRO.ThreatPerHealPoint;

                for (int i = 0; i < healBuffer.Length; i++)
                {
                    var heal = healBuffer[i];
                    if (heal.SourceEntity == Entity.Null || heal.Amount <= 0f)
                        continue;

                    heals.Add(new HealData
                    {
                        HealedEntity = entity,
                        HealerEntity = heal.SourceEntity,
                        HealAmount = heal.Amount,
                        ThreatPerHP = threatPerHP
                    });
                }
            }

            if (heals.Length == 0)
            {
                heals.Dispose();
                return;
            }

            // For each enemy with ThreatEntry, check if any healed entity is in their threat table
            foreach (var (config, entity) in
                SystemAPI.Query<RefRO<AggroConfig>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);
                int maxTargets = config.ValueRO.MaxTrackedTargets;

                for (int h = 0; h < heals.Length; h++)
                {
                    var heal = heals[h];

                    // Check if this enemy is fighting the healed entity
                    bool fightingHealedEntity = false;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == heal.HealedEntity)
                        {
                            fightingHealedEntity = true;
                            break;
                        }
                    }

                    if (!fightingHealedEntity)
                        continue;

                    float healThreat = heal.HealAmount * heal.ThreatPerHP;

                    // Find or create entry for healer
                    int healerIndex = -1;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == heal.HealerEntity)
                        {
                            healerIndex = t;
                            break;
                        }
                    }

                    float3 healerPos = float3.zero;
                    if (_transformLookup.HasComponent(heal.HealerEntity))
                    {
                        healerPos = _transformLookup[heal.HealerEntity].Position;
                    }

                    if (healerIndex >= 0)
                    {
                        var entry = threatBuffer[healerIndex];
                        entry.ThreatValue += healThreat;
                        entry.SourceFlags |= ThreatSourceFlags.Healing;
                        entry.LastKnownPosition = healerPos;
                        threatBuffer[healerIndex] = entry;
                    }
                    else if (threatBuffer.Length < maxTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = heal.HealerEntity,
                            ThreatValue = healThreat,
                            LastKnownPosition = healerPos,
                            TimeSinceVisible = 999f,
                            IsCurrentlyVisible = false,
                            SourceFlags = ThreatSourceFlags.Healing
                        });
                    }
                }
            }

            heals.Dispose();
        }

        private struct HealData
        {
            public Entity HealedEntity;
            public Entity HealerEntity;
            public float HealAmount;
            public float ThreatPerHP;
        }
    }
}
