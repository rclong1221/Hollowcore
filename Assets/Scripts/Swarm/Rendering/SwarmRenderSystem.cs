using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;

namespace DIG.Swarm.Rendering
{
    /// <summary>
    /// EPIC 16.2 Phase 4: GPU-instanced rendering for swarm particles.
    /// Pipeline:
    ///   1. ToComponentDataArray (cross-world bulk copy)
    ///   2. BuildMatricesJob (Burst, parallel) — computes Matrix4x4 + LOD tier per particle
    ///   3. Main thread partition by LOD tier + DrawMeshInstanced batches
    ///
    /// Managed SystemBase (can't Burst the Draw calls — touches Mesh, Material, Graphics API).
    /// Runs in PresentationSystemGroup, client-side only.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class SwarmRenderSystem : SystemBase
    {
        private const int MAX_INSTANCES_PER_BATCH = 1023;

        // Pre-allocated managed arrays for DrawMeshInstanced (reused each frame)
        private Matrix4x4[] _fullMatrices;
        private Vector4[] _fullAnim;
        private Matrix4x4[] _reducedMatrices;
        private Vector4[] _reducedAnim;
        private Matrix4x4[] _billboardMatrices;
        private Vector4[] _billboardAnim;
        private MaterialPropertyBlock _propBlock;

        // Cross-world particle query
        private EntityQuery _particleQuery;
        private bool _sourceResolved;
        private bool _hasQuery;
        private bool _diagnosticLogged;
        private int _framesSinceResolve;

        protected override void OnCreate()
        {
            _fullMatrices = new Matrix4x4[MAX_INSTANCES_PER_BATCH];
            _fullAnim = new Vector4[MAX_INSTANCES_PER_BATCH];
            _reducedMatrices = new Matrix4x4[MAX_INSTANCES_PER_BATCH];
            _reducedAnim = new Vector4[MAX_INSTANCES_PER_BATCH];
            _billboardMatrices = new Matrix4x4[MAX_INSTANCES_PER_BATCH];
            _billboardAnim = new Vector4[MAX_INSTANCES_PER_BATCH];
            _propBlock = new MaterialPropertyBlock();
        }

        protected override void OnDestroy()
        {
        }

        private void ResolveSourceWorld()
        {
            _sourceResolved = true;
            _framesSinceResolve = 0;

            var localQuery = GetEntityQuery(
                ComponentType.ReadOnly<SwarmParticle>(),
                ComponentType.ReadOnly<SwarmAnimState>()
            );

            if (localQuery.CalculateEntityCount() > 0)
            {
                _particleQuery = localQuery;
                _hasQuery = true;
                Debug.Log($"[Swarm Render] Found {localQuery.CalculateEntityCount()} particles in {World.Name}");
                return;
            }

            EntityQuery bestQuery = default;
            string bestWorldName = null;
            int bestCount = 0;

            foreach (var world in World.All)
            {
                if (!world.IsCreated || world == World) continue;

                var query = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<SwarmParticle>(),
                    ComponentType.ReadOnly<SwarmAnimState>()
                );

                int count = query.CalculateEntityCount();
                if (count > bestCount)
                {
                    bestCount = count;
                    bestQuery = query;
                    bestWorldName = world.Name;
                }

                if (bestWorldName == null && world.Name.Contains("Server"))
                {
                    bestQuery = query;
                    bestWorldName = world.Name;
                }
            }

            if (bestWorldName != null)
            {
                _particleQuery = bestQuery;
                _hasQuery = true;
                Debug.Log(bestCount > 0
                    ? $"[Swarm Render] Found {bestCount} particles in {bestWorldName} (cross-world)"
                    : $"[Swarm Render] Using {bestWorldName} (no particles yet — spawner may still be starting)");
                return;
            }

            _particleQuery = localQuery;
            _hasQuery = true;
            Debug.Log($"[Swarm Render] No other worlds found, using {World.Name}");
        }

        protected override void OnUpdate()
        {
            var renderConfig = SwarmRenderConfigManaged.Instance;

            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                if (renderConfig == null)
                    Debug.LogWarning("[Swarm Render] SwarmRenderConfigManaged.Instance is null — add SwarmRenderConfig to scene");
                else if (renderConfig.FullMesh == null)
                    Debug.LogWarning("[Swarm Render] SwarmRenderConfigManaged.FullMesh is null — assign a mesh");
                else if (renderConfig.SwarmMaterial == null)
                    Debug.LogWarning("[Swarm Render] SwarmRenderConfigManaged.SwarmMaterial is null — assign a material");
                else
                    Debug.Log($"[Swarm Render] Config OK: mesh={renderConfig.FullMesh.name}, mat={renderConfig.SwarmMaterial.name}");
            }

            if (renderConfig == null || renderConfig.FullMesh == null || renderConfig.SwarmMaterial == null)
                return;

            var cam = Camera.main;
            if (cam == null) return;

            if (!_sourceResolved)
                ResolveSourceWorld();

            _framesSinceResolve++;
            if (_framesSinceResolve > 120 && _hasQuery && _particleQuery.CalculateEntityCount() == 0)
            {
                _sourceResolved = false;
                _hasQuery = false;
            }

            if (!_hasQuery)
                return;

            using (SwarmProfilerMarkers.RenderBuild.Auto())
            {
                var particles = _particleQuery.ToComponentDataArray<SwarmParticle>(Allocator.TempJob);
                var animStates = _particleQuery.ToComponentDataArray<SwarmAnimState>(Allocator.TempJob);

                int count = particles.Length;
                if (count == 0)
                {
                    particles.Dispose();
                    animStates.Dispose();
                    return;
                }

                float3 camPos = cam.transform.position;
                float maxDistSq = renderConfig.MaxRenderDistance * renderConfig.MaxRenderDistance;
                float lod1Sq = renderConfig.LODDistance1 * renderConfig.LODDistance1;
                float lod2Sq = renderConfig.LODDistance2 * renderConfig.LODDistance2;

                // Precompute clip start frames for Burst job
                int clipCount = renderConfig.ClipFrameCounts != null ? renderConfig.ClipFrameCounts.Length : 0;
                var clipStarts = new NativeArray<int>(clipCount, Allocator.TempJob);
                var clipCounts = new NativeArray<int>(clipCount, Allocator.TempJob);
                int startFrame = 0;
                for (int i = 0; i < clipCount; i++)
                {
                    clipStarts[i] = startFrame;
                    clipCounts[i] = renderConfig.ClipFrameCounts[i];
                    startFrame += renderConfig.ClipFrameCounts[i];
                }

                // Burst job output arrays
                var matrices = new NativeArray<Matrix4x4>(count, Allocator.TempJob);
                var animParams = new NativeArray<Vector4>(count, Allocator.TempJob);
                var lodTiers = new NativeArray<byte>(count, Allocator.TempJob);

                // Phase 1: Build matrices + LOD tiers (Burst, parallel)
                new BuildMatricesJob
                {
                    Particles = particles,
                    AnimStates = animStates,
                    Matrices = matrices,
                    AnimParams = animParams,
                    LodTiers = lodTiers,
                    CameraPosition = camPos,
                    MaxDistSq = maxDistSq,
                    Lod1Sq = lod1Sq,
                    Lod2Sq = lod2Sq,
                    ClipStarts = clipStarts,
                    ClipCounts = clipCounts,
                    ClipArrayLength = clipCount,
                }.Schedule(count, 64).Complete();

                particles.Dispose();
                animStates.Dispose();
                clipStarts.Dispose();
                clipCounts.Dispose();

                // Phase 2: Partition by LOD tier and draw batches (main thread — Graphics API)
                using (SwarmProfilerMarkers.RenderDraw.Auto())
                {
                    int fullCount = 0, reducedCount = 0, billboardCount = 0;

                    for (int i = 0; i < count; i++)
                    {
                        byte lod = lodTiers[i];
                        if (lod == 3) continue; // culled

                        if (lod == 0)
                        {
                            _fullMatrices[fullCount] = matrices[i];
                            _fullAnim[fullCount] = animParams[i];
                            fullCount++;
                            if (fullCount >= MAX_INSTANCES_PER_BATCH)
                            {
                                FlushBatch(renderConfig.FullMesh, renderConfig.SwarmMaterial,
                                    _fullMatrices, _fullAnim, fullCount, renderConfig.CastShadows);
                                fullCount = 0;
                            }
                        }
                        else if (lod == 1)
                        {
                            _reducedMatrices[reducedCount] = matrices[i];
                            _reducedAnim[reducedCount] = animParams[i];
                            reducedCount++;
                            if (reducedCount >= MAX_INSTANCES_PER_BATCH)
                            {
                                FlushBatch(renderConfig.ReducedMesh ?? renderConfig.FullMesh,
                                    renderConfig.SwarmMaterial,
                                    _reducedMatrices, _reducedAnim, reducedCount, false);
                                reducedCount = 0;
                            }
                        }
                        else // lod == 2
                        {
                            _billboardMatrices[billboardCount] = matrices[i];
                            _billboardAnim[billboardCount] = animParams[i];
                            billboardCount++;
                            if (billboardCount >= MAX_INSTANCES_PER_BATCH)
                            {
                                FlushBatch(renderConfig.BillboardMesh ?? renderConfig.FullMesh,
                                    renderConfig.BillboardMaterial ?? renderConfig.SwarmMaterial,
                                    _billboardMatrices, _billboardAnim, billboardCount, false);
                                billboardCount = 0;
                            }
                        }
                    }

                    // Flush remaining
                    if (fullCount > 0)
                        FlushBatch(renderConfig.FullMesh, renderConfig.SwarmMaterial,
                            _fullMatrices, _fullAnim, fullCount, renderConfig.CastShadows);
                    if (reducedCount > 0)
                        FlushBatch(renderConfig.ReducedMesh ?? renderConfig.FullMesh,
                            renderConfig.SwarmMaterial,
                            _reducedMatrices, _reducedAnim, reducedCount, false);
                    if (billboardCount > 0)
                        FlushBatch(renderConfig.BillboardMesh ?? renderConfig.FullMesh,
                            renderConfig.BillboardMaterial ?? renderConfig.SwarmMaterial,
                            _billboardMatrices, _billboardAnim, billboardCount, false);
                }

                matrices.Dispose();
                animParams.Dispose();
                lodTiers.Dispose();
            }
        }

        private void FlushBatch(Mesh mesh, Material material,
            Matrix4x4[] batchMatrices, Vector4[] batchAnimParams, int batchCount, bool shadows)
        {
            _propBlock.Clear();
            _propBlock.SetVectorArray("_SwarmAnimParams", batchAnimParams);

            Graphics.DrawMeshInstanced(
                mesh, 0, material, batchMatrices, batchCount, _propBlock,
                shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
                true, 0, null
            );
        }

        /// <summary>
        /// Burst-compiled parallel matrix + LOD computation.
        /// Computes rotation from velocity, builds Matrix4x4, determines LOD tier, packs anim params.
        /// </summary>
        [BurstCompile]
        struct BuildMatricesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<SwarmParticle> Particles;
            [ReadOnly] public NativeArray<SwarmAnimState> AnimStates;
            [WriteOnly] public NativeArray<Matrix4x4> Matrices;
            [WriteOnly] public NativeArray<Vector4> AnimParams;
            [WriteOnly] public NativeArray<byte> LodTiers;
            public float3 CameraPosition;
            public float MaxDistSq;
            public float Lod1Sq;
            public float Lod2Sq;
            [ReadOnly] public NativeArray<int> ClipStarts;
            [ReadOnly] public NativeArray<int> ClipCounts;
            public int ClipArrayLength;

            public void Execute(int i)
            {
                float3 pos = Particles[i].Position;
                float distSq = math.distancesq(pos, CameraPosition);

                if (distSq > MaxDistSq)
                {
                    LodTiers[i] = 3; // culled
                    return;
                }

                // Build rotation from velocity
                float3 velocity = Particles[i].Velocity;
                float3 forward = math.normalizesafe(new float3(velocity.x, 0f, velocity.z));
                if (math.lengthsq(forward) < 0.001f)
                    forward = new float3(0f, 0f, 1f);

                quaternion rot = quaternion.LookRotationSafe(forward, math.up());

                // Build matrix using float4x4 (Burst-optimized SIMD)
                float4x4 mat = float4x4.TRS(pos, rot, new float3(1f, 1f, 1f));
                Matrices[i] = new Matrix4x4(
                    new Vector4(mat.c0.x, mat.c0.y, mat.c0.z, mat.c0.w),
                    new Vector4(mat.c1.x, mat.c1.y, mat.c1.z, mat.c1.w),
                    new Vector4(mat.c2.x, mat.c2.y, mat.c2.z, mat.c2.w),
                    new Vector4(mat.c3.x, mat.c3.y, mat.c3.z, mat.c3.w)
                );

                // LOD tier
                LodTiers[i] = distSq < Lod1Sq ? (byte)0 : distSq < Lod2Sq ? (byte)1 : (byte)2;

                // Anim params
                int clipIdx = AnimStates[i].AnimClipIndex;
                int sf = 0, fc = 1;
                if (clipIdx >= 0 && clipIdx < ClipArrayLength)
                {
                    sf = ClipStarts[clipIdx];
                    fc = ClipCounts[clipIdx];
                }
                AnimParams[i] = new Vector4(clipIdx, AnimStates[i].AnimTime, sf, fc);
            }
        }
    }
}
