using Unity.Burst;
using Unity.Entities;
using DIG.Aggro.Components;

namespace DIG.Aggro.Systems
{
    /// <summary>
    /// EPIC 15.19: Processes ThreatModifierEvent components for taunt/detaunt abilities.
    /// Enables ability-based threat manipulation (taunt adds massive threat, 
    /// threat wipe clears threat, etc.)
    /// 
    /// Runs after ThreatFromDamageSystem to process ability-based modifications.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ThreatFromDamageSystem))]
    [BurstCompile]
    public partial struct ThreatModifierSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // No specific requirements - runs when modifier events exist
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var threatBufferLookup = SystemAPI.GetBufferLookup<ThreatEntry>(false);
            var configLookup = SystemAPI.GetComponentLookup<AggroConfig>(true);
            
            // Process all enabled ThreatModifierEvent components
            foreach (var (modifier, entity) in
                SystemAPI.Query<RefRO<ThreatModifierEvent>>()
                .WithAll<ThreatModifierEvent>()
                .WithEntityAccess())
            {
                // Check if this event is enabled
                if (!SystemAPI.IsComponentEnabled<ThreatModifierEvent>(entity))
                    continue;
                
                var evt = modifier.ValueRO;
                Entity targetAI = evt.TargetAI;
                Entity threatSource = evt.ThreatSource;
                
                // Validate target AI has threat buffer
                if (!threatBufferLookup.HasBuffer(targetAI))
                {
                    SystemAPI.SetComponentEnabled<ThreatModifierEvent>(entity, false);
                    continue;
                }
                
                var threatBuffer = threatBufferLookup[targetAI];
                
                // Find threat entry for this source
                int entryIndex = -1;
                for (int t = 0; t < threatBuffer.Length; t++)
                {
                    if (threatBuffer[t].SourceEntity == threatSource)
                    {
                        entryIndex = t;
                        break;
                    }
                }
                
                // Apply modification based on type
                switch (evt.Type)
                {
                    case ThreatModifierType.Add:
                        if (entryIndex >= 0)
                        {
                            var entry = threatBuffer[entryIndex];
                            entry.ThreatValue += evt.FlatThreatAdd;
                            entry.SourceFlags |= ThreatSourceFlags.Taunt;
                            threatBuffer[entryIndex] = entry;
                        }
                        else if (configLookup.HasComponent(targetAI))
                        {
                            var config = configLookup[targetAI];
                            if (threatBuffer.Length < config.MaxTrackedTargets)
                            {
                                threatBuffer.Add(new ThreatEntry
                                {
                                    SourceEntity = threatSource,
                                    ThreatValue = evt.FlatThreatAdd,
                                    LastKnownPosition = Unity.Mathematics.float3.zero,
                                    TimeSinceVisible = 0f,
                                    IsCurrentlyVisible = false,
                                    SourceFlags = ThreatSourceFlags.Taunt
                                });
                            }
                        }
                        break;

                    case ThreatModifierType.Multiply:
                        if (entryIndex >= 0)
                        {
                            var entry = threatBuffer[entryIndex];
                            entry.ThreatValue *= evt.ThreatMultiplier;
                            entry.SourceFlags |= ThreatSourceFlags.Taunt;
                            threatBuffer[entryIndex] = entry;
                        }
                        break;

                    case ThreatModifierType.Set:
                        if (entryIndex >= 0)
                        {
                            var entry = threatBuffer[entryIndex];
                            entry.ThreatValue = evt.FlatThreatAdd;
                            entry.SourceFlags |= ThreatSourceFlags.Taunt;
                            threatBuffer[entryIndex] = entry;
                        }
                        else if (configLookup.HasComponent(targetAI))
                        {
                            var config = configLookup[targetAI];
                            if (threatBuffer.Length < config.MaxTrackedTargets)
                            {
                                threatBuffer.Add(new ThreatEntry
                                {
                                    SourceEntity = threatSource,
                                    ThreatValue = evt.FlatThreatAdd,
                                    LastKnownPosition = Unity.Mathematics.float3.zero,
                                    TimeSinceVisible = 0f,
                                    IsCurrentlyVisible = false,
                                    SourceFlags = ThreatSourceFlags.Taunt
                                });
                            }
                        }
                        break;
                        
                    case ThreatModifierType.Wipe:
                        if (entryIndex >= 0)
                        {
                            threatBuffer.RemoveAtSwapBack(entryIndex);
                        }
                        break;
                }
                
                // Disable the event after processing
                SystemAPI.SetComponentEnabled<ThreatModifierEvent>(entity, false);
            }
        }
    }
}
