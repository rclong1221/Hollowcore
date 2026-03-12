using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using DIG.Survival.Environment; // for CurrentEnvironmentZone
using Player.Components;        // for PlayerStressState
using Visuals.Components;       // for FlashlightState, FlashlightConfig

namespace Player.Systems
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class DarknessStressSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (stress, zone, entity) in 
                     SystemAPI.Query<RefRW<PlayerStressState>, RefRO<CurrentEnvironmentZone>>()
                     .WithEntityAccess())
            {
                // Determine if Safe
                bool isSafe = !zone.ValueRO.IsDark;
                
                // If Dark, check Flashlight using optimized split components
                if (!isSafe)
                {
                    // Check FlashlightState (IsOn) and FlashlightConfig (battery)
                    if (SystemAPI.HasComponent<FlashlightState>(entity) && 
                        SystemAPI.HasComponent<FlashlightConfig>(entity))
                    {
                        var state = SystemAPI.GetComponent<FlashlightState>(entity);
                        var config = SystemAPI.GetComponent<FlashlightConfig>(entity);
                        if (state.IsOn && config.BatteryCurrent > 0)
                        {
                            isSafe = true;
                        }
                    }
                }
                
                // Logic
                if (isSafe)
                {
                    // Recovery
                    // Don't modify TimeInDarkness (it's a cumulative stat, maybe reset on safe? No, accumulator usually persists for stats)
                    // Recover stress
                    stress.ValueRW.CurrentStress -= stress.ValueRO.RecoveryRate * dt;
                }
                else
                {
                    // Gain Stress
                    float rate = stress.ValueRO.StressRate * zone.ValueRO.StressMultiplier;
                    stress.ValueRW.CurrentStress += rate * dt;
                    stress.ValueRW.TimeInDarkness += dt;
                    
                    if (rate > 0)
                    { 
                        // UnityEngine.Debug.Log($"[Darkness] Stress increasing: {stress.ValueRW.CurrentStress:F1} (Rate: {rate:F1})");
                    }
                }
                
                // Clamp
                stress.ValueRW.CurrentStress = math.clamp(stress.ValueRW.CurrentStress, 0f, stress.ValueRO.MaxStress);
            }
        }
    }
}
