using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// Legacy shootable action system — superseded by WeaponFireSystem (modular components).
    /// WeaponBaker creates WeaponFireComponent (not ShootableAction), so this matches zero entities.
    /// Disabled until ShootableAction component is removed from the codebase.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(UsableActionSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ShootableActionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            foreach (var (action, shootable, shootableState, request, recoil, transform, entity) in 
                     SystemAPI.Query<RefRW<UsableAction>, RefRO<ShootableAction>, RefRW<ShootableState>, 
                                    RefRO<UseRequest>, RefRW<RecoilState>, RefRO<LocalTransform>>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                ref var actionRef = ref action.ValueRW;
                ref var stateRef = ref shootableState.ValueRW;
                ref var recoilRef = ref recoil.ValueRW;
                var config = shootable.ValueRO;

                // Update time since last shot
                stateRef.TimeSinceLastShot += deltaTime;

                // Update reload progress
                if (stateRef.IsReloading)
                {
                    stateRef.ReloadProgress += deltaTime / config.ReloadTime;
                    if (stateRef.ReloadProgress >= 1f)
                    {
                        // Reload complete
                        stateRef.IsReloading = false;
                        stateRef.ReloadProgress = 0f;
                        
                        int ammoNeeded = actionRef.ClipSize - actionRef.AmmoCount;
                        int ammoToAdd = math.min(ammoNeeded, actionRef.ReserveAmmo);
                        actionRef.AmmoCount += ammoToAdd;
                        actionRef.ReserveAmmo -= ammoToAdd;
                    }
                    continue; // Can't shoot while reloading
                }

                // Check for reload request (when empty or manual)
                if (actionRef.AmmoCount <= 0 && actionRef.ReserveAmmo > 0)
                {
                    stateRef.IsReloading = true;
                    stateRef.ReloadProgress = 0f;
                    continue;
                }

                // Calculate fire interval
                float fireInterval = 1f / config.FireRate;
                bool canFire = stateRef.TimeSinceLastShot >= fireInterval && 
                               actionRef.AmmoCount > 0 &&
                               !stateRef.IsReloading;

                // Handle fire request
                bool wantsFire = request.ValueRO.StartUse;
                if (!config.IsAutomatic)
                {
                    // Semi-auto: only fire on initial press
                    wantsFire = request.ValueRO.StartUse && !stateRef.IsFiring;
                }

                if (wantsFire && canFire)
                {
                    // Fire!
                    stateRef.IsFiring = true;
                    stateRef.TimeSinceLastShot = 0f;
                    actionRef.AmmoCount--;

                    // Apply spread
                    float spreadRad = math.radians(config.SpreadAngle + stateRef.CurrentSpread);
                    var random = Unity.Mathematics.Random.CreateFromIndex((uint)(entity.Index + SystemAPI.Time.ElapsedTime * 1000));
                    float2 spreadOffset = random.NextFloat2(-spreadRad, spreadRad);

                    float3 aimDir = math.normalize(request.ValueRO.AimDirection);
                    float3 right = math.cross(math.up(), aimDir);
                    float3 up = math.cross(aimDir, right);
                    float3 spreadDir = math.normalize(aimDir + right * spreadOffset.x + up * spreadOffset.y);

                    // Hitscan or projectile
                    if (config.UseHitscan)
                    {
                        // Perform raycast
                        var rayInput = new RaycastInput
                        {
                            Start = transform.ValueRO.Position + math.up() * 1.5f, // Eye height
                            End = transform.ValueRO.Position + math.up() * 1.5f + spreadDir * config.Range,
                            Filter = CollisionFilter.Default
                        };

                        if (physicsWorld.CastRay(rayInput, out var hit))
                        {
                            // TODO: Apply damage to hit.Entity
                            // For now, just register the hit
                        }
                    }
                    else
                    {
                        // TODO: Spawn projectile entity
                    }

                    // Apply recoil
                    recoilRef.RecoilVelocity.x += config.RecoilAmount;
                    recoilRef.RecoilVelocity.y += random.NextFloat(-config.RecoilAmount * 0.3f, config.RecoilAmount * 0.3f);

                    // Increase spread
                    stateRef.CurrentSpread = math.min(stateRef.CurrentSpread + config.SpreadAngle * 0.2f, config.SpreadAngle * 3f);

                    // Set cooldown
                    actionRef.CooldownRemaining = fireInterval;
                }
                else
                {
                    stateRef.IsFiring = false;
                }

                // Recover spread
                if (!stateRef.IsFiring)
                {
                    stateRef.CurrentSpread = math.max(0, stateRef.CurrentSpread - config.SpreadAngle * deltaTime * 2f);
                }
            }
        }
    }
}
