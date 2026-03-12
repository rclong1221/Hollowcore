using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DIG.Voxel.Core;
using DIG.Voxel.Components;
using DIG.Voxel.Debug;

namespace DIG.Voxel.Fluids
{
    /// <summary>
    /// System for updating fluid simulation in active chunks.
    /// Implements Optimization 10.8.1: Only simulating chunks with fluid.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct FluidSimulationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FluidSimulationSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var _ = VoxelProfilerMarkers.FluidSimulation.Auto();

            var settings = SystemAPI.GetSingleton<FluidSimulationSettings>();
            if (settings.Enabled == 0) return;
            
            float dt = SystemAPI.Time.DeltaTime;
            // Limit max viscosity to prevent instability
            float viscosity = 0.1f; 
            
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);
            var activeFluidLookup = SystemAPI.GetComponentLookup<ChunkHasActiveFluid>(false);
            
            // Iterate over all chunks that have fluid buffers AND are active
            foreach (var (fluidBuffer, voxelData, fluidData, activeStateRef, visibilityRef, entity) in 
                     SystemAPI.Query<DynamicBuffer<FluidBufferElement>, RefRO<ChunkVoxelData>, RefRW<ChunkFluidData>, RefRW<ChunkHasActiveFluid>, RefRO<ChunkVisibility>>()
                     .WithEntityAccess())
            {
                // Optimization: Skip chunks with no fluid (dry chunks)
                if (fluidData.ValueRO.HasFluid == 0) continue;
                
                // Optimization 10.3.17: Skip simulation for culled chunks
                if (!visibilityRef.ValueRO.IsVisible) continue;
                
                if (!voxelData.ValueRO.IsValid) continue;
                
                // Active simulation
                // 1. Create temporary array for next state
                var currentState = fluidBuffer.Reinterpret<FluidCell>().AsNativeArray();
                var nextState = new NativeArray<FluidCell>(currentState.Length, state.WorldUpdateAllocator);
                var isChunkActive = new NativeArray<byte>(1, state.WorldUpdateAllocator);
                isChunkActive[0] = 0;
                
                // 2. Run simulation job
                var job = new FluidSimulationJob
                {
                    CurrentState = currentState,
                    NextState = nextState,
                    VoxelBlob = voxelData.ValueRO.Data,
                    ChunkSize = new int3(VoxelConstants.CHUNK_SIZE, VoxelConstants.CHUNK_SIZE, VoxelConstants.CHUNK_SIZE),
                    DeltaTime = dt,
                    Viscosity = viscosity,
                    IsChunkActive = isChunkActive
                };
                
                // Schedule directly using state.Dependency
                state.Dependency = job.Schedule(currentState.Length, 64, state.Dependency);
                
                // 3. Copy results back to buffer
                state.Dependency = new ApplyFluidUpdateJob
                {
                    Buffer = fluidBuffer,
                    NextState = nextState,
                    IsChunkActive = isChunkActive
                }.Schedule(state.Dependency);
                
                // 4. Update Activity State & Sleep
                state.Dependency = new UpdateFluidActivityJob
                {
                    IsChunkActive = isChunkActive,
                    // ChunkActiveState assignment removed (not in struct) 
                    // We must use ComponentLookup or IJobEntity?
                    // Since we are traversing manually, we can pass a NativeReference if we allocated it? No.
                    // We can use [NativeDisableUnsafePtrRestriction] pointer? Unsafe.
                    // Correct ECS way: Schedule IJobEntity? But we want specific Entity.
                    // We need a lookup.
                    // BUT lookups inside Query loop is tricky.
                    // Simpler: Use a separate component data structure?
                    // No, let's use ComponentLookup passed to the job.
                    ActiveFluidLookup = activeFluidLookup,
                    Entity = entity,
                    ECB = ecb,
                    DeltaTime = dt
                }.Schedule(state.Dependency);
            }
        }
        
        [BurstCompile]
        private struct ApplyFluidUpdateJob : IJob
        {
            public DynamicBuffer<FluidBufferElement> Buffer;
            [ReadOnly] public NativeArray<FluidCell> NextState;
            [ReadOnly] public NativeArray<byte> IsChunkActive;
            
            public void Execute()
            {
                // Copy simulation result back to ECS buffer
                var bufferAsArray = Buffer.Reinterpret<FluidCell>().AsNativeArray();
                NativeArray<FluidCell>.Copy(NextState, bufferAsArray);
            }
        }
        
        [BurstCompile]
        private struct UpdateFluidActivityJob : IJob
        {
            [ReadOnly] public NativeArray<byte> IsChunkActive;
            public ComponentLookup<ChunkHasActiveFluid> ActiveFluidLookup;
            public Entity Entity;
            public EntityCommandBuffer ECB;
            public float DeltaTime;
            
            public void Execute()
            {
                if (!ActiveFluidLookup.HasComponent(Entity)) return;
                
                var state = ActiveFluidLookup[Entity];
                
                if (IsChunkActive[0] > 0)
                {
                    // Fluid moving, reset timer
                    state.TimeSinceLastChange = 0;
                }
                else
                {
                    // Fluid settled, increment timer
                    state.TimeSinceLastChange += DeltaTime;
                    
                    // Sleep threshold (e.g. 5 seconds)
                    if (state.TimeSinceLastChange > 5.0f)
                    {
                        ECB.RemoveComponent<ChunkHasActiveFluid>(Entity);
                    }
                }
                
                ActiveFluidLookup[Entity] = state;
            }
        }
    }
}
