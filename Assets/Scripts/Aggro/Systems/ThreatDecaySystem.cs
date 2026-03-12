using Unity.Burst;
using Unity.Entities;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19: Passive threat reduction over time.
    /// Threat decays faster for hidden targets than visible ones.
    /// Removes entries that fall below minimum threat or exceed memory duration.
    /// 
    /// Runs in LateSimulationSystemGroup after all threat additions are processed.
    /// OPTIMIZED: Uses EntityStorageInfoLookup for Burst-compatible existence checks.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [BurstCompile]
    public partial struct ThreatDecaySystem : ISystem
    {
        private EntityStorageInfoLookup _entityStorageInfoLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AggroConfig>();
            _entityStorageInfoLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityStorageInfoLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (config, threatBuffer, entity) in
                SystemAPI.Query<RefRO<AggroConfig>, DynamicBuffer<ThreatEntry>>()
                .WithEntityAccess())
            {
                var threats = threatBuffer;
                var cfg = config.ValueRO;
                
                // Clamp decay rates to reasonable values (handles old baked prefabs with extreme values)
                // Max decay of 2.0/s means minimum 5 seconds to lose aggro from sight threat of 10
                float visDecay = Unity.Mathematics.math.clamp(cfg.VisibleDecayRate, 0f, 2f);
                float hidDecay = Unity.Mathematics.math.clamp(cfg.HiddenDecayRate, 0f, 2f);
                
                // Process each threat entry - iterate backwards for safe removal
                for (int t = threats.Length - 1; t >= 0; t--)
                {
                    var entry = threats[t];
                    
                    // Check if source entity still exists (Burst-compatible)
                    if (!_entityStorageInfoLookup.Exists(entry.SourceEntity))
                    {
                        threats.RemoveAtSwapBack(t);
                        continue;
                    }
                    
                    // Apply decay based on visibility
                    float decayRate = entry.IsCurrentlyVisible 
                        ? visDecay 
                        : hidDecay;
                    
                    entry.ThreatValue -= decayRate * deltaTime;
                    
                    // Remove if below minimum threat
                    if (entry.ThreatValue < cfg.MinimumThreat)
                    {
                        threats.RemoveAtSwapBack(t);
                        continue;
                    }
                    
                    // Remove if exceeded memory duration (only for hidden targets)
                    if (!entry.IsCurrentlyVisible && entry.TimeSinceVisible > cfg.MemoryDuration)
                    {
                        threats.RemoveAtSwapBack(t);
                        continue;
                    }
                    
                    threats[t] = entry;
                }
            }
        }
    }
}
