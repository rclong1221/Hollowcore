using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using DIG.Swarm.Components;
using DIG.Swarm.Profiling;
using Player.Components;

namespace DIG.Swarm.Systems
{
    /// <summary>
    /// EPIC 16.2 Phase 1: Builds the flow field grid via BFS flood-fill from player positions.
    /// Phase 1 (BFS): Burst-compiled IJob — sequential but SIMD-optimized.
    /// Phase 2 (Directions): Burst-compiled IJobParallelFor — embarrassingly parallel.
    /// Persistent scratch buffers avoid per-rebuild allocations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct FlowFieldBuildSystem : ISystem
    {
        private NativeList<float3> _playerPositions;
        private NativeArray<float> _distanceField;
        private NativeQueue<int> _bfsQueue;
        private int _allocatedCellCount;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FlowFieldGrid>();
            _playerPositions = new NativeList<float3>(8, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_playerPositions.IsCreated) _playerPositions.Dispose();
            if (_distanceField.IsCreated) _distanceField.Dispose();
            if (_bfsQueue.IsCreated) _bfsQueue.Dispose();
            SwarmFlowFieldData.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            using (SwarmProfilerMarkers.FlowFieldBuild.Auto())
            {
                var grid = SystemAPI.GetSingletonRW<FlowFieldGrid>();
                ref var gridRef = ref grid.ValueRW;

                // Initialize on first frame
                if (!SwarmFlowFieldData.IsInitialized)
                {
                    int cellCount = gridRef.GridWidth * gridRef.GridHeight;
                    SwarmFlowFieldData.Initialize(cellCount);
                    gridRef.IsBuilt = false;
                }

                // Throttle rebuilds
                gridRef.TimeSinceLastUpdate += SystemAPI.Time.DeltaTime;
                if (gridRef.IsBuilt && gridRef.TimeSinceLastUpdate < gridRef.UpdateInterval)
                    return;

                gridRef.TimeSinceLastUpdate = 0f;

                // Gather player positions
                _playerPositions.Clear();
                foreach (var transform in
                    SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
                {
                    _playerPositions.Add(transform.ValueRO.Position);
                }

                if (_playerPositions.Length == 0)
                    return;

                var gridRO = gridRef;
                int width = gridRO.GridWidth;
                int height = gridRO.GridHeight;
                int totalCells = width * height;

                // Read static cells handle once (managed code), pass to Burst jobs
                var cells = SwarmFlowFieldData.Cells;

                // Allocate/resize persistent scratch buffers (reused across rebuilds)
                if (_allocatedCellCount != totalCells)
                {
                    if (_distanceField.IsCreated) _distanceField.Dispose();
                    if (_bfsQueue.IsCreated) _bfsQueue.Dispose();
                    _distanceField = new NativeArray<float>(totalCells, Allocator.Persistent);
                    _bfsQueue = new NativeQueue<int>(Allocator.Persistent);
                    _allocatedCellCount = totalCells;
                }

                // Copy player positions to temp array for job
                var playerPosArray = new NativeArray<float3>(_playerPositions.Length, Allocator.TempJob);
                _playerPositions.AsArray().CopyTo(playerPosArray);

                // Phase 1: BFS flood fill (Burst, single-threaded — BFS is inherently sequential)
                _bfsQueue.Clear();
                new BFSFloodFillJob
                {
                    DistanceField = _distanceField,
                    Cells = cells,
                    Queue = _bfsQueue,
                    PlayerPositions = playerPosArray,
                    Width = width,
                    Height = height,
                    CellSize = gridRO.CellSize,
                    WorldOrigin = gridRO.WorldOrigin,
                }.Run();

                playerPosArray.Dispose();

                // Phase 2: Build directions from distance field (Burst, parallel across all cells)
                new DirectionBuildJob
                {
                    DistanceField = _distanceField,
                    Cells = cells,
                    Width = width,
                    Height = height,
                    CellSize = gridRO.CellSize,
                }.Schedule(totalCells, 64).Complete();

                gridRef.IsBuilt = true;
            }
        }

        /// <summary>
        /// Burst-compiled BFS flood fill from player positions.
        /// Runs on main thread (.Run()) since BFS is sequential, but Burst gives SIMD optimization.
        /// </summary>
        [BurstCompile]
        struct BFSFloodFillJob : IJob
        {
            public NativeArray<float> DistanceField;
            [ReadOnly] public NativeArray<FlowFieldCell> Cells;
            public NativeQueue<int> Queue;
            [ReadOnly] public NativeArray<float3> PlayerPositions;
            public int Width;
            public int Height;
            public float CellSize;
            public float3 WorldOrigin;

            public void Execute()
            {
                int totalCells = Width * Height;

                // Reset distance field
                for (int i = 0; i < totalCells; i++)
                    DistanceField[i] = float.MaxValue;

                // Seed BFS from all player positions
                for (int p = 0; p < PlayerPositions.Length; p++)
                {
                    float3 pos = PlayerPositions[p];
                    int x = (int)math.floor((pos.x - WorldOrigin.x) / CellSize);
                    int z = (int)math.floor((pos.z - WorldOrigin.z) / CellSize);
                    if (x < 0 || x >= Width || z < 0 || z >= Height) continue;
                    int idx = z * Width + x;
                    DistanceField[idx] = 0f;
                    Queue.Enqueue(idx);
                }

                // 8-directional BFS flood fill
                while (Queue.Count > 0)
                {
                    int current = Queue.Dequeue();
                    int cx = current % Width;
                    int cz = current / Width;
                    float currentDist = DistanceField[current];

                    // 4 cardinal neighbors (cost 1.0)
                    TryEnqueue(cx - 1, cz, currentDist + 1f);
                    TryEnqueue(cx + 1, cz, currentDist + 1f);
                    TryEnqueue(cx, cz - 1, currentDist + 1f);
                    TryEnqueue(cx, cz + 1, currentDist + 1f);

                    // 4 diagonal neighbors (cost 1.414)
                    TryEnqueue(cx - 1, cz - 1, currentDist + 1.414f);
                    TryEnqueue(cx + 1, cz - 1, currentDist + 1.414f);
                    TryEnqueue(cx - 1, cz + 1, currentDist + 1.414f);
                    TryEnqueue(cx + 1, cz + 1, currentDist + 1.414f);
                }
            }

            void TryEnqueue(int nx, int nz, float newDist)
            {
                if ((uint)nx >= (uint)Width || (uint)nz >= (uint)Height)
                    return;
                int ni = nz * Width + nx;
                if (Cells[ni].Cost >= 200) return;
                if (newDist < DistanceField[ni])
                {
                    DistanceField[ni] = newDist;
                    Queue.Enqueue(ni);
                }
            }
        }

        /// <summary>
        /// Burst-compiled parallel direction computation.
        /// Each cell independently finds the neighbor with lowest distance and stores the direction.
        /// </summary>
        [BurstCompile]
        struct DirectionBuildJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> DistanceField;
            [NativeDisableParallelForRestriction]
            public NativeArray<FlowFieldCell> Cells;
            public int Width;
            public int Height;
            public float CellSize;

            public void Execute(int i)
            {
                int cx = i % Width;
                int cz = i / Width;

                float bestDist = DistanceField[i];
                int bestX = cx;
                int bestZ = cz;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = cx + dx;
                        int nz = cz + dz;
                        if ((uint)nx >= (uint)Width || (uint)nz >= (uint)Height) continue;
                        int ni = nz * Width + nx;
                        if (DistanceField[ni] < bestDist)
                        {
                            bestDist = DistanceField[ni];
                            bestX = nx;
                            bestZ = nz;
                        }
                    }
                }

                var cell = Cells[i];
                if (bestX != cx || bestZ != cz)
                {
                    float2 dir = new float2(bestX - cx, bestZ - cz);
                    float len = math.length(dir);
                    cell.Direction = len > 0.001f ? dir / len : float2.zero;
                }
                else
                {
                    cell.Direction = float2.zero;
                }
                cell.Distance = bestDist * CellSize;
                Cells[i] = cell;
            }
        }
    }
}
