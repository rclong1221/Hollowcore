using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DIG.Weapons.Audio;

namespace DIG.Weapons.Effects
{
    /// <summary>
    /// EPIC 14.20: Hybrid presentation system that instantiates GameObjects for ECS effect entities.
    /// Bridges between pure ECS effect spawning and Unity's GameObject/VFX systems.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(EffectLifetimeSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class EffectPresentationSystem : SystemBase
    {
        private EffectPoolManager _poolManager;
        private EffectPrefabRegistry _registry;
        private WeaponAudioManager _audioManager;
        private SurfaceAudioLibrary _surfaceAudioLibrary;

        protected override void OnCreate()
        {
            RequireForUpdate<MuzzleFlashTag>();
        }

        protected override void OnStartRunning()
        {
            _poolManager = EffectPoolManager.Instance;
            _registry = EffectPrefabRegistry.Instance;
            _audioManager = WeaponAudioManager.Instance;
            _surfaceAudioLibrary = SurfaceAudioLibrary.Instance;
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

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Process muzzle flash entities - spawn GameObjects
            foreach (var (tag, transform, entity) in
                     SystemAPI.Query<RefRO<MuzzleFlashTag>, RefRO<LocalTransform>>()
                     .WithNone<EffectGameObjectLink>()
                     .WithEntityAccess())
            {
                var effectEntry = _registry.GetMuzzleFlash(tag.ValueRO.PrefabIndex);
                if (effectEntry?.Prefab != null)
                {
                    var pos = transform.ValueRO.Position;
                    var rot = transform.ValueRO.Rotation;

                    var go = _poolManager.Spawn(
                        effectEntry.Prefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        effectEntry.DefaultLifetime
                    );

                    // Add muzzle light flash
                    SpawnMuzzleLight(pos);

                    // Link GameObject to entity for cleanup
                    ecb.AddComponent(entity, new EffectGameObjectLink { InstanceId = go.GetInstanceID() });
                }
            }

            // Process shell casing entities
            foreach (var (tag, transform, entity) in
                     SystemAPI.Query<RefRO<ShellCasingTag>, RefRO<LocalTransform>>()
                     .WithNone<EffectGameObjectLink>()
                     .WithEntityAccess())
            {
                var effectEntry = _registry.GetShellEject(tag.ValueRO.PrefabIndex);
                if (effectEntry?.Prefab != null)
                {
                    var pos = transform.ValueRO.Position;
                    var rot = transform.ValueRO.Rotation;

                    var go = _poolManager.Spawn(
                        effectEntry.Prefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        effectEntry.DefaultLifetime
                    );

                    ecb.AddComponent(entity, new EffectGameObjectLink { InstanceId = go.GetInstanceID() });
                }
            }

            // Process tracer entities
            foreach (var (tag, transform, entity) in
                     SystemAPI.Query<RefRO<TracerTag>, RefRO<LocalTransform>>()
                     .WithNone<EffectGameObjectLink>()
                     .WithEntityAccess())
            {
                var effectEntry = _registry.GetTracer(tag.ValueRO.PrefabIndex);
                if (effectEntry?.Prefab != null)
                {
                    var pos = transform.ValueRO.Position;
                    var rot = transform.ValueRO.Rotation;

                    var go = _poolManager.Spawn(
                        effectEntry.Prefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        effectEntry.DefaultLifetime
                    );

                    ecb.AddComponent(entity, new EffectGameObjectLink { InstanceId = go.GetInstanceID() });
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void SpawnMuzzleLight(float3 position)
        {
            // Create temporary point light for muzzle flash
            var lightGO = new GameObject("MuzzleLight");
            lightGO.transform.position = new Vector3(position.x, position.y, position.z);

            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.4f); // Orange/yellow
            light.intensity = 3f;
            light.range = 5f;

            // Auto-destroy after short duration
            Object.Destroy(lightGO, 0.05f);
        }
    }

    /// <summary>
    /// Link component to track spawned GameObjects.
    /// </summary>
    public struct EffectGameObjectLink : IComponentData
    {
        public int InstanceId;
    }

    /// <summary>
    /// EPIC 14.20: Presentation system for impact effects (decals, particles, audio).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ImpactEffectSpawnerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ImpactPresentationSystem : SystemBase
    {
        private EffectPoolManager _poolManager;
        private EffectPrefabRegistry _registry;
        private WeaponAudioManager _audioManager;
        private SurfaceAudioLibrary _surfaceAudioLibrary;

        protected override void OnCreate()
        {
            RequireForUpdate<ImpactParticleTag>();
        }

        protected override void OnStartRunning()
        {
            _poolManager = EffectPoolManager.Instance;
            _registry = EffectPrefabRegistry.Instance;
            _audioManager = WeaponAudioManager.Instance;
            _surfaceAudioLibrary = SurfaceAudioLibrary.Instance;
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

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Process impact particle entities
            foreach (var (tag, transform, entity) in
                     SystemAPI.Query<RefRO<ImpactParticleTag>, RefRO<LocalTransform>>()
                     .WithNone<EffectGameObjectLink>()
                     .WithEntityAccess())
            {
                var surfaceType = (SurfaceMaterialType)tag.ValueRO.SurfaceMaterialId;
                var impactType = tag.ValueRO.ImpactType;

                // Get surface-specific impact effect
                var effectEntry = _registry.GetSurfaceImpactEffect(surfaceType, impactType);
                if (effectEntry?.Prefab != null)
                {
                    var pos = transform.ValueRO.Position;
                    var rot = transform.ValueRO.Rotation;

                    var go = _poolManager.Spawn(
                        effectEntry.Prefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        effectEntry.DefaultLifetime
                    );

                    ecb.AddComponent(entity, new EffectGameObjectLink { InstanceId = go.GetInstanceID() });
                }

                // Play impact sound
                if (_audioManager != null)
                {
                    var pos = transform.ValueRO.Position;
                    _audioManager.PlayImpactSound(surfaceType, new Vector3(pos.x, pos.y, pos.z));
                }
            }

            // Process decal entities
            foreach (var (decalInfo, transform, entity) in
                     SystemAPI.Query<RefRO<ImpactDecalInfo>, RefRO<LocalTransform>>()
                     .WithNone<EffectGameObjectLink>()
                     .WithEntityAccess())
            {
                var surfaceType = (SurfaceMaterialType)decalInfo.ValueRO.SurfaceMaterialId;
                int decalIndex = GetDecalIndexForSurface(surfaceType);

                var effectEntry = _registry.GetDecal(decalIndex);
                if (effectEntry?.Prefab != null)
                {
                    var pos = transform.ValueRO.Position;
                    var rot = transform.ValueRO.Rotation;
                    var scale = transform.ValueRO.Scale;

                    var go = _poolManager.Spawn(
                        effectEntry.Prefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        new Quaternion(rot.value.x, rot.value.y, rot.value.z, rot.value.w),
                        30f // Decals last longer
                    );

                    go.transform.localScale = Vector3.one * scale;
                    ecb.AddComponent(entity, new EffectGameObjectLink { InstanceId = go.GetInstanceID() });
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private int GetDecalIndexForSurface(SurfaceMaterialType surfaceType)
        {
            return surfaceType switch
            {
                SurfaceMaterialType.Metal => 1,    // Metal bullet hole
                SurfaceMaterialType.Wood => 2,     // Wood bullet hole
                SurfaceMaterialType.Flesh => 3,    // Blood splatter
                _ => 0                              // Default bullet hole
            };
        }
    }
}
