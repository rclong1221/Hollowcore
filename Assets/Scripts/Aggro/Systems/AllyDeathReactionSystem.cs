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
    /// EPIC 15.33: Reacts to ally deaths within encounter groups.
    /// Supports avenge (bonus threat on killer), enrage (multiply all threat),
    /// flee (transition AI state), and pack alpha death reactions.
    ///
    /// Detects death by checking DeathState.Phase != Alive on entities
    /// that have SocialAggroConfig.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CallForHelpSystem))]
    [BurstCompile]
    public partial struct AllyDeathReactionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SocialAggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            // Collect recently dead entities with social config
            var deadEntities = new NativeList<DeadAllyData>(8, Allocator.Temp);

            foreach (var (social, deathState, threatBuffer, transform, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRO<DeathState>,
                    DynamicBuffer<ThreatEntry>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                if (deathState.ValueRO.Phase == DeathPhase.Alive)
                    continue;

                // Find killer (highest damage threat)
                Entity killer = Entity.Null;
                float highestDmgThreat = 0f;
                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].DamageThreat > highestDmgThreat)
                    {
                        highestDmgThreat = threatBuffer[t].DamageThreat;
                        killer = threatBuffer[t].SourceEntity;
                    }
                }

                deadEntities.Add(new DeadAllyData
                {
                    DeadEntity = entity,
                    GroupId = social.ValueRO.EncounterGroupId,
                    Position = transform.ValueRO.Position,
                    Killer = killer,
                    Role = social.ValueRO.Role
                });
            }

            if (deadEntities.Length == 0)
            {
                deadEntities.Dispose();
                return;
            }

            // React in surviving allies
            foreach (var (social, socialState, config, aggroState, transform, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRW<SocialAggroState>,
                    RefRO<AggroConfig>, RefRO<AggroState>, RefRO<LocalTransform>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);
                float3 myPos = transform.ValueRO.Position;
                float shareRadius = config.ValueRO.AggroShareRadius;
                int maxTargets = config.ValueRO.MaxTrackedTargets;
                var flags = social.ValueRO.Flags;

                // Tick rage timer
                if (socialState.ValueRO.RageTimer > 0f)
                {
                    socialState.ValueRW.RageTimer -= dt;
                }

                for (int d = 0; d < deadEntities.Length; d++)
                {
                    var dead = deadEntities[d];
                    if (dead.DeadEntity == entity) continue;

                    // Skip if already processed this ally's death
                    if (dead.DeadEntity == socialState.ValueRO.LastDeadAlly)
                        continue;

                    float distance = math.distance(myPos, dead.Position);
                    if (distance > shareRadius) continue;

                    // Avenge: add bonus threat on killer
                    if ((flags & SocialAggroFlags.AllyDeathAvenge) != 0 && dead.Killer != Entity.Null)
                    {
                        float bonus = social.ValueRO.AllyDeathThreatBonus;
                        AddOrUpdateThreat(ref threatBuffer, dead.Killer, bonus,
                            dead.Position, maxTargets, ThreatSourceFlags.Social);
                    }

                    // Enrage: multiply all threat
                    if ((flags & SocialAggroFlags.AllyDeathEnrage) != 0)
                    {
                        float mult = social.ValueRO.AllyDeathRageMultiplier;
                        if (mult > 1f)
                        {
                            for (int t = 0; t < threatBuffer.Length; t++)
                            {
                                var entry = threatBuffer[t];
                                entry.ThreatValue *= mult;
                                entry.SourceFlags |= ThreatSourceFlags.Social;
                                threatBuffer[t] = entry;
                            }
                            socialState.ValueRW.RageTimer = 10f; // 10s rage duration
                        }
                    }

                    // Pack behavior: alpha death special reaction
                    if ((flags & SocialAggroFlags.PackBehavior) != 0 && dead.Role == PackRole.Alpha)
                    {
                        // Alpha died — enrage members (double threat)
                        for (int t = 0; t < threatBuffer.Length; t++)
                        {
                            var entry = threatBuffer[t];
                            entry.ThreatValue *= 2f;
                            entry.SourceFlags |= ThreatSourceFlags.Social;
                            threatBuffer[t] = entry;
                        }
                        socialState.ValueRW.RageTimer = 15f;
                    }

                    socialState.ValueRW.AllyDeathCount++;
                    socialState.ValueRW.LastDeadAlly = dead.DeadEntity;
                }
            }

            deadEntities.Dispose();
        }

        private static void AddOrUpdateThreat(ref DynamicBuffer<ThreatEntry> buffer,
            Entity source, float threat, float3 position, int maxTargets, ThreatSourceFlags flags)
        {
            for (int t = 0; t < buffer.Length; t++)
            {
                if (buffer[t].SourceEntity == source)
                {
                    var entry = buffer[t];
                    entry.ThreatValue += threat;
                    entry.LastKnownPosition = position;
                    entry.SourceFlags |= flags;
                    buffer[t] = entry;
                    return;
                }
            }

            if (buffer.Length < maxTargets)
            {
                buffer.Add(new ThreatEntry
                {
                    SourceEntity = source,
                    ThreatValue = threat,
                    LastKnownPosition = position,
                    TimeSinceVisible = 999f,
                    IsCurrentlyVisible = false,
                    SourceFlags = flags
                });
            }
        }

        private struct DeadAllyData
        {
            public Entity DeadEntity;
            public int GroupId;
            public float3 Position;
            public Entity Killer;
            public PackRole Role;
        }
    }
}
