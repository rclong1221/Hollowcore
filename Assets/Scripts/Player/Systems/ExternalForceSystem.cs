using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Player.Components;

namespace Player.Systems
{
    /// <summary>
    /// System managing external forces (wind, explosions, knockback, etc).
    /// <para>
    /// <b>Architecture:</b> Single-pass buffer processing for performance.
    /// All operations (decay, accumulation, cleanup) in one job.
    /// </para>
    /// <para><b>Performance:</b> Single buffer pass, inlined math, no utility functions.</para>
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct ExternalForceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ExternalForceState>();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            
            // Process force requests first (add new forces)
            state.Dependency = new ProcessForceRequestsJob().ScheduleParallel(state.Dependency);
            
            // Single-pass: Decay, accumulate, and cleanup forces
            state.Dependency = new ProcessAndAccumulateForcesJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
        }
        
        /// <summary>
        /// Processes AddExternalForceRequest components and adds to force buffer.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct ProcessForceRequestsJob : IJobEntity
        {
            public void Execute(
                Entity entity,
                ref DynamicBuffer<ExternalForceElement> forceBuffer,
                ref AddExternalForceRequest request,
                EnabledRefRW<AddExternalForceRequest> requestEnabled,
                in ExternalForceSettings settings)
            {
                if (!requestEnabled.ValueRO) return;
                
                // Check for existing force from same source
                bool found = false;
                for (int i = 0; i < forceBuffer.Length; i++)
                {
                    if (forceBuffer[i].SourceId == request.SourceId)
                    {
                        // Update existing force
                        var element = forceBuffer[i];
                        element.Force += request.Force;
                        element.Decay = request.Decay;
                        element.FramesRemaining = request.SoftFrames;
                        forceBuffer[i] = element;
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    // Add new force
                    forceBuffer.Add(new ExternalForceElement
                    {
                        SourceId = request.SourceId,
                        Force = request.Force,
                        Decay = request.Decay,
                        FramesRemaining = request.SoftFrames,
                        ForceMode = 0
                    });
                }
                
                // Disable the request
                requestEnabled.ValueRW = false;
            }
        }
        
        /// <summary>
        /// Single-pass job: decays forces, accumulates total, and removes expired forces.
        /// Much more efficient than separate passes.
        /// </summary>
        [BurstCompile]
        [WithAll(typeof(Simulate))]
        public partial struct ProcessAndAccumulateForcesJob : IJobEntity
        {
            public float DeltaTime;
            
            public void Execute(
                ref DynamicBuffer<ExternalForceElement> forceBuffer,
                ref ExternalForceState forceState,
                in ExternalForceSettings settings)
            {
                float3 totalForce = float3.zero;
                float maxForceSq = settings.MaxForceMagnitude * settings.MaxForceMagnitude;
                
                // Process buffer in reverse for safe removal
                for (int i = forceBuffer.Length - 1; i >= 0; i--)
                {
                    var element = forceBuffer[i];
                    
                    // Handle soft force distribution
                    float3 frameForce;
                    if (element.FramesRemaining > 0)
                    {
                        // Distribute force across remaining frames
                        frameForce = element.Force / element.FramesRemaining;
                        element.Force -= frameForce;
                        element.FramesRemaining--;
                    }
                    else
                    {
                        // Apply full force with decay
                        frameForce = element.Force;
                        element.Force *= (1f - element.Decay * DeltaTime);
                    }
                    
                    // Accumulate to total
                    totalForce += frameForce;
                    
                    // Check if force has expired
                    bool expired = math.lengthsq(element.Force) < 0.01f && element.FramesRemaining <= 0;
                    
                    if (expired)
                    {
                        // Remove via swap-back
                        forceBuffer.RemoveAtSwapBack(i);
                    }
                    else
                    {
                        forceBuffer[i] = element;
                    }
                }
                
                // Apply force resistance
                totalForce *= (1f / math.max(0.01f, settings.ForceResistance));
                
                // Apply damping when outside force zones
                if (forceState.IsInForceZone == 0 && forceState.CurrentDamping > 0)
                {
                    float dampFactor = 1f - (forceState.CurrentDamping * DeltaTime);
                    totalForce *= math.max(0f, dampFactor);
                }
                
                // Clamp to max magnitude
                float forceSq = math.lengthsq(totalForce);
                if (forceSq > maxForceSq)
                {
                    totalForce = math.normalize(totalForce) * settings.MaxForceMagnitude;
                }
                
                forceState.AccumulatedForce = totalForce;
            }
        }
    }
    
    /// <summary>
    /// Static helper for radial force calculations.
    /// NOTE: For Burst jobs, inline this math directly.
    /// </summary>
    public static class ExternalForceMath
    {
        /// <summary>
        /// Calculates radial force. For Burst jobs, inline this logic.
        /// </summary>
        public static float3 CalculateRadialForce(float3 targetPosition, float3 center, float forceMagnitude, float falloffRadius)
        {
            float3 direction = targetPosition - center;
            float distance = math.length(direction);
            
            if (distance < 0.01f) return float3.zero;
            
            direction = direction / distance;
            
            float force = forceMagnitude;
            if (falloffRadius > 0 && distance > falloffRadius)
            {
                float falloff = 1f - ((distance - falloffRadius) / falloffRadius);
                force *= math.saturate(falloff);
            }
            
            return direction * force;
        }
    }
}
