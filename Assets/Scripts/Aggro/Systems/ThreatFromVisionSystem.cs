using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DIG.Aggro.Components;
using DIG.Vision.Components;
using DIG.Vision.Systems;
using static Unity.Entities.WorldSystemFilterFlags;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19: Adds initial "sight aggro" when a target first appears in SeenTargetElement.
    /// Also updates visibility status and last known position for existing threat entries.
    /// 
    /// Runs after DetectionSystem to consume fresh vision data.
    /// OPTIMIZED: Parallelized Burst job implementation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DetectionSystem))]
    [BurstCompile]
    public partial struct ThreatFromVisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            new ThreatFromVisionJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct ThreatFromVisionJob : IJobEntity
        {
            [ReadOnly] public float DeltaTime;

            public void Execute(
                ref DynamicBuffer<ThreatEntry> threats,
                ref DynamicBuffer<SeenTargetElement> seen,
                in AggroConfig config)
            {
                float sightThreat = config.SightThreatValue;
                
                // Mark all threat entries as not visible by default
                for (int t = 0; t < threats.Length; t++)
                {
                    var entry = threats[t];
                    entry.IsCurrentlyVisible = false;
                    entry.TimeSinceVisible += DeltaTime;
                    threats[t] = entry;
                }
                
                // Process each seen target
                for (int s = 0; s < seen.Length; s++)
                {
                    var seenTarget = seen[s];
                    if (!seenTarget.IsVisibleNow)
                        continue;
                    
                    Entity targetEntity = seenTarget.Entity;
                    float3 targetPos = seenTarget.LastKnownPosition;
                    
                    // Find or create threat entry for this target
                    int existingIndex = -1;
                    for (int t = 0; t < threats.Length; t++)
                    {
                        if (threats[t].SourceEntity == targetEntity)
                        {
                            existingIndex = t;
                            break;
                        }
                    }
                    
                    if (existingIndex >= 0)
                    {
                        // Update existing entry - mark as visible, update position
                        var entry = threats[existingIndex];
                        bool wasHidden = !entry.IsCurrentlyVisible && entry.TimeSinceVisible > 0.5f;
                        
                        entry.IsCurrentlyVisible = true;
                        entry.TimeSinceVisible = 0f;
                        entry.LastKnownPosition = targetPos;
                        entry.SourceFlags |= ThreatSourceFlags.Vision;

                        // Add sight threat if target was hidden and just became visible again
                        if (wasHidden)
                        {
                            entry.ThreatValue += sightThreat * 0.5f; // Half sight threat for re-acquisition
                        }

                        threats[existingIndex] = entry;
                    }
                    else
                    {
                        // New target - add with initial sight threat
                        // Use at least 10.0 threat even if config has lower (handles old baked prefabs)
                        float initialThreat = math.max(sightThreat, 10f);
                        
                        if (threats.Length < config.MaxTrackedTargets)
                        {
                            threats.Add(new ThreatEntry
                            {
                                SourceEntity = targetEntity,
                                ThreatValue = initialThreat,
                                LastKnownPosition = targetPos,
                                TimeSinceVisible = 0f,
                                IsCurrentlyVisible = true,
                                SourceFlags = ThreatSourceFlags.Vision
                            });
                        }
                    }
                }
            }
        }
    }
}
