using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Manages ammo counts and reloading lifecycle.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(WeaponFireSystem))] // Update ammo status before firing check
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct WeaponAmmoSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (ammoState, ammoConfig, request, entity) in 
                     SystemAPI.Query<RefRW<WeaponAmmoState>, RefRO<WeaponAmmoComponent>, RefRO<UseRequest>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var stateRef = ref ammoState.ValueRW;
                var config = ammoConfig.ValueRO;

                // Handle Reloading
                if (stateRef.IsReloading)
                {
                    stateRef.ReloadProgress += deltaTime / config.ReloadTime;
                    
                    // EVENT-DRIVEN RELOAD LOGIC:
                    // We wait for the "ReloadComplete" animation event to clear IsReloading.
                    // However, we need a failsafe in case the event never fires (e.g. animation interrupted or missing event).
                    
                    // Failsafe: If reload takes 3x longer than expected, force finish
                    // This creates a "stuck protection" mechanism
                    if (stateRef.ReloadProgress > 3.0f)
                    {
                        // Force Finish Reload (Failsafe)
                        stateRef.IsReloading = false;
                        stateRef.ReloadProgress = 0f;

                        int needed = config.ClipSize - stateRef.AmmoCount;
                        int available = math.min(needed, stateRef.ReserveAmmo);
                        
                        stateRef.AmmoCount += available;
                        stateRef.ReserveAmmo -= available;
                        
                        #if UNITY_EDITOR
                        UnityEngine.Debug.LogWarning($"[SHOOT_DEBUG] [WeaponAmmoSystem] Reload FAILSAFE triggered for Entity {entity.Index} (No event received)");
                        #endif
                    }
                    else if (stateRef.ReloadProgress > 1f && stateRef.ReloadProgress < 1.1f) // Log once just after passing 1.0
                    {
                         #if UNITY_EDITOR
                         UnityEngine.Debug.LogWarning($"[SHOOT_DEBUG] [WeaponAmmoSystem] Reload Progress > 1.0 ({stateRef.ReloadProgress}). Waiting for animation event...");
                         #endif
                    }
                    else if (stateRef.ReloadProgress >= 1f)
                    {
                        // Wait for event...
                        // Do nothing here, let WeaponAnimationEventSystem handle completion
                    }
                    
                    continue; // Skip other checks if reloading
                }

                // Check for Manual Reload Input
                // Added explicit config.ClipSize > 0 check to prevent reload on non-reloadable weapons
                if (request.ValueRO.Reload && !stateRef.IsReloading && 
                    config.ClipSize > 0 && 
                    stateRef.AmmoCount < config.ClipSize && 
                    stateRef.ReserveAmmo > 0)
                {
                    stateRef.IsReloading = true;
                    stateRef.ReloadProgress = 0f;
                }

                // Check for Empty Clip (Auto Reload)
                if (config.ClipSize > 0 && stateRef.AmmoCount <= 0 && stateRef.ReserveAmmo > 0 && config.AutoReload && !stateRef.IsReloading)
                {
                    stateRef.IsReloading = true;
                    stateRef.ReloadProgress = 0f;
                }
            }
        }
    }
}
