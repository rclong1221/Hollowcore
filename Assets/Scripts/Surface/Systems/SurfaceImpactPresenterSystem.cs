using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Audio.Systems;
using DIG.CameraSystem;
using DIG.Surface.Config;
using DIG.Surface.Debug;
using DIG.Core.Settings;
using System.Collections.Generic;

namespace DIG.Surface.Systems
{
    /// <summary>
    /// EPIC 15.24: Unified surface impact presenter.
    /// Drains SurfaceImpactQueue and spawns VFX, decals, and audio via existing managers.
    /// Single consumer for ALL impact events regardless of source (hitscan, projectile, footstep, etc.).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SurfaceImpactPresenterSystem : SystemBase
    {
        private SurfaceMaterialRegistry _registry;
        private VFXManager _vfxManager;
        private DecalManager _decalManager;
        private AudioManager _audioManager;
        private ParadigmSurfaceConfig _paradigmConfig;

        // Phase 2: ImpactClass scaling tables
        private static readonly float[] ParticleScales = { 0.5f, 1.0f, 1.5f, 0.8f, 1.2f, 2.0f, 3.0f, 0.3f, 0.6f, 0.5f };
        private static readonly float[] DecalScales = { 0.05f, 0.1f, 0.15f, 0.1f, 0.2f, 0.8f, 1.5f, 0f, 0.3f, 0.1f };
        private static readonly float[] CameraShakeAmounts = { 0f, 0f, 0.1f, 0f, 0.15f, 0.3f, 0.5f, 0f, 0.05f, 0f };

        // Phase 5: Frame budget
        private const int MaxEventsPerFrame = 32;

        // Phase 5: LOD distance thresholds
        private const float LOD_Full = 15f;
        private const float LOD_Reduced = 40f;
        private const float LOD_Minimal = 60f;

        // Phase 6: Decal clustering — spatial hash of recent decal positions
        private const float ClusterRadius = 0.2f;
        private const int ClusterThreshold = 3;
        private const int MaxClusterEntries = 100;
        private readonly List<float3> _recentDecalPositions = new List<float3>();

        // Phase 6: Wind direction for particle drift
        private static readonly float3 DefaultWind = new float3(1f, 0f, 0.3f);

        // Phase 11: Recent impacts for haptic bridge (cleared each frame)
        public static readonly List<SurfaceImpactData> RecentImpacts = new List<SurfaceImpactData>();

        // Cached camera reference (refreshed once per frame)
        private Camera _cachedCam;
        private float3 _cachedCamPos;

        // Avoid calling FindAnyObjectByType every frame when AudioManager doesn't exist
        private bool _audioManagerSearched;

        protected override void OnUpdate()
        {
            using var _marker = SurfaceFXProfiler.ImpactPresenterMarker.Auto();

            EnsureDependencies();

            // Phase 12: Reset frame counters
            SurfaceFXProfiler.ResetFrameCounters();
            SurfaceFXProfiler.QueueDepthAtFrameStart = SurfaceImpactQueue.Count;

            // Phase 11: Clear recent impacts for haptic bridge
            RecentImpacts.Clear();

            // Cache camera once per frame (avoid repeated Camera.main tag search)
            _cachedCam = Camera.main;
            _cachedCamPos = _cachedCam != null ? (float3)_cachedCam.transform.position : float3.zero;

            // Phase 11: Global motion intensity scaling
            float globalIntensity = MotionIntensitySettings.HasInstance
                ? MotionIntensitySettings.Instance.GlobalIntensity : 1f;

            // Phase 7: Read paradigm-adaptive limits
            var profile = _paradigmConfig != null ? _paradigmConfig.ActiveProfile : null;
            int maxEvents = profile != null ? profile.MaxEventsPerFrame : MaxEventsPerFrame;

            int processed = 0;

            while (SurfaceImpactQueue.TryDequeue(out var impact))
            {
                // Phase 5: Frame budget enforcement (Phase 7: paradigm-adaptive)
                if (processed >= maxEvents) break;

                // Phase 5: LOD computation (Phase 7: paradigm-adaptive thresholds)
                float distToCamera = GetDistanceToCamera(impact.Position);
                impact.LODTier = ComputeLODTier(distToCamera, profile);
                if (impact.LODTier == EffectLODTier.Culled)
                {
                    SurfaceFXProfiler.EventsCulledThisFrame++;
                    continue;
                }

                // Phase 11: Apply global motion intensity
                if (globalIntensity < 0.99f)
                {
                    impact.Intensity *= globalIntensity;
                }

                ProcessImpact(impact, profile);
                RecentImpacts.Add(impact);
                processed++;
            }

            SurfaceFXProfiler.EventsProcessedThisFrame = processed;
        }

        private void ProcessImpact(SurfaceImpactData impact, ParadigmSurfaceProfile profile)
        {
            using var _marker = SurfaceFXProfiler.ProcessImpactMarker.Auto();
            // Resolve surface material for asset lookup
            SurfaceMaterial material = null;
            if (_registry != null)
            {
                _registry.TryGetById(impact.SurfaceMaterialId, out material);
                material ??= _registry.DefaultMaterial;
            }

            // Resolve SurfaceID for surface-specific behavior
            SurfaceID surfaceId = impact.SurfaceId;
            if (surfaceId == SurfaceID.Default && material != null)
            {
                surfaceId = SurfaceIdResolver.FromMaterial(material);
            }

            // Get ImpactClass scaling
            int classIndex = (int)impact.ImpactClass;
            float particleScale = classIndex < ParticleScales.Length ? ParticleScales[classIndex] : 1f;
            float decalScale = classIndex < DecalScales.Length ? DecalScales[classIndex] : 0.1f;
            float shakeAmount = classIndex < CameraShakeAmounts.Length ? CameraShakeAmounts[classIndex] : 0f;

            // Phase 7: Apply paradigm multipliers
            if (profile != null)
            {
                particleScale *= profile.ParticleScaleMultiplier;
                decalScale *= profile.DecalScaleMultiplier;
                shakeAmount *= profile.CameraShakeMultiplier;
            }

            // === VFX ===
            SpawnVFX(impact, material, surfaceId, particleScale);

            // === Decals ===
            SpawnDecal(impact, material, surfaceId, decalScale);

            // === Audio ===
            PlayAudio(impact, material, surfaceId, profile);

            // === Camera Shake (Phase 2, Phase 7: paradigm-scaled) ===
            if (shakeAmount > 0f)
            {
                ApplyCameraShake(impact.Position, shakeAmount, impact.Intensity);
            }

            // === Phase 6: Screen Dirt for large explosions (Phase 7: paradigm-gated) ===
            if (impact.ImpactClass == ImpactClass.Explosion_Large)
            {
                bool screenDirtEnabled = profile == null || profile.ScreenDirtEnabled;
                if (screenDirtEnabled)
                {
                    ScreenDirtTrigger.Set(impact.Position, impact.Intensity);
                }
            }
        }

        private void SpawnVFX(SurfaceImpactData impact, SurfaceMaterial material, SurfaceID surfaceId, float scale)
        {
            if (_vfxManager == null) return;
            if (impact.LODTier == EffectLODTier.Minimal) return; // Billboard only — skip particle VFX

            GameObject prefab = material?.VFXPrefab;
            if (prefab == null) return;

            // Normal-aligned rotation
            Vector3 normal = new Vector3(impact.Normal.x, impact.Normal.y, impact.Normal.z);
            Quaternion rotation = normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(Vector3.forward, normal)
                : Quaternion.identity;

            // Phase 2: Reflection vector for particle direction
            if (math.lengthsq(impact.Velocity) > 0.01f)
            {
                float3 reflected = math.reflect(math.normalize(impact.Velocity), impact.Normal);
                rotation = Quaternion.LookRotation(
                    new Vector3(reflected.x, reflected.y, reflected.z),
                    normal
                );
            }

            var go = _vfxManager.SpawnVFX(prefab,
                new Vector3(impact.Position.x, impact.Position.y, impact.Position.z),
                rotation);
            if (go != null) SurfaceFXProfiler.VFXSpawnedThisFrame++;

            // Phase 2: Apply scale based on ImpactClass
            if (go != null && math.abs(scale - 1f) > 0.01f)
            {
                go.transform.localScale = Vector3.one * scale;
            }

            if (go != null)
            {
                var ps = go.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    // Phase 2: Reduced LOD — halve particle emission
                    if (impact.LODTier == EffectLODTier.Reduced)
                    {
                        var emission = ps.emission;
                        emission.rateOverTimeMultiplier *= 0.5f;
                    }

                    // Phase 6: Wind-affected particles (dust, debris, smoke)
                    // Only affects longer-lived particles (> 0.5s start lifetime)
                    if (ps.main.startLifetime.constant > 0.5f)
                    {
                        var forceModule = ps.forceOverLifetime;
                        forceModule.enabled = true;
                        forceModule.x = new ParticleSystem.MinMaxCurve(DefaultWind.x * 0.5f);
                        forceModule.y = new ParticleSystem.MinMaxCurve(0f);
                        forceModule.z = new ParticleSystem.MinMaxCurve(DefaultWind.z * 0.5f);
                    }
                }
            }
        }

        private void SpawnDecal(SurfaceImpactData impact, SurfaceMaterial material, SurfaceID surfaceId, float decalScale)
        {
            if (_decalManager == null) return;
            if (impact.LODTier >= EffectLODTier.Minimal) return; // Skip decals at minimal+ LOD
            if (impact.ImpactClass == ImpactClass.Footstep) return; // Footprints handled by FootprintDecalSpawnerSystem

            DecalData decalData = material?.ImpactDecal;
            if (decalData == null) return;

            // Normal-aligned rotation (look INTO the surface)
            Vector3 normal = new Vector3(impact.Normal.x, impact.Normal.y, impact.Normal.z);
            Quaternion rotation = normal.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(-normal)
                : Quaternion.identity;

            // Phase 2: Random rotation around normal axis
            float randomAngle = UnityEngine.Random.Range(0f, 360f);
            rotation *= Quaternion.AngleAxis(randomAngle, Vector3.forward);

            // Phase 6: Decal clustering — skip if area is already saturated
            int nearbyCount = CountNearbyDecals(impact.Position);
            if (nearbyCount >= ClusterThreshold) return;

            _decalManager.SpawnDecal(decalData, impact.Position, rotation);
            SurfaceFXProfiler.DecalsSpawnedThisFrame++;
            TrackDecalPosition(impact.Position);
        }

        private void PlayAudio(SurfaceImpactData impact, SurfaceMaterial material, SurfaceID surfaceId,
            ParadigmSurfaceProfile profile = null)
        {
            if (_audioManager == null) return;
            if (impact.LODTier >= EffectLODTier.Culled) return;

            // Phase 4: Water gets special audio
            if (surfaceId == SurfaceID.Water && material != null && material.IsLiquid)
            {
                _audioManager.PlayImpact(impact.SurfaceMaterialId, impact.Position, impact.Intensity * 0.5f);
                return;
            }

            // Phase 6: Audio occlusion — reduce volume if impact is behind a wall
            // Phase 7: Paradigm can disable occlusion (e.g. top-down/2D)
            float volumeScale = 1f;
            bool occlusionEnabled = profile == null || profile.AudioOcclusionEnabled;
            if (occlusionEnabled && impact.LODTier >= EffectLODTier.Reduced)
            {
                volumeScale = IsOccluded(impact.Position) ? 0.3f : 1f;
            }
            _audioManager.PlayImpact(impact.SurfaceMaterialId, impact.Position, impact.Intensity * volumeScale);
        }

        private void ApplyCameraShake(float3 impactPos, float shakeAmount, float intensity)
        {
            if (_cachedCam == null) return;

            float dist = math.distance(impactPos, _cachedCamPos);
            if (dist > 10f) return; // Only shake for nearby impacts

            // Scale shake by distance (stronger when closer)
            float distFactor = 1f - math.saturate(dist / 10f);
            float trauma = shakeAmount * intensity * distFactor;

            if (trauma > 0.01f)
            {
                CameraShakeEffect.TriggerTrauma(trauma);
            }
        }

        private EffectLODTier ComputeLODTier(float distance, ParadigmSurfaceProfile profile = null)
        {
            // Phase 7: Paradigm-adaptive LOD thresholds
            float fullDist = LOD_Full;
            float reducedDist = LOD_Reduced;
            float minimalDist = LOD_Minimal;

            if (profile != null)
            {
                fullDist *= profile.LODFullMultiplier;
                reducedDist *= profile.LODReducedMultiplier;
                minimalDist *= profile.LODMinimalMultiplier * profile.DistanceCullingMultiplier;
            }

            if (distance <= fullDist) return EffectLODTier.Full;
            if (distance <= reducedDist) return EffectLODTier.Reduced;
            if (distance <= minimalDist) return EffectLODTier.Minimal;
            return EffectLODTier.Culled;
        }

        private float GetDistanceToCamera(float3 position)
        {
            if (_cachedCam == null) return 0f;
            return math.distance(position, _cachedCamPos);
        }

        // Phase 6: Decal clustering helpers
        private int CountNearbyDecals(float3 position)
        {
            int count = 0;
            for (int i = 0; i < _recentDecalPositions.Count; i++)
            {
                if (math.distancesq(position, _recentDecalPositions[i]) < ClusterRadius * ClusterRadius)
                    count++;
            }
            return count;
        }

        private void TrackDecalPosition(float3 position)
        {
            if (_recentDecalPositions.Count >= MaxClusterEntries)
                _recentDecalPositions.RemoveAt(0); // Evict oldest
            _recentDecalPositions.Add(position);
        }

        // Phase 6: Audio occlusion — LOS raycast from camera to impact
        private bool IsOccluded(float3 position)
        {
            if (_cachedCam == null) return false;

            Vector3 camPos = _cachedCam.transform.position;
            Vector3 dir = new Vector3(position.x, position.y, position.z) - camPos;
            float dist = dir.magnitude;
            if (dist < 0.1f) return false;

            // Simple Unity raycast for LOS check (not ECS physics — this runs in presentation)
            return UnityEngine.Physics.Raycast(camPos, dir.normalized, dist * 0.95f,
                UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        protected override void OnCreate()
        {
            _registry = Resources.Load<SurfaceMaterialRegistry>("SurfaceMaterialRegistry");
        }

        private void EnsureDependencies()
        {
            if (_vfxManager == null)
                _vfxManager = VFXManager.Instance;
            if (_decalManager == null)
                _decalManager = DecalManager.Instance;
            if (_audioManager == null && !_audioManagerSearched)
            {
                _audioManager = Object.FindAnyObjectByType<AudioManager>();
                _audioManagerSearched = true;
            }
            if (_paradigmConfig == null)
                _paradigmConfig = ParadigmSurfaceConfig.Instance;
        }
    }
}
