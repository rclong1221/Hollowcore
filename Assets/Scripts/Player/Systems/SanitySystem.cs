using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Player;
using Player.Components;

namespace DIG.Player.Systems
{
    /// <summary>
    /// Server-side system that handles sanity drain from darkness and horror entities.
    /// Low sanity causes visual distortions and hallucinations.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct SanitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0) return;
            float time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (sanity, entity) in 
                SystemAPI.Query<RefRW<PlayerSanity>>()
                    .WithEntityAccess())
            {
                bool inDarkness = SystemAPI.HasComponent<InDarkness>(entity);
                bool nearHorror = SystemAPI.HasComponent<NearHorrorEntity>(entity);
                bool inSafeZone = SystemAPI.HasComponent<InSafeZone>(entity);
                
                bool isDraining = false;
                float drainRate = 0f;
                
                // Calculate drain from multiple sources
                if (inDarkness)
                {
                    drainRate += sanity.ValueRO.DarknessDrainRate;
                    isDraining = true;
                }
                
                if (nearHorror)
                {
                    // Get horror intensity if available
                    float horrorMultiplier = 1f;
                    if (SystemAPI.HasComponent<NearHorrorEntity>(entity))
                    {
                        horrorMultiplier = SystemAPI.GetComponent<NearHorrorEntity>(entity).Intensity;
                    }
                    drainRate += sanity.ValueRO.HorrorDrainRate * horrorMultiplier;
                    isDraining = true;
                }
                
                if (isDraining)
                {
                    // Drain sanity
                    sanity.ValueRW.Current = math.max(0, sanity.ValueRO.Current - drainRate * dt);
                    sanity.ValueRW.LastDrainTime = time;
                }
                else if (inSafeZone || !inDarkness)
                {
                    // Recover sanity (after delay)
                    float timeSinceDrain = time - sanity.ValueRO.LastDrainTime;
                    if (timeSinceDrain >= sanity.ValueRO.RecoveryDelay && sanity.ValueRO.Current < sanity.ValueRO.Max)
                    {
                        float recoveryRate = sanity.ValueRO.RecoveryRate;
                        if (inSafeZone) recoveryRate *= 2f; // Faster recovery in safe zones
                        
                        sanity.ValueRW.Current = math.min(
                            sanity.ValueRO.Max,
                            sanity.ValueRO.Current + recoveryRate * dt);
                    }
                }
                
                // Calculate distortion intensity for visual effects
                float percent = sanity.ValueRO.Percent;
                float threshold = sanity.ValueRO.DistortionThreshold;
                if (percent < threshold)
                {
                    sanity.ValueRW.DistortionIntensity = 1f - (percent / threshold);
                }
                else
                {
                    sanity.ValueRW.DistortionIntensity = 0f;
                }
            }
        }
    }
    
    /// <summary>
    /// Tag component added when player is in darkness.
    /// </summary>
    public struct InDarkness : IComponentData
    {
        /// <summary>Light level from 0 (pitch black) to 1 (bright).</summary>
        public float LightLevel;
    }
    
    /// <summary>
    /// Tag component added when player is near horror entities.
    /// </summary>
    public struct NearHorrorEntity : IComponentData
    {
        /// <summary>Intensity multiplier based on proximity and entity type.</summary>
        public float Intensity;
    }
    
    /// <summary>
    /// Tag component for safe zone areas.
    /// </summary>
    public struct InSafeZone : IComponentData { }
}
