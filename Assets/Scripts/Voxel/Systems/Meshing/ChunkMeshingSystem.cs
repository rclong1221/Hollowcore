using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using Unity.Rendering;
using Unity.Transforms;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Meshing;
using DIG.Voxel.Systems;
using DIG.Voxel.Jobs;
using DIG.Voxel.Debug;
using Collider = Unity.Physics.Collider;
using MeshCollider = Unity.Physics.MeshCollider;

namespace DIG.Voxel.Systems.Meshing
{
    /// <summary>
    /// Generates visual and physics meshes for chunks using Marching Cubes.
    /// MVP Implementation - Synchronous, will be optimized in Epic 9.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class ChunkMeshingSystem : SystemBase
    {
        private const byte ISO_LEVEL = 127;
        private const bool USE_SMOOTH_NORMALS = true;
        private const int PADDED_SIZE = 34;
        private const int PADDED_VOLUME = PADDED_SIZE * PADDED_SIZE * PADDED_SIZE;
        private const float FRUSTUM_PADDING = 32f; // Extra padding for chunks at edge of view
        
        // OPTIMIZATION 10.9.6: Adaptive Mesh Concurrency
        private const int MIN_CONCURRENT_MESHES = 8;   // Minimum when frame time is high
        private const int MAX_CONCURRENT_MESHES = 20;  // Maximum when frame time is low
        private const float FRAME_BUDGET_MS = 8.0f;    // Target frame budget for meshing
        private const float HIGH_FRAME_TIME_THRESHOLD = 16.0f; // Reduce concurrency above this
        private const float LOW_FRAME_TIME_THRESHOLD = 8.0f;   // Can increase concurrency below this
        
        // OPTIMIZATION 10.13.3: Shadow Distance Culling
        // Chunks beyond this distance will not cast shadows (squared for performance)
        private const float SHADOW_DISTANCE = 60f;  // Meters - adjust based on URP shadow distance
        private const float SHADOW_DISTANCE_SQ = SHADOW_DISTANCE * SHADOW_DISTANCE;
        
        // OPTIMIZATION 10.9.14: Job Scheduling Spread
        private const int MAX_MESH_COMPLETIONS_PER_FRAME = 4; // Limit mesh finalizations per frame
        
        // NOTE: Collider creation is now handled by ChunkColliderBuildSystem (10.11.2)
        // MAX_COLLIDER_CREATES_PER_FRAME moved there
        
        // Adaptive concurrency tracking
        private int _currentMaxConcurrency = 12;  // Start at mid-point
        private float _frameTimeAccumulator = 0f;
        private int _frameTimeCount = 0;
        private const int FRAME_SAMPLE_COUNT = 10; // Average over 10 frames
        
        // LOD configuration
        private Rendering.VoxelLODConfig _lodConfig;
        
        private UnityEngine.Material _cachedMaterial;
        
        // Debug toggle - set true to enable verbose logging
        public static bool EnableDebugLogs { get; set; } = false;
        
        // DIAGNOSTIC 10.13: Rendering stats logging (throttled)
        public static bool EnableRenderingStatsLogs { get; set; } = false;  // Set true to enable diagnostic logs
        private float _lastStatsLogTime = 0f;
        private const float STATS_LOG_INTERVAL = 5.0f;  // Log every 5 seconds
        
        // Visibility culling
        private UnityEngine.Plane[] _frustumPlanes = new UnityEngine.Plane[6];
        private float3 _cachedCameraPos;
        
        // Pending mesh generation jobs
        // OPTIMIZATION 10.9.2: Updated for parallel job pipeline
        private struct PendingMeshJob
        {
            public Entity Entity;
            public int3 ChunkPos;
            // Phase 0: Parallel MC job running
            // Phase 1: Merge job running  
            // Phase 2: SmoothNormals running
            public int Phase;
            
            public JobHandle MarchingCubesHandle;  // Phase 0: parallel + merge combined
            public JobHandle SmoothNormalsHandle;  // Phase 2
            
            // Buffers (Persistent)
            public NativeArray<byte> PaddedDensities;
            public NativeArray<byte> PaddedMaterials;
            public NativeList<float3> Vertices;
            public NativeList<float3> Normals;
            public NativeList<Color32> Colors;
            public NativeList<int> Indices;
            public NativeList<int3> Triangles;
            
            // OPTIMIZATION 10.9.2: NativeStream for parallel output
            public NativeStream OutputStream;
            public int TotalCubes;  // For merge job ForEachCount
            
            // Created in Phase 2 for Normals job
            public NativeList<float3> SmoothNormals;
        }
        
        private NativeList<PendingMeshJob> _pendingMeshJobs;
        private EntityQuery _migrationQuery;
        
        // Synchronous now - no buffers needed
        
        protected override void OnCreate()
        {
            NativeCollectionPool.RegisterUser();
            _pendingMeshJobs = new NativeList<PendingMeshJob>(MAX_CONCURRENT_MESHES, Allocator.Persistent);
            
            // V1.5 MIGRATION QUERY: Cached to avoid recreation/disposal issues
            // Include Disabled/Prefab to catch ALL stale entities
            _migrationQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<ChunkPosition>(), ComponentType.ReadWrite<MaterialMeshInfo>() },
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            });
            RequireForUpdate<ChunkMeshState>();
            RequireForUpdate<VoxelWorldEnabled>();
            
            // Load LOD config
            _lodConfig = Resources.Load<Rendering.VoxelLODConfig>("VoxelLODConfig");
            
            InitializeMaterial();
        }
        
        protected override void OnDestroy()
        {
            // ... (rest of OnDestroy)
            // Complete and dispose all pending jobs
            for (int i = 0; i < _pendingMeshJobs.Length; i++)
            {
                var job = _pendingMeshJobs[i];
                job.MarchingCubesHandle.Complete();
                job.SmoothNormalsHandle.Complete();
                DisposeJobData(job);
            }
            _pendingMeshJobs.Dispose();
            NativeCollectionPool.UnregisterUser();
            
            // CRITICAL: Dispose static pool container to prevent leak
            ChunkMeshPool.Clear();
        }
        
        protected override void OnUpdate()
        {
            VoxelProfiler.BeginSample("MeshSystem");
            
            // OPTIMIZATION 10.9.6: Update adaptive concurrency based on frame time
            UpdateAdaptiveConcurrency();
            
            // ----------------------------------------------------------------------------------
            // V1.5 MIGRATION CLEANUP: Aggressively strip ECS Rendering components
            // This ensures no stale entities try to render via BatchRendererGroup (which fails with current shader)
            // ----------------------------------------------------------------------------------
            if (!_migrationQuery.IsEmptyIgnoreFilter)
            {
                using var staleEntities = _migrationQuery.ToEntityArray(Allocator.Temp);
                foreach (var entity in staleEntities)
                {
                    EntityManager.RemoveComponent<MaterialMeshInfo>(entity);
                    if (EntityManager.HasComponent<RenderBounds>(entity)) EntityManager.RemoveComponent<RenderBounds>(entity);
                    if (EntityManager.HasComponent<RenderMeshArray>(entity)) EntityManager.RemoveComponent<RenderMeshArray>(entity);
                }
            }

            // 1. Process Finished Jobs
            ProcessPendingJobs();
            
            // DIAGNOSTIC 10.13: Log rendering stats periodically
            LogRenderingStats();
            
            // OPTIMIZATION 10.13.3: Dynamically update shadow states for existing chunks
            UpdateChunkShadowStates();
            
            // OPTIMIZATION 10.9.5: Use cached camera data instead of Camera.main
            if (!SystemAPI.HasSingleton<CameraData>())
            {
                VoxelProfiler.EndSample("MeshSystem");
                return;
            }
            var cameraData = SystemAPI.GetSingleton<CameraData>();
            if (!cameraData.IsValid)
            {
                VoxelProfiler.EndSample("MeshSystem");
                return;
            }
            _cachedCameraPos = cameraData.Position;
            
            // 3. Schedule new jobs if slots available (using adaptive concurrency)
            int availableSlots = _currentMaxConcurrency - _pendingMeshJobs.Length;
            
            // OPTIMIZATION 10.9.17: Check frame budget before scheduling
            bool hasBudget = FrameBudgetSystem.Instance == null || 
                             FrameBudgetSystem.Instance.HasBudget(1.0f);
            
            if (availableSlots > 0 && hasBudget)
            {
                // Collect and sort chunks by distance (priority queue)
                using var candidateChunks = new NativeList<ChunkMeshCandidate>(Allocator.Temp);
                
                foreach (var (chunkPos, voxelData, meshState, entity) in 
                    SystemAPI.Query<RefRO<ChunkPosition>, RefRO<ChunkVoxelData>, RefRW<ChunkMeshState>>()
                    .WithAll<ChunkNeedsRemesh>()
                    .WithEntityAccess())
                {
                    if (!voxelData.ValueRO.IsValid) continue;
                    
                    // Check if already processing
                    bool alreadyProcessing = false;
                    for (int i = 0; i < _pendingMeshJobs.Length; i++)
                    {
                        if (_pendingMeshJobs[i].Entity == entity) { alreadyProcessing = true; break; }
                    }
                    if (alreadyProcessing) continue;
                    
                    // Phase 1: Visibility culling - skip chunks outside frustum
                    // OPTIMIZATION 10.9.5: Use cached frustum planes
                    float3 worldPos = CoordinateUtils.ChunkToWorldPos(chunkPos.ValueRO.Value);
                    float3 chunkCenter = worldPos + new float3(VoxelConstants.CHUNK_SIZE * 0.5f);
                    float3 chunkExtents = new float3(VoxelConstants.CHUNK_SIZE * 0.5f + FRUSTUM_PADDING * 0.5f);
                    
                    if (!cameraData.Frustum.TestAABB(chunkCenter, chunkExtents))
                    {
                        // Chunk is outside frustum - defer but don't skip entirely
                        continue;
                    }
                    
                    // Phase 1b: Occlusion - skip chunks buried underground
                    // If the chunk's TOP is below camera AND chunk is more than 1 chunk distance below,
                    // it's likely a cave hidden by solid terrain
                    float chunkTopY = worldPos.y + VoxelConstants.CHUNK_SIZE;
                    float cameraY = _cachedCameraPos.y;
                    float depthBelowCamera = cameraY - chunkTopY;
                    
                    // Skip chunks that are significantly below camera (buried caves)
                    // Only mesh if: within 2 chunks vertically, or camera is underground too
                    if (depthBelowCamera > VoxelConstants.CHUNK_SIZE * 2)
                    {
                        // This chunk is buried deep - extremely low priority or skip
                        // OPTIMIZATION 10.9.5: Use cached camera forward
                        float3 cameraForward = cameraData.Forward;
                        
                        // If camera is looking mostly horizontal or up, skip underground chunks
                        if (cameraForward.y > -0.5f) // Not looking steeply down
                        {
                            continue; // Skip this buried chunk
                        }
                    }
                    
                    // Calculate distance for priority
                    float distSq = math.distancesq(_cachedCameraPos, chunkCenter);
                    
                    // Get LOD-based voxel step
                    int voxelStep = 1;
                    if (SystemAPI.HasComponent<ChunkLODState>(entity))
                    {
                        var lodState = SystemAPI.GetComponent<ChunkLODState>(entity);
                        if (_lodConfig != null)
                        {
                            voxelStep = _lodConfig.GetVoxelStep(lodState.CurrentLOD);
                        }
                    }
                    
                    candidateChunks.Add(new ChunkMeshCandidate
                    {
                        Entity = entity,
                        ChunkPos = chunkPos.ValueRO.Value,
                        DistanceSq = distSq,
                        VoxelData = voxelData.ValueRO.Data,
                        VoxelStep = voxelStep
                    });
                }
                
                // Phase 3: Sort by distance (nearest first)
                candidateChunks.Sort(new ChunkDistanceComparer());
                
                // Process up to availableSlots
                int processed = 0;
                for (int i = 0; i < candidateChunks.Length && processed < availableSlots; i++)
                {
                    var candidate = candidateChunks[i];
                    
                    // Safety check: entity may have been destroyed by another system
                    if (!EntityManager.Exists(candidate.Entity))
                        continue;
                    
                    // Start async job with LOD step
                    StartAsyncMeshJob(candidate.Entity, candidate.ChunkPos, candidate.VoxelData, candidate.VoxelStep);
                    processed++;
                    
                    // Unmark dirty
                    if (SystemAPI.HasComponent<ChunkMeshState>(candidate.Entity))
                    {
                        var meshState = SystemAPI.GetComponentRW<ChunkMeshState>(candidate.Entity);
                        meshState.ValueRW.IsDirty = false;
                    }
                    
                    // BUGFIX: Use direct EntityManager access instead of ECB to avoid
                    // race condition where entity is destroyed before ECB playback
                    if (EntityManager.HasComponent<ChunkNeedsRemesh>(candidate.Entity))
                    {
                        EntityManager.SetComponentEnabled<ChunkNeedsRemesh>(candidate.Entity, false);
                    }
                }
            }
            
            VoxelProfiler.EndSample("MeshSystem");
        }
        
        // Helper struct for priority sorting
        private struct ChunkMeshCandidate
        {
            public Entity Entity;
            public int3 ChunkPos;
            public float DistanceSq;
            public BlobAssetReference<VoxelBlob> VoxelData;
            public int VoxelStep; // LOD-based step size
        }
        
        private struct ChunkDistanceComparer : IComparer<ChunkMeshCandidate>
        {
            public int Compare(ChunkMeshCandidate x, ChunkMeshCandidate y)
            {
                return x.DistanceSq.CompareTo(y.DistanceSq);
            }
        }
        
        /// <summary>
        /// OPTIMIZATION 10.9.6: Dynamically adjust mesh job concurrency based on frame time.
        /// Increases concurrency when frame time is low, decreases when high.
        /// </summary>
        private void UpdateAdaptiveConcurrency()
        {
            // Accumulate frame time samples
            float deltaMs = UnityEngine.Time.deltaTime * 1000f;
            _frameTimeAccumulator += deltaMs;
            _frameTimeCount++;
            
            // Only adjust every FRAME_SAMPLE_COUNT frames
            if (_frameTimeCount >= FRAME_SAMPLE_COUNT)
            {
                float avgFrameTime = _frameTimeAccumulator / _frameTimeCount;
                
                // Adjust concurrency based on average frame time
                if (avgFrameTime > HIGH_FRAME_TIME_THRESHOLD)
                {
                    // Frame time is high - reduce concurrency
                    _currentMaxConcurrency = math.max(MIN_CONCURRENT_MESHES, _currentMaxConcurrency - 2);
                }
                else if (avgFrameTime < LOW_FRAME_TIME_THRESHOLD)
                {
                    // Frame time is low - can increase concurrency
                    _currentMaxConcurrency = math.min(MAX_CONCURRENT_MESHES, _currentMaxConcurrency + 1);
                }
                // else: frame time is in acceptable range, keep current concurrency
                
                // Reset accumulators
                _frameTimeAccumulator = 0f;
                _frameTimeCount = 0;
            }
        }
        
        private void ProcessPendingJobs()
        {
            // OPTIMIZATION 10.9.14: Limit completions per frame to prevent spikes
            int meshCompletionsThisFrame = 0;
            
             for (int i = _pendingMeshJobs.Length - 1; i >= 0; i--)
             {
                 var job = _pendingMeshJobs[i];
                 
                 if (job.Phase == 0) // Check Marching Cubes
                 {
                     if (job.MarchingCubesHandle.IsCompleted)
                     {
                         job.MarchingCubesHandle.Complete();
                         
                         // FIX: Dispose NativeStream immediately - it's only needed for merge job
                         // TempJob allocations must be disposed within 4 frames
                         if (job.OutputStream.IsCreated) 
                         {
                             job.OutputStream.Dispose();
                             job.OutputStream = default;
                             _pendingMeshJobs[i] = job; // FIX: Update struct in list after dispose
                         }
                         
                         // Check if we produced vertices
                         if (job.Vertices.Length > 0 && USE_SMOOTH_NORMALS)
                         {
                             // Schedule Smooth Normals (Phase 1)
                             job.SmoothNormals = NativeCollectionPool.GetList<float3>(job.Vertices.Length);
                             job.SmoothNormals.Resize(job.Vertices.Length, NativeArrayOptions.UninitializedMemory);
                             
                             var smoothJob = new CalculateSmoothNormalsJob
                             {
                                 Densities = job.PaddedDensities,
                                 Vertices = job.Vertices.AsArray(),
                                 SmoothNormals = job.SmoothNormals.AsArray(),
                                 IsoLevel = ISO_LEVEL
                             };
                             
                             job.SmoothNormalsHandle = smoothJob.Schedule(job.Vertices.Length, 64);
                             job.Phase = 1; 
                             _pendingMeshJobs[i] = job; // Update struct in list
                         }
                         else
                         {
                             // No vertices or Skip Smooth Normals -> Finalize now
                             // OPTIMIZATION 10.9.14: Check completion limit
                             if (meshCompletionsThisFrame >= MAX_MESH_COMPLETIONS_PER_FRAME)
                                 continue;
                             
                             // OPTIMIZATION 10.11.2: Collider creation is now deferred
                             FinalizeMesh(job);
                             
                             DisposeJobData(job);
                             _pendingMeshJobs.RemoveAtSwapBack(i);
                             meshCompletionsThisFrame++;
                         }
                     }
                 }
                 else if (job.Phase == 1) // Check Smooth Normals
                 {
                     if (job.SmoothNormalsHandle.IsCompleted)
                     {
                         // OPTIMIZATION 10.9.14: Check completion limit
                         if (meshCompletionsThisFrame >= MAX_MESH_COMPLETIONS_PER_FRAME)
                             continue;
                         
                         job.SmoothNormalsHandle.Complete();
                         
                         // Apply Smooth Normals
                         job.Normals.Resize(job.Vertices.Length, NativeArrayOptions.UninitializedMemory);
                         NativeArray<float3>.Copy(job.SmoothNormals.AsArray(), job.Normals.AsArray(), job.Vertices.Length);
                         
                         // OPTIMIZATION 10.11.2: Collider creation is now deferred
                         FinalizeMesh(job);
                         
                         DisposeJobData(job);
                         _pendingMeshJobs.RemoveAtSwapBack(i);
                         meshCompletionsThisFrame++;
                     }
                 }
             }
        }
        
        private void StartAsyncMeshJob(Entity entity, int3 chunkPos, BlobAssetReference<VoxelBlob> blob, int voxelStep = 1)
        {
            // Allocate persistent buffers (Pooled)
            var paddedDensities = NativeCollectionPool.GetArray<byte>(PADDED_VOLUME);
            var paddedMaterials = NativeCollectionPool.GetArray<byte>(PADDED_VOLUME);
            
            // Get neighbors (Main Thread, fast component lookup)
            var neighbors = GetNeighborBlobs(entity);
            
            // Schedule CopyPaddedDataJob (Burst)
            var copyJob = new CopyPaddedDataJob
            {
                PaddedDensities = paddedDensities,
                PaddedMaterials = paddedMaterials,
                Source = blob,
                HasPosX = neighbors.HasPosX, PosX = neighbors.PosX,
                HasNegX = neighbors.HasNegX, NegX = neighbors.NegX,
                HasPosY = neighbors.HasPosY, PosY = neighbors.PosY,
                HasNegY = neighbors.HasNegY, NegY = neighbors.NegY,
                HasPosZ = neighbors.HasPosZ, PosZ = neighbors.PosZ,
                HasNegZ = neighbors.HasNegZ, NegZ = neighbors.NegZ
            };
            var copyHandle = copyJob.Schedule();
            
            // Allocate output buffers (Pooled)
            var vertices = NativeCollectionPool.GetList<float3>(4096);
            var normals = NativeCollectionPool.GetList<float3>(4096);
            var colors = NativeCollectionPool.GetList<Color32>(4096);
            var indices = NativeCollectionPool.GetList<int>(6144);
            var triangles = NativeCollectionPool.GetList<int3>(2048);

            // OPTIMIZATION 10.9.2: Use parallel marching cubes job
            int step = math.max(1, voxelStep);
            int cubesPerAxis = MarchingCubesParallelHelper.CalculateCubesPerAxis(32, step);
            int totalCubes = cubesPerAxis * cubesPerAxis * cubesPerAxis;
            
            // Allocate NativeStream for parallel output
            // FIX: Use Persistent instead of TempJob to avoid 4-frame timeout leaks
            // We manually dispose this after Phase 0 completes
            var outputStream = new NativeStream(totalCubes, Allocator.Persistent);
            
            // Schedule Parallel Marching Cubes Job
            var parallelJob = new GenerateMarchingCubesParallelJob
            {
                Densities = paddedDensities,
                Materials = paddedMaterials,
                ChunkSize = new int3(32, 32, 32),
                IsoLevel = ISO_LEVEL,
                VertexScale = 1.0f,
                VoxelStep = step,
                CubesPerAxis = cubesPerAxis,
                TotalCubes = totalCubes,
                OutputWriter = outputStream.AsWriter()
            };
            
            // Schedule with batch size 64 for good work distribution
            var parallelHandle = parallelJob.Schedule(totalCubes, 64, copyHandle);
            
            // Schedule Merge Job to combine stream output into final arrays
            var mergeJob = new MergeMarchingCubesOutputJob
            {
                InputReader = outputStream.AsReader(),
                Vertices = vertices,
                Normals = normals,
                Colors = colors,
                Indices = indices,
                Triangles = triangles,
                ForEachCount = totalCubes
            };
            
            var mergeHandle = mergeJob.Schedule(parallelHandle);
            
            _pendingMeshJobs.Add(new PendingMeshJob
            {
                Entity = entity,
                ChunkPos = chunkPos,
                Phase = 0,
                MarchingCubesHandle = mergeHandle, // Completing this ensures parallel + merge are done
                PaddedDensities = paddedDensities,
                PaddedMaterials = paddedMaterials,
                Vertices = vertices,
                Normals = normals,
                Colors = colors,
                Indices = indices,
                Triangles = triangles,
                OutputStream = outputStream,
                TotalCubes = totalCubes
            });
        }
        
        private void FinalizeMesh(PendingMeshJob job)
        {
            // Verify entity exists
            if (!EntityManager.Exists(job.Entity)) return;
            
            if (job.Vertices.Length > 0)
            {
                Mesh mesh = new Mesh();
                
                // Optimization: Use MeshDataArray API (Task 8.14.6)
                var meshDataArray = Mesh.AllocateWritableMeshData(1);
                var meshData = meshDataArray[0];
                
                int vertexCount = job.Vertices.Length;
                int indexCount = job.Indices.Length;
                
                // Configure Streams: 0=Pos, 1=Normal, 2=Color (Optional)
                bool hasColors = job.Colors.Length == vertexCount;
                var attributes = new NativeArray<VertexAttributeDescriptor>(
                    hasColors ? 3 : 2, Allocator.Temp, NativeArrayOptions.UninitializedMemory
                );
                
                attributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, stream: 0);
                attributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, stream: 1);
                if (hasColors)
                    attributes[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4, stream: 2);
                    
                meshData.SetVertexBufferParams(vertexCount, attributes);
                attributes.Dispose();
                
                meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
                
                // MemCpy directly to mesh memory
                meshData.GetVertexData<float3>(0).CopyFrom(job.Vertices.AsArray());
                meshData.GetVertexData<float3>(1).CopyFrom(job.Normals.AsArray());
                if (hasColors)
                    meshData.GetVertexData<Color32>(2).CopyFrom(job.Colors.AsArray());
                    
                meshData.GetIndexData<int>().CopyFrom(job.Indices.AsArray());
                
                // Setup SubMesh
                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
                {
                    bounds = new Bounds(new Vector3(VoxelConstants.CHUNK_SIZE/2f, VoxelConstants.CHUNK_SIZE/2f, VoxelConstants.CHUNK_SIZE/2f), new Vector3(VoxelConstants.CHUNK_SIZE, VoxelConstants.CHUNK_SIZE, VoxelConstants.CHUNK_SIZE)),
                    firstVertex = 0,
                    vertexCount = vertexCount
                });

                // Apply
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
                mesh.RecalculateBounds(); // Ensure precise bounds for culling
                
                // HYBRID V1: GameObjects (Restored for Stability/Visuals)
                
                // Get or Create GameObject
                ChunkGameObject chunkGo = null;
                if (EntityManager.HasComponent<ChunkGameObject>(job.Entity))
                {
                    chunkGo = EntityManager.GetComponentData<ChunkGameObject>(job.Entity);
                    if (chunkGo.Value == null)
                    {
                        // Object was destroyed but component exists?
                        EntityManager.RemoveComponent<ChunkGameObject>(job.Entity);
                        chunkGo = null;
                    }
                }

                if (chunkGo == null)
                {
                    // Enable Shadow Casting based on LOD AND distance
                    // OPTIMIZATION 10.13.3: Disable shadows for distant chunks to reduce Draw Calls
                    bool castShadows = true;
                    
                    // Check LOD level first
                    if (EntityManager.HasComponent<ChunkLODState>(job.Entity))
                    {
                        var lodState = EntityManager.GetComponentData<ChunkLODState>(job.Entity);
                        if (lodState.CurrentLOD > 0) castShadows = false; // Only LOD 0 casts shadows
                        
                        // OPTIMIZATION 10.13.3: Also check distance even for LOD 0
                        // Chunks beyond SHADOW_DISTANCE don't cast shadows regardless of LOD
                        if (lodState.DistanceToCamera * lodState.DistanceToCamera > SHADOW_DISTANCE_SQ)
                        {
                            castShadows = false;
                        }
                    }

                    if (_cachedMaterial == null) InitializeMaterial();

                    // OPTIMIZATION 10.13.4: Use Pooled GameObjects
                    // Eliminates GC Alloc for strings and massive Object.Instantiate overhead
                    var go = ChunkMeshPool.Get(
                         CoordinateUtils.ChunkToWorldPos(job.ChunkPos),
                        _cachedMaterial,
                        castShadows
                    );
                    
                    chunkGo = new ChunkGameObject
                    {
                        Value = go,
                        MeshFilter = go.GetComponent<MeshFilter>(),
                        MeshRenderer = go.GetComponent<MeshRenderer>(),
                        MeshCollider = go.GetComponent<UnityEngine.MeshCollider>() // For ragdoll collision
                    };

                    EntityManager.AddComponentObject(job.Entity, chunkGo);
                }
                else
                {
                    // If reusing existing GO, update material/shadows just in case LOD changed
                    bool castShadows = true;
                    if (EntityManager.HasComponent<ChunkLODState>(job.Entity))
                    {
                        var lodState = EntityManager.GetComponentData<ChunkLODState>(job.Entity);
                        if (lodState.CurrentLOD > 0) castShadows = false;
                        
                        // OPTIMIZATION 10.13.3: Distance-based shadow culling
                        if (lodState.DistanceToCamera * lodState.DistanceToCamera > SHADOW_DISTANCE_SQ)
                        {
                            castShadows = false;
                        }
                    }
                    chunkGo.MeshRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }

                // Update Mesh
                // Note: We need to handle mesh destruction to avoid leaks if overriding
                if (chunkGo.MeshFilter.sharedMesh != null && chunkGo.MeshFilter.sharedMesh != mesh)
                {
                    UnityEngine.Object.Destroy(chunkGo.MeshFilter.sharedMesh);
                }
                chunkGo.MeshFilter.sharedMesh = mesh;
                if (chunkGo.MeshCollider != null) 
                {
                    chunkGo.MeshCollider.sharedMesh = mesh; // Legacy collider support if used
                    if (EnableDebugLogs) UnityEngine.Debug.Log($"[ChunkMeshing] Updated GO MeshCollider for {chunkGo.Value.name}");
                }
                else
                {
                    if (EnableDebugLogs) UnityEngine.Debug.Log($"[ChunkMeshing] GO {chunkGo.Value.name} has NO MeshCollider");
                }

                // Clean up ECS Rendering components if they accidentally exist (Migration cleanup)
                if (EntityManager.HasComponent<MaterialMeshInfo>(job.Entity))
                {
                    EntityManager.RemoveComponent<MaterialMeshInfo>(job.Entity);
                    if (EntityManager.HasComponent<RenderBounds>(job.Entity))
                        EntityManager.RemoveComponent<RenderBounds>(job.Entity);
                }
                
                // Ensure RenderMeshArray is NOT present
                if (EntityManager.HasComponent<RenderMeshArray>(job.Entity)) EntityManager.RemoveComponent<RenderMeshArray>(job.Entity);

                // NOTE: Collider creation moved to ChunkColliderBuildSystem (job-based, async)
                // The ChunkNeedsCollider tag is already set - ChunkColliderBuildSystem will handle it
                // This removes synchronous MeshCollider.Create() from the main thread
            }
        }
        
        private void DisposeJobData(PendingMeshJob job)
        {
            if (job.PaddedDensities.IsCreated) NativeCollectionPool.ReturnArray(job.PaddedDensities);
            if (job.PaddedMaterials.IsCreated) NativeCollectionPool.ReturnArray(job.PaddedMaterials);
            if (job.Vertices.IsCreated) NativeCollectionPool.ReturnList(job.Vertices);
            if (job.Normals.IsCreated) NativeCollectionPool.ReturnList(job.Normals);
            if (job.Colors.IsCreated) NativeCollectionPool.ReturnList(job.Colors);
            if (job.Indices.IsCreated) NativeCollectionPool.ReturnList(job.Indices);
            if (job.Triangles.IsCreated) NativeCollectionPool.ReturnList(job.Triangles);
            if (job.SmoothNormals.IsCreated) NativeCollectionPool.ReturnList(job.SmoothNormals);
            // OPTIMIZATION 10.9.2: Dispose NativeStream
            if (job.OutputStream.IsCreated) job.OutputStream.Dispose();
        }
        

        
        private void InitializeMaterial()
        {
             // Shader finding logic (Existing)
            var shader = Shader.Find("DIG/Voxel/Triplanar");
            if (shader == null) shader = Shader.Find("DIG/VoxelTriplanar");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            
            // Fallback to error shader if all else fails
            if (shader == null)
            {
                shader = Shader.Find("Hidden/InternalErrorShader");
                UnityEngine.Debug.LogError("[ChunkMeshingSystem] Failed to find any suitable shader! Visuals will be broken.");
            }
            
            if (shader != null)
            {
                if (_cachedMaterial == null) _cachedMaterial = new UnityEngine.Material(shader);
                else _cachedMaterial.shader = shader; // Update existing if possible
                
                _cachedMaterial.name = "VoxelTerrain";
                _cachedMaterial.SetColor("_Color", new Color(0.6f, 0.5f, 0.4f));
                if (_cachedMaterial.HasProperty("_Smoothness")) _cachedMaterial.SetFloat("_Smoothness", 0.1f);
                if (_cachedMaterial.HasProperty("_Metallic")) _cachedMaterial.SetFloat("_Metallic", 0.0f);
                
                var textureConfig = Resources.Load<DIG.Voxel.Rendering.VoxelTextureConfig>("VoxelTextureConfig");
                if (textureConfig != null && textureConfig.TextureArray != null)
                {
                    _cachedMaterial.SetTexture("_MainTex", textureConfig.TextureArray);
                    if (_cachedMaterial.HasProperty("_TextureScale")) _cachedMaterial.SetFloat("_TextureScale", 0.25f);
                }
                
                if (shader.name != "DIG/Voxel/Triplanar")
                {
                    if (_cachedMaterial.HasProperty("_BaseColor")) _cachedMaterial.SetColor("_BaseColor", new Color(0.6f, 0.5f, 0.4f));
                }
                
                _cachedMaterial.enableInstancing = true;
            }
        }
        
        /// <summary>
        /// DIAGNOSTIC 10.13: Log rendering stats every STATS_LOG_INTERVAL seconds.
        /// Helps diagnose shadow casters, LOD distribution, and batching issues.
        /// </summary>
        private void LogRenderingStats()
        {
            if (!EnableRenderingStatsLogs) return;
            
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            if (currentTime - _lastStatsLogTime < STATS_LOG_INTERVAL) return;
            _lastStatsLogTime = currentTime;
            
            // Count chunks by LOD level and shadow status
            int totalChunks = 0;
            int shadowCasters = 0;
            int shadowsOff = 0;
            int[] lodCounts = new int[5]; // LOD 0-4
            int noLodComponent = 0;
            int beyondShadowDist = 0;
            float minDist = float.MaxValue;
            float maxDist = 0f;
            
            foreach (var (chunkGo, entity) in SystemAPI.Query<ChunkGameObject>().WithEntityAccess())
            {
                if (chunkGo.Value == null) continue;
                totalChunks++;
                
                var mr = chunkGo.MeshRenderer;
                if (mr != null)
                {
                    if (mr.shadowCastingMode == ShadowCastingMode.On)
                        shadowCasters++;
                    else
                        shadowsOff++;
                }
                
                if (EntityManager.HasComponent<ChunkLODState>(entity))
                {
                    var lodState = EntityManager.GetComponentData<ChunkLODState>(entity);
                    int lod = math.clamp(lodState.CurrentLOD, 0, 4);
                    lodCounts[lod]++;
                    
                    float dist = lodState.DistanceToCamera;
                    minDist = math.min(minDist, dist);
                    maxDist = math.max(maxDist, dist);
                    
                    if (dist * dist > SHADOW_DISTANCE_SQ)
                        beyondShadowDist++;
                }
                else
                {
                    noLodComponent++;
                }
            }
            
            // Build and log the summary
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ChunkMeshingSystem] Rendering Stats (T={currentTime:F1}s):");
            sb.AppendLine($"  Total Chunks: {totalChunks}");
            sb.AppendLine($"  Shadow Casters: {shadowCasters} | Off: {shadowsOff}");
            sb.AppendLine($"  LOD Distribution: L0={lodCounts[0]}, L1={lodCounts[1]}, L2={lodCounts[2]}, L3={lodCounts[3]}, L4={lodCounts[4]}");
            sb.AppendLine($"  No LOD Component: {noLodComponent}");
            sb.AppendLine($"  Beyond Shadow Dist ({SHADOW_DISTANCE}m): {beyondShadowDist}");
            sb.AppendLine($"  Distance Range: {minDist:F1}m - {maxDist:F1}m");
            sb.AppendLine($"  Material Instancing: {(_cachedMaterial != null && _cachedMaterial.enableInstancing ? "ON" : "OFF")}");
            
            // DIAGNOSTIC: Scene-wide shadow caster audit
            var allRenderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(UnityEngine.FindObjectsSortMode.None);
            int sceneShadowCasters = 0;
            var shadowCastersByPrefix = new System.Collections.Generic.Dictionary<string, int>();
            
            foreach (var mr in allRenderers)
            {
                if (mr.shadowCastingMode == ShadowCastingMode.On && mr.enabled && mr.gameObject.activeInHierarchy)
                {
                    sceneShadowCasters++;
                    // Group by name prefix (first word or "Chunk_" pattern)
                    string name = mr.gameObject.name;
                    string prefix = name.Contains("_") ? name.Split('_')[0] + "_*" : name;
                    if (prefix.Length > 20) prefix = prefix.Substring(0, 20) + "...";
                    
                    if (!shadowCastersByPrefix.ContainsKey(prefix))
                        shadowCastersByPrefix[prefix] = 0;
                    shadowCastersByPrefix[prefix]++;
                }
            }
            
            sb.AppendLine($"  --- SCENE SHADOW AUDIT ---");
            sb.AppendLine($"  Total MeshRenderer ShadowCasters: {sceneShadowCasters}");
            
            // Sort and show top 5 contributors
            var sorted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(shadowCastersByPrefix);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            
            for (int i = 0; i < System.Math.Min(5, sorted.Count); i++)
            {
                sb.AppendLine($"    {sorted[i].Key}: {sorted[i].Value}");
            }
            
            // TASK 10.13.2: BATCHING DIAGNOSTICS
            sb.AppendLine($"  --- BATCHING DIAGNOSTICS ---");
            
            // Count unique meshes, materials, and analyze batching eligibility
            var meshSet = new System.Collections.Generic.HashSet<int>(); // Mesh instance IDs
            var materialSet = new System.Collections.Generic.HashSet<int>(); // Material instance IDs
            int chunkRendererCount = 0;
            int totalVerts = 0;
            int dynamicBatchEligible = 0; // <300 verts AND dynamic batching supported
            int staticBatchEligible = 0;  // Marked static
            int srpBatcherCompatible = 0;
            int hasPropertyBlock = 0;
            int maxVertsInChunk = 0;
            int minVertsInChunk = int.MaxValue;
            
            foreach (var (chunkGo, entity) in SystemAPI.Query<ChunkGameObject>().WithEntityAccess())
            {
                if (chunkGo.Value == null || chunkGo.MeshRenderer == null) continue;
                chunkRendererCount++;
                
                var mr = chunkGo.MeshRenderer;
                var mf = chunkGo.Value.GetComponent<MeshFilter>();
                
                // Material analysis
                if (mr.sharedMaterial != null)
                {
                    materialSet.Add(mr.sharedMaterial.GetInstanceID());
                    
                    // Check SRP Batcher compatibility
                    if (mr.sharedMaterial.shader != null && mr.sharedMaterial.enableInstancing)
                    {
                        srpBatcherCompatible++;
                    }
                }
                
                // Check for MaterialPropertyBlock (breaks batching)
                if (mr.HasPropertyBlock())
                {
                    hasPropertyBlock++;
                }
                
                // Mesh analysis
                if (mf != null && mf.sharedMesh != null)
                {
                    var mesh = mf.sharedMesh;
                    meshSet.Add(mesh.GetInstanceID());
                    
                    int verts = mesh.vertexCount;
                    totalVerts += verts;
                    maxVertsInChunk = System.Math.Max(maxVertsInChunk, verts);
                    minVertsInChunk = System.Math.Min(minVertsInChunk, verts);
                    
                    // Dynamic batching: requires <300 verts AND no skinning/particles
                    if (verts < 300)
                    {
                        dynamicBatchEligible++;
                    }
                }
                
                // Static batching check
                if (chunkGo.Value.isStatic)
                {
                    staticBatchEligible++;
                }
            }
            
            if (minVertsInChunk == int.MaxValue) minVertsInChunk = 0;
            
            sb.AppendLine($"  Chunk Renderers: {chunkRendererCount}");
            sb.AppendLine($"  Unique Meshes: {meshSet.Count} (batching needs shared meshes!)");
            sb.AppendLine($"  Unique Materials: {materialSet.Count}");
            sb.AppendLine($"  Total Vertices: {totalVerts:N0}");
            sb.AppendLine($"  Verts/Chunk: min={minVertsInChunk}, max={maxVertsInChunk}");
            sb.AppendLine($"  Dynamic Batch Eligible (<300 verts): {dynamicBatchEligible}");
            sb.AppendLine($"  Static Batch Eligible (isStatic): {staticBatchEligible}");
            sb.AppendLine($"  SRP Batcher Compatible: {srpBatcherCompatible}");
            sb.AppendLine($"  Has MaterialPropertyBlock (breaks batching): {hasPropertyBlock}");
            
            // Explain why batching is 0
            if (meshSet.Count == chunkRendererCount && chunkRendererCount > 0)
            {
                sb.AppendLine($"  ⚠️ BATCHING ISSUE: Each chunk has a UNIQUE mesh!");
                sb.AppendLine($"     Static/Dynamic batching requires SHARED meshes.");
                sb.AppendLine($"     GPU Instancing works with unique meshes but same material.");
            }
            if (maxVertsInChunk >= 300)
            {
                sb.AppendLine($"  ⚠️ Dynamic batching disabled: chunks have >{299} verts");
            }
            
            UnityEngine.Debug.Log(sb.ToString());
        }
        
        /// <summary>
        /// OPTIMIZATION 10.13.3: Dynamically update shadow states for all existing chunks.
        /// This ensures chunks that move beyond SHADOW_DISTANCE have shadows disabled.
        /// </summary>
        private void UpdateChunkShadowStates()
        {
            foreach (var (chunkGo, entity) in SystemAPI.Query<ChunkGameObject>().WithEntityAccess())
            {
                if (chunkGo.Value == null || chunkGo.MeshRenderer == null) continue;
                
                bool shouldCastShadow = true;
                
                if (EntityManager.HasComponent<ChunkLODState>(entity))
                {
                    var lodState = EntityManager.GetComponentData<ChunkLODState>(entity);
                    
                    // LOD > 0 = no shadows
                    if (lodState.CurrentLOD > 0)
                    {
                        shouldCastShadow = false;
                    }
                    // Distance > SHADOW_DISTANCE = no shadows
                    else if (lodState.DistanceToCamera * lodState.DistanceToCamera > SHADOW_DISTANCE_SQ)
                    {
                        shouldCastShadow = false;
                    }
                }
                
                // Only update if changed (avoid redundant property sets)
                var currentMode = chunkGo.MeshRenderer.shadowCastingMode;
                var targetMode = shouldCastShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
                
                if (currentMode != targetMode)
                {
                    chunkGo.MeshRenderer.shadowCastingMode = targetMode;
                }
            }
        }
        
        // Reuse GetNeighborBlobs, GetDensityWithNeighbors, GetMaterialWithNeighbors, AssignMeshToGameObject from existing code
        // (Just ensure they remain in the class)
        
        // ... (Remaining methods preserved) ...
        
        private void AssignMeshToGameObject(Entity entity, Mesh mesh, int3 chunkPos)
        {
            if (EntityManager.HasComponent<ChunkGameObject>(entity))
            {
                var chunkGo = EntityManager.GetComponentData<ChunkGameObject>(entity);
                if (chunkGo.Value != null)
                {
                    var filter = chunkGo.Value.GetComponent<MeshFilter>();
                    var collider = chunkGo.Value.GetComponent<UnityEngine.MeshCollider>();
                    if (filter.sharedMesh != null) UnityEngine.Object.Destroy(filter.sharedMesh);
                    filter.sharedMesh = mesh;
                    if (collider != null) { collider.sharedMesh = null; collider.convex = false; collider.sharedMesh = mesh; }
                    return;
                }
            }
            
            GameObject go = new GameObject("Chunk"); // OPTIMIZATION: Avoid string concat allocs
            go.transform.position = CoordinateUtils.ChunkToWorldPos(chunkPos);
            go.layer = LayerMask.NameToLayer("Default");
            go.isStatic = true;
            
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mc = go.AddComponent<UnityEngine.MeshCollider>();
            
            if (mf.sharedMesh != null) UnityEngine.Object.Destroy(mf.sharedMesh);
            mf.sharedMesh = mesh;
            mc.sharedMesh = null; mc.convex = false; mc.sharedMesh = mesh;
            
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
            if (_cachedMaterial != null) mr.sharedMaterial = _cachedMaterial;
            
            EntityManager.AddComponentData(entity, new ChunkGameObject { Value = go });
        }
        
        // ... (GetNeighborBlobs, GetDensityWithNeighbors etc) ...
        // I will rely on the fact that I'm keeping the original methods (check EndLine/StartLine logic)
        // Wait, I am replacing LINES 23-542? That overwrites EVERYTHING.
        // So I MUST include the helper methods in ReplacementContent.
        
        // I will include ALL methods to be safe.
        
        private NeighborBlobs GetNeighborBlobs(Entity entity)
        {
             var neighbors = new NeighborBlobs();
             if (!EntityManager.HasComponent<ChunkNeighbors>(entity)) return neighbors;
             
             var refs = EntityManager.GetComponentData<ChunkNeighbors>(entity);
             
             // +X
             if (refs.PosX != Entity.Null && EntityManager.HasComponent<ChunkVoxelData>(refs.PosX))
             { if (EntityManager.GetComponentData<ChunkVoxelData>(refs.PosX).IsValid) { neighbors.HasPosX = true; neighbors.PosX = EntityManager.GetComponentData<ChunkVoxelData>(refs.PosX).Data; } }
             // -X
             if (refs.NegX != Entity.Null && EntityManager.HasComponent<ChunkVoxelData>(refs.NegX))
             { if (EntityManager.GetComponentData<ChunkVoxelData>(refs.NegX).IsValid) { neighbors.HasNegX = true; neighbors.NegX = EntityManager.GetComponentData<ChunkVoxelData>(refs.NegX).Data; } }
             // +Y
             if (refs.PosY != Entity.Null && EntityManager.HasComponent<ChunkVoxelData>(refs.PosY))
             { if (EntityManager.GetComponentData<ChunkVoxelData>(refs.PosY).IsValid) { neighbors.HasPosY = true; neighbors.PosY = EntityManager.GetComponentData<ChunkVoxelData>(refs.PosY).Data; } }
             // -Y
             if (refs.NegY != Entity.Null && EntityManager.HasComponent<ChunkVoxelData>(refs.NegY))
             { if (EntityManager.GetComponentData<ChunkVoxelData>(refs.NegY).IsValid) { neighbors.HasNegY = true; neighbors.NegY = EntityManager.GetComponentData<ChunkVoxelData>(refs.NegY).Data; } }
             // +Z
             if (refs.PosZ != Entity.Null && EntityManager.HasComponent<ChunkVoxelData>(refs.PosZ))
             { if (EntityManager.GetComponentData<ChunkVoxelData>(refs.PosZ).IsValid) { neighbors.HasPosZ = true; neighbors.PosZ = EntityManager.GetComponentData<ChunkVoxelData>(refs.PosZ).Data; } }
             // -Z
             if (refs.NegZ != Entity.Null && EntityManager.HasComponent<ChunkVoxelData>(refs.NegZ))
             { if (EntityManager.GetComponentData<ChunkVoxelData>(refs.NegZ).IsValid) { neighbors.HasNegZ = true; neighbors.NegZ = EntityManager.GetComponentData<ChunkVoxelData>(refs.NegZ).Data; } }
             
             return neighbors;
        }

        private struct NeighborBlobs
        {
            public bool HasPosX, HasNegX, HasPosY, HasNegY, HasPosZ, HasNegZ;
            public BlobAssetReference<VoxelBlob> PosX, NegX, PosY, NegY, PosZ, NegZ;
        }
    }
}
