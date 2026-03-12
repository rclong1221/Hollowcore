using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DIG.Voxel.Components;
using DIG.Voxel.Core;
using DIG.Voxel.Rendering;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Systems
{
    /// <summary>
    /// Manages LOD levels for all chunks based on camera distance.
    /// Updates chunk LOD states and triggers mesh regeneration when LOD changes.
    /// Optimized with distance-squared checks and priority-based updates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DIG.Voxel.Systems.Meshing.ChunkMeshingSystem))]
    public partial class ChunkLODSystem : SystemBase
    {
        private VoxelLODConfig _config;
        private float _lastUpdateTime;
        private float3 _cachedCameraPos;
        
        // Pre-computed squared distances for each LOD level
        private NativeArray<float> _lodDistancesSq;

        protected override void OnCreate()
        {
            RequireForUpdate<VoxelWorldEnabled>();

            // Load config
            _config = Resources.Load<VoxelLODConfig>("VoxelLODConfig");
            if (_config == null)
            {
                UnityEngine.Debug.LogWarning("[ChunkLODSystem] VoxelLODConfig not found in Resources. LOD disabled.");
                Enabled = false;
                return;
            }
            
            // Pre-compute squared distances (avoiding sqrt in main loop)
            _lodDistancesSq = new NativeArray<float>(_config.Levels.Length, Allocator.Persistent);
            for (int i = 0; i < _config.Levels.Length; i++)
            {
                _lodDistancesSq[i] = _config.Levels[i].Distance * _config.Levels[i].Distance;
            }
        }

        protected override void OnDestroy()
        {
            if (_lodDistancesSq.IsCreated)
                _lodDistancesSq.Dispose();
        }

        protected override void OnUpdate()
        {
            if (_config == null) return;

            // Throttle updates based on config frequency
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            if (currentTime - _lastUpdateTime < _config.UpdateFrequency)
                return;
            _lastUpdateTime = currentTime;
            
            VoxelProfiler.BeginSample("LODSystem");

            // OPTIMIZATION 10.9.5: Use cached camera data instead of Camera.main
            if (!SystemAPI.HasSingleton<CameraData>())
            {
                VoxelProfiler.EndSample("LODSystem");
                return;
            }
            var cameraData = SystemAPI.GetSingleton<CameraData>();
            if (!cameraData.IsValid) 
            {
                VoxelProfiler.EndSample("LODSystem");
                return;
            }
            _cachedCameraPos = cameraData.Position;
            
            float hysteresisSq = _config.Hysteresis * _config.Hysteresis;
            int updatesThisFrame = 0;
            int maxUpdates = _config.MaxUpdatesPerFrame;
            float halfChunkSize = VoxelConstants.CHUNK_SIZE * 0.5f;

            // Query all chunks with LOD state
            foreach (var (lodState, chunkPos, entity) in 
                SystemAPI.Query<RefRW<ChunkLODState>, RefRO<ChunkPosition>>()
                .WithEntityAccess())
            {
                // Calculate distance squared (faster than distance)
                float3 chunkCenter = CoordinateUtils.ChunkToWorldPos(chunkPos.ValueRO.Value) 
                                     + new float3(halfChunkSize);
                float distSq = math.distancesq(_cachedCameraPos, chunkCenter);

                // Store actual distance for external use (computed lazily only when needed)
                lodState.ValueRW.DistanceToCamera = math.sqrt(distSq);

                // Determine target LOD using squared distances
                int targetLOD = GetLODLevelSq(distSq, lodState.ValueRO.CurrentLOD, hysteresisSq);

                if (targetLOD != lodState.ValueRO.CurrentLOD)
                {
                    lodState.ValueRW.TargetLOD = targetLOD;
                    lodState.ValueRW.NeedsLODUpdate = true;

                    // Limit updates per frame
                    if (updatesThisFrame < maxUpdates)
                    {
                        // Apply the LOD change
                        lodState.ValueRW.CurrentLOD = targetLOD;
                        lodState.ValueRW.NeedsLODUpdate = false;

                        // Trigger mesh regeneration via enableable component
                        if (EntityManager.HasComponent<ChunkNeedsLODMesh>(entity))
                        {
                            EntityManager.SetComponentEnabled<ChunkNeedsLODMesh>(entity, true);
                        }

                        // Handle collider based on LOD config (batch at end if possible)
                        UpdateColliderState(entity, targetLOD);

                        updatesThisFrame++;
                    }
                }
            }
            
            VoxelProfiler.EndSample("LODSystem");
        }

        /// <summary>
        /// Get LOD level using squared distances for performance.
        /// </summary>
        private int GetLODLevelSq(float distSq, int currentLOD, float hysteresisSq)
        {
            int targetLOD = _config.Levels.Length - 1;
            
            for (int i = 0; i < _lodDistancesSq.Length; i++)
            {
                if (distSq <= _lodDistancesSq[i])
                {
                    targetLOD = i;
                    break;
                }
            }

            // Apply hysteresis using squared distances
            if (targetLOD != currentLOD && hysteresisSq > 0)
            {
                float thresholdSq = _lodDistancesSq[math.max(0, currentLOD)];
                
                if (currentLOD < targetLOD)
                {
                    // Moving to lower LOD - require more distance
                    if (distSq < thresholdSq + hysteresisSq * 2f * math.sqrt(thresholdSq))
                        return currentLOD;
                }
                else
                {
                    // Moving to higher LOD - require less distance
                    if (distSq > thresholdSq - hysteresisSq * 2f * math.sqrt(thresholdSq))
                        return currentLOD;
                }
            }
            
            return targetLOD;
        }

        private void UpdateColliderState(Entity entity, int lodLevel)
        {
            bool shouldHaveCollider = _config.ShouldHaveCollider(lodLevel);

            // If chunk has a ChunkGameObject with MeshCollider, enable/disable it
            if (EntityManager.HasComponent<ChunkGameObject>(entity))
            {
                var chunkGo = EntityManager.GetComponentData<ChunkGameObject>(entity);
                if (chunkGo.Value != null)
                {
                    var collider = chunkGo.Value.GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        collider.enabled = shouldHaveCollider;
                    }
                }
            }

            // Update ChunkColliderState component
            if (EntityManager.HasComponent<ChunkColliderState>(entity))
            {
                var state = EntityManager.GetComponentData<ChunkColliderState>(entity);
                state.IsActive = shouldHaveCollider;
                EntityManager.SetComponentData(entity, state);
            }
        }
    }
}

