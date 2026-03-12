using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using DIG.Weapons.Systems;
using DIG.Surface;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Spawns visual GameObjects for ECS projectile entities.
    /// Handles tracer rendering, projectile mesh display, and trails.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ProjectilePresentationSystem : SystemBase
    {
        private EffectPoolManager _poolManager;
        private EffectPrefabRegistry _registry;

        protected override void OnStartRunning()
        {
            _poolManager = EffectPoolManager.Instance;
            _registry = EffectPrefabRegistry.Instance;
        }

        protected override void OnUpdate()
        {
            if (_poolManager == null || _registry == null)
            {
                _poolManager = EffectPoolManager.Instance;
                _registry = EffectPrefabRegistry.Instance;
                if (_poolManager == null || _registry == null)
                    return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Spawn visual representations for new projectiles
            foreach (var (projectile, transform, entity) in
                     SystemAPI.Query<RefRO<Projectile>, RefRO<LocalTransform>>()
                     .WithNone<ProjectileVisualLink>()
                     .WithEntityAccess())
            {
                // Get projectile visual prefab based on type
                int prefabIndex = GetPrefabIndexForProjectile(projectile.ValueRO);
                var effectEntry = _registry.GetEffect(prefabIndex);

                if (effectEntry?.Prefab != null)
                {
                    var pos = transform.ValueRO.Position;
                    var rot = transform.ValueRO.Rotation;

                    var go = _poolManager.Spawn(
                        effectEntry.Prefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        projectile.ValueRO.Lifetime
                    );

                    // Store link to visual
                    ecb.AddComponent(entity, new ProjectileVisualLink
                    {
                        VisualInstanceId = go.GetInstanceID()
                    });

                    // Store reference for position updates
                    ProjectileVisualTracker.Register(entity, go);
                }
            }

            // Update visual positions to match ECS entities
            foreach (var (transform, link) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProjectileVisualLink>>())
            {
                ProjectileVisualTracker.UpdatePosition(
                    link.ValueRO.VisualInstanceId,
                    transform.ValueRO.Position,
                    transform.ValueRO.Rotation
                );
            }

            // Handle impacted projectiles - spawn impact effects
            foreach (var (impacted, projectile, entity) in
                     SystemAPI.Query<RefRO<ProjectileImpacted>, RefRO<Projectile>>()
                     .WithNone<ProjectileImpactEffectSpawned>()
                     .WithEntityAccess())
            {
                var impact = impacted.ValueRO;

                // Queue impact effect
                SpawnImpactEffect(impact.ImpactPoint, impact.ImpactNormal, projectile.ValueRO);

                // Mark as handled
                ecb.AddComponent<ProjectileImpactEffectSpawned>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private int GetPrefabIndexForProjectile(Projectile projectile)
        {
            // Map projectile type to visual prefab
            // Could be expanded based on ProjectileType component
            return EffectPrefabRegistry.EffectIds.Tracer_Standard;
        }

        private void SpawnImpactEffect(float3 position, float3 normal, Projectile projectile)
        {
            // EPIC 15.24: Route through unified SurfaceImpactQueue
            SurfaceImpactQueue.Enqueue(new SurfaceImpactData
            {
                Position = position,
                Normal = normal,
                Velocity = float3.zero,
                SurfaceId = SurfaceID.Default,
                ImpactClass = ImpactClassResolver.FromDamage(projectile.Damage),
                SurfaceMaterialId = 0,
                Intensity = 1f,
                LODTier = EffectLODTier.Full
            });
        }
    }

    /// <summary>
    /// Link between ECS projectile and its visual GameObject.
    /// </summary>
    public struct ProjectileVisualLink : IComponentData
    {
        public int VisualInstanceId;
    }

    /// <summary>
    /// Tag to mark that impact effect has been spawned.
    /// </summary>
    public struct ProjectileImpactEffectSpawned : IComponentData { }

    /// <summary>
    /// EPIC 14.20: Tracks projectile visual GameObjects for position updates.
    /// Static tracker to bridge ECS and GameObject worlds.
    /// </summary>
    public static class ProjectileVisualTracker
    {
        private static System.Collections.Generic.Dictionary<int, GameObject> _visuals =
            new System.Collections.Generic.Dictionary<int, GameObject>();
        private static System.Collections.Generic.Dictionary<Entity, int> _entityToInstanceId =
            new System.Collections.Generic.Dictionary<Entity, int>();

        public static void Register(Entity entity, GameObject visual)
        {
            int instanceId = visual.GetInstanceID();
            _visuals[instanceId] = visual;
            _entityToInstanceId[entity] = instanceId;
        }

        public static void UpdatePosition(int instanceId, float3 position, quaternion rotation)
        {
            if (_visuals.TryGetValue(instanceId, out var go) && go != null)
            {
                go.transform.position = new Vector3(position.x, position.y, position.z);
                go.transform.rotation = new Quaternion(rotation.value.x, rotation.value.y, rotation.value.z, rotation.value.w);
            }
        }

        public static void Unregister(Entity entity)
        {
            if (_entityToInstanceId.TryGetValue(entity, out int instanceId))
            {
                _visuals.Remove(instanceId);
                _entityToInstanceId.Remove(entity);
            }
        }

        public static void Clear()
        {
            _visuals.Clear();
            _entityToInstanceId.Clear();
        }
    }
}
