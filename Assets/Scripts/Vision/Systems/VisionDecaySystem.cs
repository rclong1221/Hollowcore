using Unity.Burst;
using Unity.Entities;
using DIG.Vision.Components;

namespace DIG.Vision.Systems
{
    /// <summary>
    /// Increments TimeSinceLastSeen for entries that are no longer visible
    /// and prunes entries that exceed the memory duration.
    /// Runs every frame (lightweight — no physics queries).
    /// EPIC 15.17: Vision / Line-of-Sight System
    /// OPTIMIZED: Full Burst compilation for high performance.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DetectionSystem))]
    [BurstCompile]
    public partial struct VisionDecaySystem : ISystem
    {
        private EntityStorageInfoLookup _entityStorageInfoLookup;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityStorageInfoLookup = state.GetEntityStorageInfoLookup();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityStorageInfoLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;

            var settings = SystemAPI.HasSingleton<VisionSettings>()
                ? SystemAPI.GetSingleton<VisionSettings>()
                : VisionSettings.Default;

            float memoryDuration = settings.MemoryDuration;

            foreach (var (_, entity) in
                SystemAPI.Query<RefRO<DetectionSensor>>()
                .WithAll<SeenTargetElement>()
                .WithEntityAccess())
            {
                var seenBuffer = SystemAPI.GetBuffer<SeenTargetElement>(entity);

                for (int i = seenBuffer.Length - 1; i >= 0; i--)
                {
                    var entry = seenBuffer[i];

                    if (!entry.IsVisibleNow)
                    {
                        entry.TimeSinceLastSeen += deltaTime;

                        // Prune if memory expired or entity no longer exists
                        if (entry.TimeSinceLastSeen >= memoryDuration ||
                            entry.Entity == Entity.Null ||
                            !_entityStorageInfoLookup.Exists(entry.Entity))
                        {
                            seenBuffer.RemoveAtSwapBack(i);
                            continue;
                        }

                        seenBuffer[i] = entry;
                    }
                }
            }
        }
    }
}
