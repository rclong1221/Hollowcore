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
    /// Client-side system that generates private hallucinations for high-stress players.
    /// - Whispers, phantom footsteps, visual distortions
    /// - Only affects the local player
    /// - Not networked (each client runs independently)
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HallucinationSystem : ISystem
    {
        private Random _random;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HorrorSettings>();
            _random = new Random((uint)System.DateTime.Now.Ticks);
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            
            if (!SystemAPI.TryGetSingleton<HorrorSettings>(out var settings))
                return;
            
            // Create ECB BEFORE the loop to avoid structural changes during iteration
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            foreach (var (hallucination, stress, entity) in 
                SystemAPI.Query<RefRW<PlayerHallucinationState>, RefRO<PlayerStressState>>()
                    .WithAll<GhostOwnerIsLocal>()
                    .WithEntityAccess())
            {
                // Calculate stress ratio
                float stressRatio = stress.ValueRO.MaxStress > 0 
                    ? stress.ValueRO.CurrentStress / stress.ValueRO.MaxStress 
                    : 0f;
                
                // Update hallucination intensity based on stress
                hallucination.ValueRW.HallucinationIntensity = math.clamp(
                    (stressRatio - settings.HallucinationThreshold) / (1f - settings.HallucinationThreshold),
                    0f, 1f
                );
                
                // Update existing hallucination
                if (hallucination.ValueRO.IsHallucinating)
                {
                    hallucination.ValueRW.HallucinationTimeRemaining -= dt;
                    if (hallucination.ValueRO.HallucinationTimeRemaining <= 0)
                    {
                        hallucination.ValueRW.IsHallucinating = false;
                        hallucination.ValueRW.HallucinationTimeRemaining = 0;
                    }
                }
                else
                {
                    hallucination.ValueRW.TimeSinceLastHallucination += dt;
                }
                
                // Check if we should trigger a new hallucination
                if (!hallucination.ValueRO.IsHallucinating && 
                    stressRatio > settings.HallucinationThreshold &&
                    hallucination.ValueRO.TimeSinceLastHallucination >= settings.MinHallucinationCooldown)
                {
                    // Probability scales with stress above threshold
                    float prob = hallucination.ValueRO.HallucinationIntensity * 
                                 settings.HallucinationProbabilityPerSecond * dt;
                    
                    _random = new Random(_random.NextUInt());
                    if (_random.NextFloat() < prob)
                    {
                        TriggerHallucination(ref ecb, entity, hallucination, settings);
                    }
                }
            }
            
            // Playback AFTER the loop - safe from structural change errors
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        private void TriggerHallucination(
            ref EntityCommandBuffer ecb, 
            Entity player,
            RefRW<PlayerHallucinationState> hallucination,
            HorrorSettings settings)
        {
            // Pick a random hallucination type
            _random = new Random(_random.NextUInt());
            int choice = _random.NextInt(0, 3);
            
            HorrorEventType eventType = choice switch
            {
                0 => HorrorEventType.PhantomFootsteps,
                1 => HorrorEventType.Whispers,
                2 => HorrorEventType.VisualDistortion,
                _ => HorrorEventType.Whispers
            };
            
            float duration = _random.NextFloat(0.5f, settings.MaxHallucinationDuration);
            
            // Update state
            hallucination.ValueRW.IsHallucinating = true;
            hallucination.ValueRW.CurrentHallucinationType = eventType;
            hallucination.ValueRW.HallucinationTimeRemaining = duration;
            hallucination.ValueRW.TimeSinceLastHallucination = 0;
            
            // Create event request (ECB playback happens in OnUpdate after loop)
            var eventEntity = ecb.CreateEntity();
            ecb.AddComponent(eventEntity, new HorrorEventRequest
            {
                EventType = eventType,
                Intensity = hallucination.ValueRO.HallucinationIntensity,
                Duration = duration,
                Position = float3.zero,
                TargetPlayer = player,
                IsPrivate = true
            });
            
            UnityEngine.Debug.Log($"[Horror] Hallucination: {eventType}, Intensity={hallucination.ValueRO.HallucinationIntensity:F2}, Duration={duration:F2}s");
        }
    }
}
