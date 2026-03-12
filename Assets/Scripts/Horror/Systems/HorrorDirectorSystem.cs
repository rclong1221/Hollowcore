using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Horror.Components;
using Player.Components;

namespace Horror.Systems
{
    /// <summary>
    /// Server-side system that manages global horror events.
    /// - Tracks mission time and builds global tension
    /// - Triggers global events (light flickers, vent bursts) visible to all players
    /// - Uses average player stress to modulate event frequency
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HorrorDirectorSystem : ISystem
    {
        private Random _random;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HorrorDirector>();
            state.RequireForUpdate<HorrorSettings>();
            _random = new Random(12345);
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            
            var director = SystemAPI.GetSingletonRW<HorrorDirector>();
            var settings = SystemAPI.GetSingleton<HorrorSettings>();
            
            // Update mission time
            director.ValueRW.MissionTime += dt;
            director.ValueRW.TimeSinceLastEvent += dt;
            
            // Build tension over time (slow ramp)
            director.ValueRW.GlobalTension = math.clamp(
                director.ValueRW.GlobalTension + director.ValueRW.TensionBuildRate * dt,
                0f, 1f
            );
            
            // Calculate average player stress
            float totalStress = 0f;
            int playerCount = 0;
            
            foreach (var stress in SystemAPI.Query<RefRO<PlayerStressState>>())
            {
                if (stress.ValueRO.MaxStress > 0)
                {
                    totalStress += stress.ValueRO.CurrentStress / stress.ValueRO.MaxStress;
                    playerCount++;
                }
            }
            
            float avgStress = playerCount > 0 ? totalStress / playerCount : 0f;
            
            // Combine tension and stress for event probability
            float eventModifier = math.max(director.ValueRO.GlobalTension, avgStress);
            
            // Calculate cooldown based on tension (higher tension = shorter cooldown)
            float cooldown = math.lerp(
                director.ValueRO.MaxEventCooldown,
                director.ValueRO.MinEventCooldown,
                eventModifier
            );
            
            // Check if we should trigger a global event
            if (director.ValueRO.TimeSinceLastEvent >= cooldown)
            {
                // Random chance based on tension
                _random = new Random(director.ValueRO.RandomSeed);
                float roll = _random.NextFloat();
                
                if (roll < eventModifier * 0.5f) // 50% chance at max tension
                {
                    // Trigger a global event using deferred ECB to avoid structural changes invalidating singleton handle
                    var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
                    TriggerGlobalEvent(ecb, director.ValueRO.GlobalTension, settings);
                    director.ValueRW.TimeSinceLastEvent = 0f;
                }
                
                // Update random seed
                director.ValueRW.RandomSeed = _random.NextUInt();
            }
        }
        
        private void TriggerGlobalEvent(EntityCommandBuffer ecb, float intensity, HorrorSettings settings)
        {
            // Pick a random global event type
            _random = new Random(_random.NextUInt());
            int eventChoice = _random.NextInt(0, 2); // 0 = flicker, 1 = vent
            
            HorrorEventType eventType = eventChoice == 0 
                ? HorrorEventType.LightFlicker 
                : HorrorEventType.VentBurst;
            
            float duration = eventType == HorrorEventType.LightFlicker
                ? _random.NextFloat(settings.FlickerDurationMin, settings.FlickerDurationMax)
                : 0.5f;
            
            // Create event entity
            var eventEntity = ecb.CreateEntity();
            ecb.AddComponent(eventEntity, new HorrorEventRequest
            {
                EventType = eventType,
                Intensity = intensity,
                Duration = duration,
                Position = float3.zero,
                TargetPlayer = Entity.Null,
                IsPrivate = false
            });
            
            // NOTE: Do NOT playback immediately to avoid structural changes invalidating RefRW handles in OnUpdate
            
            UnityEngine.Debug.Log($"[Horror] Global Event: {eventType}, Intensity={intensity:F2}, Duration={duration:F2}s");
        }
    }
}
