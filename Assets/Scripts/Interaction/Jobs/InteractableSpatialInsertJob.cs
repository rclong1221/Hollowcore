using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DIG.Interaction.Jobs
{
    /// <summary>
    /// EPIC 16.1 Phase 1: Parallel job for inserting interactables into the spatial hash grid.
    ///
    /// Each chunk of interactables is processed in parallel, calculating cell indices
    /// and writing to a thread-safe parallel writer for the cell -> entity map.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct InteractableSpatialInsertJob : IJobChunk
    {
        [ReadOnly, NoAlias] public ComponentTypeHandle<LocalTransform> TransformHandle;
        [ReadOnly, NoAlias] public EntityTypeHandle EntityHandle;

        public float CellSize;
        public int GridWidth;
        public int GridHeight;
        public float2 WorldOffset;

        [WriteOnly, NoAlias]
        public NativeParallelMultiHashMap<int, Entity>.ParallelWriter CellToEntitiesWriter;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref TransformHandle);
            var entities = chunk.GetNativeArray(EntityHandle);

            for (int i = 0; i < chunk.Count; i++)
            {
                float3 position = transforms[i].Position;

                int cellIndex = GetCellIndex(position);

                if (cellIndex >= 0)
                {
                    CellToEntitiesWriter.Add(cellIndex, entities[i]);
                }
            }
        }

        private int GetCellIndex(float3 worldPosition)
        {
            float offsetX = worldPosition.x + WorldOffset.x;
            float offsetZ = worldPosition.z + WorldOffset.y;

            int cellX = (int)(offsetX / CellSize);
            int cellZ = (int)(offsetZ / CellSize);

            if (cellX < 0 || cellX >= GridWidth || cellZ < 0 || cellZ >= GridHeight)
                return -1;

            return cellX + cellZ * GridWidth;
        }
    }
}
