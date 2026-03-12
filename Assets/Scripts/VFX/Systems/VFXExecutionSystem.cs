using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using UnityEngine;
using Audio.Systems;

namespace DIG.VFX.Systems
{
    /// <summary>
    /// EPIC 16.7: Managed execution system that consumes VFXRequest entities,
    /// resolves prefab via VFXTypeDatabase, and delegates to VFXManager for pooled spawning.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class VFXExecutionSystem : SystemBase
    {
        private VFXTypeDatabase _database;
        private EntityQuery _requestQuery;
        static readonly ProfilerMarker k_Marker = new("VFXExecutionSystem.Execute");

        protected override void OnCreate()
        {
            _requestQuery = GetEntityQuery(
                ComponentType.ReadOnly<VFXRequest>(),
                ComponentType.ReadOnly<VFXResolvedLOD>(),
                ComponentType.ReadOnly<VFXCulled>()
            );
            // We filter by VFXCulled disabled in the loop, not in query

            _database = Resources.Load<VFXTypeDatabase>("VFXTypeDatabase");
        }

        protected override void OnUpdate()
        {
            k_Marker.Begin();

            VFXTelemetry.ResetFrame();

            var vfxManager = VFXManager.Instance;
            if (vfxManager == null || _database == null)
            {
                k_Marker.End();
                return;
            }

            var entities = _requestQuery.ToEntityArray(Allocator.Temp);
            var requests = _requestQuery.ToComponentDataArray<VFXRequest>(Allocator.Temp);
            var lods = _requestQuery.ToComponentDataArray<VFXResolvedLOD>(Allocator.Temp);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var request = requests[i];
                var lod = lods[i];
                int cat = (int)request.Category;

                VFXTelemetry.RequestedThisFrame++;
                if (cat >= 0 && cat < 7)
                    VFXTelemetry.RequestedPerCategory[cat]++;

                // Skip culled
                if (EntityManager.IsComponentEnabled<VFXCulled>(entity))
                {
                    if (cat >= 0 && cat < 7)
                        VFXTelemetry.CulledPerCategory[cat]++;
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Resolve VFX type
                if (!_database.TryGetEntry(request.VFXTypeId, out var entry))
                {
                    // Unknown type — destroy silently
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Phase 4: LOD prefab variant selection
                GameObject prefab = SelectPrefab(entry, lod.Tier);
                if (prefab == null)
                {
                    // No prefab for this LOD tier — skip
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // Spawn via VFXManager (bypasses its throttle — we handle budgeting)
                var pos = new Vector3(request.Position.x, request.Position.y, request.Position.z);
                var rot = new Quaternion(request.Rotation.value.x, request.Rotation.value.y,
                                        request.Rotation.value.z, request.Rotation.value.w);
                var go = vfxManager.SpawnVFX(prefab, pos, rot, bypassThrottle: true);

                if (go != null)
                {
                    // Apply parameterization
                    ApplyParameters(go, request, lod.Tier);

                    VFXTelemetry.ExecutedThisFrame++;
                    VFXTelemetry.PoolHitsThisFrame++;
                    if (cat >= 0 && cat < 7)
                        VFXTelemetry.ExecutedPerCategory[cat]++;
                    if ((int)lod.Tier < 4)
                        VFXTelemetry.ExecutedPerLODTier[(int)lod.Tier]++;
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();

            entities.Dispose();
            requests.Dispose();
            lods.Dispose();

            // Session totals
            VFXTelemetry.TotalRequested += VFXTelemetry.RequestedThisFrame;
            VFXTelemetry.TotalExecuted += VFXTelemetry.ExecutedThisFrame;
            VFXTelemetry.TotalCulled += VFXTelemetry.CulledByBudgetThisFrame + VFXTelemetry.CulledByLODThisFrame;

            k_Marker.End();
        }

        private static GameObject SelectPrefab(VFXTypeEntry entry, DIG.Surface.EffectLODTier tier)
        {
            switch (tier)
            {
                case DIG.Surface.EffectLODTier.Full:
                    return entry.Prefab;
                case DIG.Surface.EffectLODTier.Reduced:
                    return entry.ReducedPrefab != null ? entry.ReducedPrefab : entry.Prefab;
                case DIG.Surface.EffectLODTier.Minimal:
                    return entry.MinimalPrefab; // null = skip at minimal
                default:
                    return null;
            }
        }

        private static void ApplyParameters(GameObject go, VFXRequest request, DIG.Surface.EffectLODTier tier)
        {
            // Scale
            if (request.Scale > 0f && request.Scale != 1f)
                go.transform.localScale = Vector3.one * request.Scale;

            var ps = go.GetComponentInChildren<ParticleSystem>();
            if (ps == null) return;

            var main = ps.main;

            // Color tint (non-zero = override)
            if (request.ColorTint.x > 0f || request.ColorTint.y > 0f ||
                request.ColorTint.z > 0f || request.ColorTint.w > 0f)
            {
                main.startColor = new Color(request.ColorTint.x, request.ColorTint.y,
                                            request.ColorTint.z, request.ColorTint.w);
            }

            // Intensity modulates emission rate
            if (request.Intensity > 0f && request.Intensity < 1f)
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier *= request.Intensity;
            }

            // Phase 4: Reduced LOD emission halving (when no ReducedPrefab)
            if (tier == DIG.Surface.EffectLODTier.Reduced)
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier *= 0.5f;

                // Stop child particle systems (sub-emitters) at Reduced tier
                var allPs = go.GetComponentsInChildren<ParticleSystem>();
                for (int j = 1; j < allPs.Length; j++) // skip index 0 (root)
                    allPs[j].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // Duration override
            if (request.Duration > 0f)
            {
                main.duration = request.Duration;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
            }
        }
    }
}
