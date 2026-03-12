using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Transforms;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Meshing;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// System for generating meshes for fluids.
    /// Decoupled from terrain meshing to prevent lag.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class FluidMeshSystem : SystemBase
    {
        private const byte FLUID_ISO_LEVEL = 10; // Threshold (0-255)
        
        // Pending mesh jobs
        private struct PendingFluidMesh
        {
            public Entity ChunkEntity;
            public Entity MeshEntity;
            public JobHandle Handle;
            
            public NativeArray<FluidCell> FluidCells; // Copied or Aliased? 
            // Better to copy if async.
            // Or access Buffer directly if strictly ordering systems.
            
            public NativeList<float3> Vertices;
            public NativeList<float3> Normals;
            public NativeList<int> Indices;
        }
        
        private NativeList<PendingFluidMesh> _pendingJobs;
        private Material _fluidMaterial;
        
        protected override void OnCreate()
        {
            _pendingJobs = new NativeList<PendingFluidMesh>(Allocator.Persistent);
            
            // Try to find a material or create a default one
            _fluidMaterial = Resources.Load<Material>("Measurements/FluidMaterial"); 
            if (_fluidMaterial == null)
            {
                // Create a simple blue material
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _fluidMaterial = new Material(shader);
                _fluidMaterial.color = new Color(0, 0.5f, 1f, 0.8f);
                _fluidMaterial.SetFloat("_Mode", 3); // Transparent
            }
        }
        
        protected override void OnDestroy()
        {
            foreach (var job in _pendingJobs)
            {
                job.Handle.Complete();
                job.FluidCells.Dispose();
                job.Vertices.Dispose();
                job.Normals.Dispose();
                job.Indices.Dispose();
            }
            _pendingJobs.Dispose();
        }
        
        protected override void OnUpdate()
        {
            // 1. Process Finished Jobs
            CompleteJobs();
            
            // 2. Schedule New Jobs
            // We check if Fluid Buffer has changed
            // Iterate all chunks with fluids
            
            // EntityManager needed for creating child entities (Hybrid)
            var em = EntityManager;
            
            foreach (var (fluids, fluidData, entity) in 
                SystemAPI.Query<DynamicBuffer<FluidBufferElement>, RefRO<ChunkFluidData>>()
                .WithChangeFilter<FluidBufferElement>()
                .WithEntityAccess())
            {
                // Skip if no fluid
                if (fluidData.ValueRO.HasFluid == 0) continue;
                
                // Ensure Mesh Entity exists
                Entity meshEntity;
                if (SystemAPI.HasComponent<FluidMeshReference>(entity))
                {
                    meshEntity = SystemAPI.GetComponent<FluidMeshReference>(entity).MeshEntity;
                }
                else
                {
                    // Create Child
                    meshEntity = em.CreateEntity();
                    em.AddComponentData(meshEntity, new LocalToWorld { Value = float4x4.identity });
                    // RenderBounds handled by Hybrid Renderer via MeshRenderer logic or not needed for pure Hybrid
                    
                    // Link to parent
                    em.AddComponentData(meshEntity, new Parent { Value = entity });
                    em.AddComponentData(meshEntity, LocalTransform.Identity);
                    
                    // Store reference
                    em.AddComponentData(entity, new FluidMeshReference { MeshEntity = meshEntity });
                    
                    // Add Hybrid Renderer components
                    em.AddComponentObject(meshEntity, new MeshFilter());
                    em.AddComponentObject(meshEntity, new MeshRenderer { material = _fluidMaterial });
                }
                
                // Schedule Mesh Job
                ScheduleJob(entity, meshEntity, fluids);
            }
        }
        
        private void ScheduleJob(Entity chunkEntity, Entity meshEntity, DynamicBuffer<FluidBufferElement> buffer)
        {
            // Copy data to NativeArray (for async job safety)
            var cells = buffer.Reinterpret<FluidCell>().AsNativeArray();
            var cellsCopy = new NativeArray<FluidCell>(cells.Length, Allocator.TempJob); // TempJob lives 4 frames max
            cellsCopy.CopyFrom(cells);
            
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var normals = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);
            
            var job = new GenerateFluidMeshJob
            {
                FluidCells = cellsCopy,
                ChunkSize = new int3(32, 32, 32), // Standard size
                IsoLevel = FLUID_ISO_LEVEL,
                Vertices = vertices,
                Normals = normals,
                Indices = indices
            };
            
            JobHandle handle = job.Schedule();
            
            _pendingJobs.Add(new PendingFluidMesh
            {
                ChunkEntity = chunkEntity,
                MeshEntity = meshEntity,
                Handle = handle,
                FluidCells = cellsCopy,
                Vertices = vertices,
                Normals = normals,
                Indices = indices
            });
        }
        
        private void CompleteJobs()
        {
            for (int i = _pendingJobs.Length - 1; i >= 0; i--)
            {
                var job = _pendingJobs[i];
                if (job.Handle.IsCompleted)
                {
                    job.Handle.Complete();
                    
                    // Apply Mesh (Main Thread)
                    if (EntityManager.Exists(job.MeshEntity))
                    {
                        var meshFilter = EntityManager.GetComponentObject<MeshFilter>(job.MeshEntity);
                        
                        // Create or update mesh
                        Mesh mesh = meshFilter.sharedMesh;
                        if (mesh == null) 
                        {
                            mesh = new Mesh();
                            mesh.name = "FluidMesh";
                            meshFilter.sharedMesh = mesh;
                        }
                        
                        mesh.Clear(); // Full clear or SetVertices with flags? Clear is safer for topo changes
                        mesh.SetVertices(job.Vertices.AsArray());
                        mesh.SetNormals(job.Normals.AsArray());
                        mesh.SetIndices(job.Indices.AsArray(), MeshTopology.Triangles, 0);
                        mesh.RecalculateBounds();
                    }
                    
                    // Cleanup
                    job.FluidCells.Dispose();
                    job.Vertices.Dispose();
                    job.Normals.Dispose();
                    job.Indices.Dispose();
                    
                    _pendingJobs.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
