using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.33: Ongoing call-for-help when aggroed enemies take damage.
    /// Unlike AggroShareSystem (one-shot on initial aggro), this fires repeatedly
    /// on cooldown while the entity is in combat and has the CallForHelp flag.
    ///
    /// Nearby entities with RespondToHelp flag gain shared threat.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LinkedPullSystem))]
    [BurstCompile]
    public partial struct CallForHelpSystem : ISystem
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

            // Phase 1: Collect call-for-help emissions
            var helpCalls = new NativeList<HelpCallData>(8, Allocator.Temp);

            foreach (var (social, socialState, aggroState, threatBuffer, transform, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRW<SocialAggroState>,
                    RefRO<AggroState>, DynamicBuffer<ThreatEntry>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Tick cooldown
                if (socialState.ValueRO.CallForHelpTimer > 0f)
                {
                    socialState.ValueRW.CallForHelpTimer -= dt;
                }

                if ((social.ValueRO.Flags & SocialAggroFlags.CallForHelp) == 0)
                    continue;
                if (!aggroState.ValueRO.IsAggroed)
                    continue;
                if (socialState.ValueRO.CallForHelpTimer > 0f)
                    continue;
                if (social.ValueRO.CallForHelpRadius <= 0f)
                    continue;

                // Check if has damage-flagged threat (recently took damage)
                bool hasDamageThreat = false;
                Entity threatLeader = Entity.Null;
                float leaderThreat = 0f;
                float3 leaderPos = float3.zero;

                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    var entry = threatBuffer[t];
                    if ((entry.SourceFlags & ThreatSourceFlags.Damage) != 0)
                        hasDamageThreat = true;
                    if (entry.ThreatValue > leaderThreat)
                    {
                        leaderThreat = entry.ThreatValue;
                        threatLeader = entry.SourceEntity;
                        leaderPos = entry.LastKnownPosition;
                    }
                }

                if (!hasDamageThreat || threatLeader == Entity.Null)
                    continue;

                // Emit call
                socialState.ValueRW.CallForHelpTimer = social.ValueRO.CallForHelpCooldown;
                float sharedThreat = leaderThreat * social.ValueRO.CallForHelpThreatShare;

                helpCalls.Add(new HelpCallData
                {
                    CallerEntity = entity,
                    CallerPosition = transform.ValueRO.Position,
                    ThreatTarget = threatLeader,
                    ThreatPosition = leaderPos,
                    ThreatValue = sharedThreat,
                    Radius = social.ValueRO.CallForHelpRadius
                });
            }

            if (helpCalls.Length == 0)
            {
                helpCalls.Dispose();
                return;
            }

            // Phase 2: Distribute to nearby responders
            foreach (var (social, config, transform, entity) in
                SystemAPI.Query<RefRO<SocialAggroConfig>, RefRO<AggroConfig>, RefRO<LocalTransform>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                if ((social.ValueRO.Flags & SocialAggroFlags.RespondToHelp) == 0)
                    continue;

                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);
                float3 myPos = transform.ValueRO.Position;
                int maxTargets = config.ValueRO.MaxTrackedTargets;

                for (int h = 0; h < helpCalls.Length; h++)
                {
                    var call = helpCalls[h];
                    if (call.CallerEntity == entity) continue;

                    float distance = math.distance(myPos, call.CallerPosition);
                    if (distance > call.Radius) continue;

                    // Find or update threat entry
                    int existingIndex = -1;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == call.ThreatTarget)
                        {
                            existingIndex = t;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        var entry = threatBuffer[existingIndex];
                        entry.ThreatValue += call.ThreatValue * 0.25f;
                        entry.LastKnownPosition = call.ThreatPosition;
                        entry.SourceFlags |= ThreatSourceFlags.Social;
                        threatBuffer[existingIndex] = entry;
                    }
                    else if (threatBuffer.Length < maxTargets)
                    {
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = call.ThreatTarget,
                            ThreatValue = call.ThreatValue,
                            LastKnownPosition = call.ThreatPosition,
                            TimeSinceVisible = 999f,
                            IsCurrentlyVisible = false,
                            SourceFlags = ThreatSourceFlags.Social
                        });
                    }
                }
            }

            helpCalls.Dispose();
        }

        private struct HelpCallData
        {
            public Entity CallerEntity;
            public float3 CallerPosition;
            public Entity ThreatTarget;
            public float3 ThreatPosition;
            public float ThreatValue;
            public float Radius;
        }
    }
}
