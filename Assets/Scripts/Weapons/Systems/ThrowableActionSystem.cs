using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using DIG.Items;
using DIG.Shared;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.10: Handles throwable charging and projectile spawning.
    /// Spawns projectiles directly from the prefab entity stored on the weapon.
    ///
    /// Hand Position Replication:
    /// - Client captures hand socket position via SocketPositionSyncBridge → SocketPositionData
    /// - PlayerInputSystem writes MainHandPosition to PlayerInput (IInputComponentData)
    /// - PlayerInput replicates client→server via NetCode input stream
    /// - This system reads MainHandPosition from PlayerInput for accurate spawn position on both client and server
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerToItemInputSystem))]
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct ThrowableActionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            bool isServer = state.WorldUnmanaged.IsServer();
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Get component lookups
            var playerTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var charItemLookup = SystemAPI.GetComponentLookup<CharacterItem>(true);

            // EPIC 15.29: Lookups for stamping weapon DamageProfile + modifiers onto projectiles
            var damageProfileLookup = SystemAPI.GetComponentLookup<DamageProfile>(true);
            var modifierLookup = SystemAPI.GetBufferLookup<WeaponModifier>(true);

            // MAIN LOOP: Processes throwable weapons
            foreach (var (actionRW, throwableRO, throwableStateRW, requestRO, entity) in
                     SystemAPI.Query<RefRW<UsableAction>, RefRO<ThrowableAction>, RefRW<ThrowableState>, RefRO<UseRequest>>()
                     .WithEntityAccess())
            {
                ref var actionRef = ref actionRW.ValueRW;
                ref var stateRef = ref throwableStateRW.ValueRW;
                var config = throwableRO.ValueRO;
                var request = requestRO.ValueRO;

                // CharacterItem is required for owner tracking
                if (!charItemLookup.HasComponent(entity)) continue;
                var charItem = charItemLookup[entity];

                // Get player position (NOT weapon position - weapons are at y=-1000!)
                Entity ownerEntity = charItem.OwnerEntity;
                float3 playerPos = float3.zero;
                if (ownerEntity != Entity.Null && playerTransformLookup.HasComponent(ownerEntity))
                {
                    playerPos = playerTransformLookup[ownerEntity].Position;
                }

                // Calculate Spawn Position - prefer hand position from PlayerInput (replicated client→server)
                float3 spawnPos = playerPos + new float3(0, 1.5f, 0); // Default fallback
                float3 cameraOffset = float3.zero;
                bool useHandPosition = false;

                // PRIORITY 1: Use MainHandPosition from PlayerInput (replicated from client to server)
                var playerInputLookup = SystemAPI.GetComponentLookup<PlayerInput>(true);
                if (playerInputLookup.HasComponent(ownerEntity))
                {
                    var playerInput = playerInputLookup[ownerEntity];
                    if (playerInput.MainHandPositionValid == 1)
                    {
                        spawnPos = playerInput.MainHandPosition;
                        useHandPosition = true;
                    }
                }

                // PRIORITY 2: Fallback to SocketPositionData (only available on client)
                if (!useHandPosition)
                {
                    var socketDataLookup = SystemAPI.GetComponentLookup<SocketPositionData>(true);
                    if (socketDataLookup.HasComponent(ownerEntity) && socketDataLookup[ownerEntity].IsValid)
                    {
                        spawnPos = socketDataLookup[ownerEntity].MainHandPosition;
                        useHandPosition = true;
                    }
                }

                // PRIORITY 3: Fallback to player position + height offset
                if (!useHandPosition)
                {
                    float3 heightOffset = new float3(0, 1.5f, 0);

                    var viewConfigLookup = SystemAPI.GetComponentLookup<global::Player.Components.CameraViewConfig>(true);
                    if (viewConfigLookup.HasComponent(ownerEntity))
                    {
                        var viewConfig = viewConfigLookup[ownerEntity];
                        heightOffset = (viewConfig.ActiveViewType == global::Player.Components.CameraViewType.Combat)
                                       ? viewConfig.CombatPivotOffset
                                       : viewConfig.AdventurePivotOffset;

                        if (viewConfig.ActiveViewType == global::Player.Components.CameraViewType.Combat)
                            cameraOffset = viewConfig.CombatCameraOffset;
                    }

                    spawnPos = playerPos + heightOffset;
                }

                // Get camera offset for convergence calculation
                var viewConfigLookupForCamera = SystemAPI.GetComponentLookup<global::Player.Components.CameraViewConfig>(true);
                if (viewConfigLookupForCamera.HasComponent(ownerEntity))
                {
                    var viewConfig = viewConfigLookupForCamera[ownerEntity];
                    if (viewConfig.ActiveViewType == global::Player.Components.CameraViewType.Combat)
                        cameraOffset = viewConfig.CombatCameraOffset;
                }

                // Calculate converged aim direction (crosshair alignment)
                float3 lookDir = math.normalizesafe(request.AimDirection, math.forward());
                quaternion camRot = quaternion.LookRotationSafe(lookDir, math.up());
                float3 worldCamOffset = math.mul(camRot, cameraOffset);
                float3 approxCamPos = spawnPos + worldCamOffset - (lookDir * 2.5f);
                float3 targetPoint = approxCamPos + (lookDir * 50.0f);
                stateRef.AimDirection = math.normalizesafe(targetPoint - spawnPos);

                // Calculate and store spawn position (with safety offset)
                float3 throwDirForOffset = math.normalizesafe(stateRef.AimDirection, math.forward());
                float safetyOffset = useHandPosition ? 0.3f : 0.8f;
                stateRef.SpawnPosition = spawnPos + throwDirForOffset * safetyOffset;

                // Handle charging
                if (request.StartUse && actionRef.AmmoCount > 0)
                {
                    if (!stateRef.IsCharging)
                    {
                        // Start charging
                        stateRef.IsCharging = true;
                        stateRef.ChargeProgress = 0f;
                    }
                    else
                    {
                        // Continue charging
                        stateRef.ChargeProgress += deltaTime / config.ChargeTime;
                        stateRef.ChargeProgress = math.min(stateRef.ChargeProgress, 1f);
                    }
                }
                else if (stateRef.IsCharging && request.StopUse)
                {
                    // Release throw - spawn projectile
                    stateRef.IsCharging = false;

                    // Calculate throw force and direction
                    float force = math.lerp(config.MinForce, config.MaxForce, stateRef.ChargeProgress);
                    float3 throwDir = math.normalizesafe(stateRef.AimDirection, math.forward());
                    float3 velocity = throwDir * force;
                    float3 finalSpawnPos = stateRef.SpawnPosition;

                    // SERVER ONLY: Spawn projectile (ghosts will replicate to clients)
                    // COMPOSITIONAL: Prefab defines behavior via baked components. We only set runtime values.
                    if (isServer && config.ProjectilePrefab != Entity.Null && ownerEntity != Entity.Null)
                    {
                        Entity projectile = ecb.Instantiate(config.ProjectilePrefab);

                        // Set transform (runtime only)
                        ecb.SetComponent(projectile, LocalTransform.FromPositionRotation(
                            finalSpawnPos,
                            quaternion.LookRotationSafe(throwDir, math.up())
                        ));

                        // COMPOSITIONAL: Read prefab-baked Projectile, only modify runtime values (Owner, ElapsedTime)
                        if (SystemAPI.HasComponent<Projectile>(config.ProjectilePrefab))
                        {
                            var prefabProjectile = SystemAPI.GetComponent<Projectile>(config.ProjectilePrefab);
                            prefabProjectile.Owner = ownerEntity;
                            prefabProjectile.ElapsedTime = 0f;
                            ecb.SetComponent(projectile, prefabProjectile);
                        }

                        // COMPOSITIONAL: Read prefab-baked ProjectileMovement, only set Velocity
                        if (SystemAPI.HasComponent<ProjectileMovement>(config.ProjectilePrefab))
                        {
                            var prefabMovement = SystemAPI.GetComponent<ProjectileMovement>(config.ProjectilePrefab);
                            prefabMovement.Velocity = velocity;
                            ecb.SetComponent(projectile, prefabMovement);
                        }

                        // COMPOSITIONAL: Read prefab-baked ProjectileImpact, only reset CurrentBounces
                        if (SystemAPI.HasComponent<ProjectileImpact>(config.ProjectilePrefab))
                        {
                            var prefabImpact = SystemAPI.GetComponent<ProjectileImpact>(config.ProjectilePrefab);
                            prefabImpact.CurrentBounces = 0;
                            ecb.SetComponent(projectile, prefabImpact);
                        }

                        ecb.AddComponent<Simulate>(projectile);

                        // EPIC 15.29: Stamp weapon's DamageProfile onto projectile
                        if (damageProfileLookup.HasComponent(entity))
                        {
                            ecb.AddComponent(projectile, damageProfileLookup[entity]);
                        }

                        // EPIC 15.29: Stamp weapon's modifier buffer onto projectile
                        if (modifierLookup.HasBuffer(entity))
                        {
                            var srcMods = modifierLookup[entity];
                            var dstBuffer = ecb.AddBuffer<WeaponModifier>(projectile);
                            for (int i = 0; i < srcMods.Length; i++)
                                dstBuffer.Add(srcMods[i]);
                        }

                        // Set GhostOwner for OwnerPredicted ghosts
                        if (SystemAPI.HasComponent<GhostOwner>(ownerEntity))
                        {
                            var ownerGhost = SystemAPI.GetComponent<GhostOwner>(ownerEntity);
                            ecb.AddComponent(projectile, new GhostOwner { NetworkId = ownerGhost.NetworkId });
                        }
                    }

                    // Consume ammo (both client and server for prediction)
                    actionRef.AmmoCount--;
                    stateRef.ChargeProgress = 0f;
                }
                else if (!request.StartUse)
                {
                    // Cancel charge if released without throw
                    stateRef.IsCharging = false;
                    stateRef.ChargeProgress = 0f;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
