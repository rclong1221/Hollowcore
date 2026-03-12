using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: When an entity with EncounterGroupId > 0 transitions to aggroed,
    /// instantly inject its threat leader into ALL entities with the same group ID.
    /// This is "linked pull" — attack one, aggro the entire encounter group.
    ///
    /// Unlike AggroShareSystem (distance-based, one-shot), this is group-based and instant.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggroShareSystem))]
    [BurstCompile]
    public partial struct LinkedPullSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SocialAggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Phase 1: Collect newly aggroed entities with linked pull
            var pullSources = new NativeList<PullData>(8, Allocator.Temp);

            foreach (var (social, aggroState, threatBuffer, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRO<AggroState>, DynamicBuffer<ThreatEntry>>()
                .WithEntityAccess())
            {
                if (social.ValueRO.EncounterGroupId <= 0)
                    continue;
                if ((social.ValueRO.Flags & SocialAggroFlags.LinkedPull) == 0)
                    continue;

                // Check just-aggroed: has threats and state says not yet aggroed
                // (AggroShareSystem marks IsAggroed = true, so we check if threats exist
                // and the previous-frame tracking says newly aggroed)
                if (!aggroState.ValueRO.IsAggroed || threatBuffer.Length == 0)
                    continue;

                // Only fire on the frame aggro started (TimeSinceLastValidTarget was > 0 last frame)
                if (aggroState.ValueRO.TimeSinceLastValidTarget > 0f)
                    continue;

                // Find highest threat
                float highestThreat = 0f;
                Entity highestSource = Entity.Null;
                float3 highestPos = float3.zero;

                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].ThreatValue > highestThreat)
                    {
                        highestThreat = threatBuffer[t].ThreatValue;
                        highestSource = threatBuffer[t].SourceEntity;
                        highestPos = threatBuffer[t].LastKnownPosition;
                    }
                }

                if (highestSource == Entity.Null)
                    continue;

                pullSources.Add(new PullData
                {
                    SourceEntity = entity,
                    GroupId = social.ValueRO.EncounterGroupId,
                    ThreatTarget = highestSource,
                    ThreatPosition = highestPos,
                    ThreatValue = highestThreat
                });
            }

            if (pullSources.Length == 0)
            {
                pullSources.Dispose();
                return;
            }

            // Phase 2: Distribute to all group members
            foreach (var (social, config, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRO<AggroConfig>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                int groupId = social.ValueRO.EncounterGroupId;
                if (groupId <= 0) continue;

                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);
                int maxTargets = config.ValueRO.MaxTrackedTargets;

                for (int p = 0; p < pullSources.Length; p++)
                {
                    var pull = pullSources[p];
                    if (pull.GroupId != groupId) continue;
                    if (pull.SourceEntity == entity) continue;

                    // Find or create entry for the threat target
                    int existingIndex = -1;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == pull.ThreatTarget)
                        {
                            existingIndex = t;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        var entry = threatBuffer[existingIndex];
                        entry.ThreatValue = math.max(entry.ThreatValue, pull.ThreatValue);
                        entry.LastKnownPosition = pull.ThreatPosition;
                        entry.SourceFlags |= ThreatSourceFlags.Social;
                        threatBuffer[existingIndex] = entry;
                    }
                    else if (threatBuffer.Length < maxTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = pull.ThreatTarget,
                            ThreatValue = pull.ThreatValue,
                            LastKnownPosition = pull.ThreatPosition,
                            TimeSinceVisible = 999f,
                            IsCurrentlyVisible = false,
                            SourceFlags = ThreatSourceFlags.Social
                        });
                    }
                }
            }

            pullSources.Dispose();
        }

        private struct PullData
        {
            public Entity SourceEntity;
            public int GroupId;
            public Entity ThreatTarget;
            public float3 ThreatPosition;
            public float ThreatValue;
        }
    }
}
