using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.NetCode;
using DIG.Voxel;
using DIG.Combat.Components;
using UnityEngine;

namespace DIG.Weapons.Systems
{
    /// <summary>
    /// EPIC 15.10: Handles projectile explosion triggers (timer and impact).
    /// Bridges the Weapon/Projectile system to the Voxel Destruction system.
    ///
    /// When a projectile meets detonation conditions:
    /// 1. Adds VoxelDetonationRequest (trigger for VoxelDetonationSystem)
    /// 2. Adds VoxelDamageRequest with explosion config (radius, damage)
    /// 3. Adds ProjectileDetonated to prevent double-triggering
    ///
    /// The VoxelDetonationSystem then processes the request and creates the crater.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    // [BurstCompile] // Disabled for logging
    public partial struct ProjectileExplosionSystem : ISystem
    {
        private bool _loggedOnce;

        // [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int timerCount = 0;
            int impactCount = 0;
            
            // Only create crater requests on the server - clients receive voxel changes via network
            bool isServer = state.WorldUnmanaged.IsServer();

            // === TIMER-BASED DETONATION ===
            // Projectiles with DetonateOnTimer that have exceeded their fuse time
            var impactedLookup = SystemAPI.GetComponentLookup<ProjectileImpacted>(true);
            impactedLookup.Update(ref state);
            float deltaTime = SystemAPI.Time.DeltaTime;

            // === TIMER-BASED DETONATION ===
            // Projectiles with DetonateOnTimer that have exceeded their fuse time
            foreach (var (projectile, timer, explosionConfig, transform, entity) in
                     SystemAPI.Query<RefRW<Projectile>, RefRO<DetonateOnTimer>, RefRO<ProjectileExplosionConfig>, RefRO<LocalTransform>>()
                     .WithNone<ProjectileDetonated>()
                     .WithAll<Simulate>()
                     .WithEntityAccess())
            {
                timerCount++;
                
                // FIX: ProjectileSystem stops updating ElapsedTime after impact (WithNone<ProjectileImpacted>).
                // We must manually tick the timer for impacted projectiles (like grenades sitting on the ground).
                if (impactedLookup.HasComponent(entity))
                {
                    projectile.ValueRW.ElapsedTime += deltaTime;
                }

                // Use FuseTime if set, otherwise use Projectile.Lifetime
                float fuseTime = timer.ValueRO.FuseTime > 0 ? timer.ValueRO.FuseTime : projectile.ValueRO.Lifetime;

                if (Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[GRENADE] ExplosionSystem: Timer projectile {entity.Index}, Elapsed={projectile.ValueRO.ElapsedTime:F2}/{fuseTime:F1}s");
                }

                if (projectile.ValueRO.ElapsedTime >= fuseTime)
                {
                    Debug.Log($"[GRENADE] TIMER DETONATION! Entity={entity.Index} at {transform.ValueRO.Position}, Radius={explosionConfig.ValueRO.ExplosionRadius}, Damage={explosionConfig.ValueRO.ExplosionDamage}");
                    TriggerDetonation(ref ecb, entity, transform.ValueRO.Position, explosionConfig.ValueRO, projectile.ValueRO.Owner, isServer);
                }
            }

            // === IMPACT-BASED DETONATION ===
            // Projectiles with DetonateOnImpact that have the ProjectileImpacted component
            foreach (var (impacted, explosionConfig, projectile, entity) in
                     SystemAPI.Query<RefRO<ProjectileImpacted>, RefRO<ProjectileExplosionConfig>, RefRO<Projectile>>()
                     .WithAll<DetonateOnImpact, Simulate>()
                     .WithNone<ProjectileDetonated>()
                     .WithEntityAccess())
            {
                impactCount++;
                Debug.Log($"[GRENADE] IMPACT DETONATION! Entity={entity.Index} at {impacted.ValueRO.ImpactPoint}, Radius={explosionConfig.ValueRO.ExplosionRadius}, Damage={explosionConfig.ValueRO.ExplosionDamage}");
                TriggerDetonation(ref ecb, entity, impacted.ValueRO.ImpactPoint, explosionConfig.ValueRO, projectile.ValueRO.Owner, isServer);
            }

            // Log status once if no explosive projectiles found
            if (timerCount == 0 && impactCount == 0 && !_loggedOnce)
            {
                Debug.Log("[GRENADE] ExplosionSystem: No explosive projectiles found. Need: Projectile + (DetonateOnTimer OR DetonateOnImpact) + ProjectileExplosionConfig + LocalTransform + Simulate");
                _loggedOnce = true;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        /// <summary>
        /// Triggers detonation: voxel crater + entity AOE damage + detonated tag.
        /// Crater and entity damage are server-only. Detonated tag is on both for prediction.
        /// </summary>
        private void TriggerDetonation(ref EntityCommandBuffer ecb, Entity projectileEntity, float3 position, ProjectileExplosionConfig config, Entity owner, bool isServer)
        {
            Debug.Log($"[GRENADE] TriggerDetonation: Entity={projectileEntity.Index}, IsServer={isServer}");

            // 1. Add crater creation request (triggers VoxelExplosionSystem) - SERVER ONLY
            // Clients receive voxel changes via VoxelExplosionNetworkSystem RPC
            if (isServer)
            {
                ecb.AddComponent(projectileEntity, new DIG.Voxel.Systems.Interaction.CreateCraterRequest
                {
                    Center = position,
                    Radius = config.ExplosionRadius,
                    Strength = 1.0f, // Full destruction
                    ReplaceMaterial = 0, // Air
                    SpawnLoot = config.SpawnLoot,
                    Seed = (uint)UnityEngine.Random.Range(1, int.MaxValue)
                });
                Debug.Log($"[GRENADE] TriggerDetonation: Added CreateCraterRequest at {position} R={config.ExplosionRadius}");
            }

            // 2. Entity AOE damage — SERVER ONLY
            // Reuses ModifierExplosionSystem infrastructure (physics overlap, hitbox resolution, distance falloff)
            if (isServer && config.ExplosionDamage > 0)
            {
                var explosionDamageEntity = ecb.CreateEntity();
                ecb.AddComponent(explosionDamageEntity, new ModifierExplosionRequest
                {
                    Position = position,
                    SourceEntity = owner,
                    Damage = config.ExplosionDamage,
                    Radius = config.ExplosionRadius,
                    Element = DIG.Targeting.Theming.DamageType.Physical,
                    KnockbackForce = 0f
                });
            }

            // 3. Mark as detonated (prevents double-triggering) - BOTH client and server for prediction
            ecb.AddComponent(projectileEntity, new ProjectileDetonated
            {
                DetonationPoint = position
            });

            // 4. Update position to detonation point (for VoxelDetonationSystem)
            ecb.SetComponent(projectileEntity, LocalTransform.FromPosition(position));
        }
    }
}
