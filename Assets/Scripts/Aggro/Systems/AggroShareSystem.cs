using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19: Shares aggro between nearby allied AI entities.
    /// When one AI becomes aggroed, nearby allies within AggroShareRadius
    /// are alerted and add the threat source to their threat tables.
    /// 
    /// This creates pack behavior where attacking one enemy alerts the group.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ThreatFromVisionSystem))]
    [BurstCompile]
    public partial struct AggroShareSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Collect all share data in a single pass - no ECB needed for Phase 1
            var shareSources = new NativeList<ShareData>(16, Allocator.Temp);
            
            // Phase 1: Detect newly aggroed entities and collect share data directly
            foreach (var (config, aggroState, threatBuffer, transform, entity) in
                SystemAPI.Query<RefRO<AggroConfig>, RefRW<AggroState>, 
                    DynamicBuffer<ThreatEntry>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                // Check if just became aggroed (has threats but wasn't aggroed before)
                bool hasThreats = threatBuffer.Length > 0;
                
                if (hasThreats && !aggroState.ValueRO.IsAggroed)
                {
                    // Find highest threat for sharing
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
                    
                    // Mark as aggroed
                    aggroState.ValueRW.IsAggroed = true;
                    
                    // Skip sharing if disabled
                    if (config.ValueRO.AggroShareRadius <= 0f)
                        continue;
                    
                    // Collect share data directly instead of using temporary components
                    shareSources.Add(new ShareData
                    {
                        SourceEntity = entity,
                        Position = transform.ValueRO.Position,
                        ThreatTarget = highestSource,
                        ThreatPosition = highestPos,
                        ThreatValue = highestThreat * 0.5f, // Shared threat is 50%
                        ShareRadius = config.ValueRO.AggroShareRadius
                    });
                }
            }
            
            // Early exit if nothing to share
            if (shareSources.Length == 0)
            {
                shareSources.Dispose();
                return;
            }
            
            // Phase 2: Distribute aggro to nearby allies
            foreach (var (config, aggroState, transform, entity) in
                SystemAPI.Query<RefRO<AggroConfig>, RefRW<AggroState>, RefRO<LocalTransform>>()
                .WithAll<ThreatEntry>()
                .WithEntityAccess())
            {
                // Get mutable buffer outside query
                var threatBuffer = SystemAPI.GetBuffer<ThreatEntry>(entity);
                float3 myPos = transform.ValueRO.Position;
                int maxTargets = config.ValueRO.MaxTrackedTargets;
                
                for (int s = 0; s < shareSources.Length; s++)
                {
                    var source = shareSources[s];
                    
                    // Don't share to self
                    if (source.SourceEntity == entity)
                        continue;
                    
                    // Check if within share radius
                    float distance = math.distance(myPos, source.Position);
                    if (distance > source.ShareRadius)
                        continue;
                    
                    // Don't share threats about entities we're already tracking with higher threat
                    bool alreadyTracking = false;
                    for (int t = 0; t < threatBuffer.Length; t++)
                    {
                        if (threatBuffer[t].SourceEntity == source.ThreatTarget)
                        {
                            alreadyTracking = true;
                            // Add bonus threat from ally alert
                            var entry = threatBuffer[t];
                            entry.ThreatValue += source.ThreatValue * 0.25f; // 25% bonus
                            entry.SourceFlags |= ThreatSourceFlags.Social;
                            threatBuffer[t] = entry;
                            break;
                        }
                    }
                    
                    if (!alreadyTracking && threatBuffer.Length < maxTargets)
                    {
                        // Add new threat entry from ally alert
                        threatBuffer.Add(new ThreatEntry
                        {
                            SourceEntity = source.ThreatTarget,
                            ThreatValue = source.ThreatValue,
                            LastKnownPosition = source.ThreatPosition,
                            TimeSinceVisible = 999f, // Didn't see it ourselves
                            IsCurrentlyVisible = false,
                            SourceFlags = ThreatSourceFlags.Social
                        });
                    }
                }
            }
            
            shareSources.Dispose();
        }
        
        private struct ShareData
        {
            public Entity SourceEntity;
            public float3 Position;
            public Entity ThreatTarget;
            public float3 ThreatPosition;
            public float ThreatValue;
            public float ShareRadius;
        }
    }
}
