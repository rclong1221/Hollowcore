using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using DIG.Voxel.Core;

namespace DIG.Voxel.Jobs
{
    /// <summary>
    /// OPTIMIZATION 10.2.13: Calculate Chunk Density Histogram.
    /// Runs after voxel generation to count solid/air/surface voxels.
    /// </summary>
    [BurstCompile]
    public struct CalculateChunkStatsJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Densities;
        public NativeArray<int> Stats; // [0]=Solid, [1]=Air, [2]=Surface
        
        public void Execute()
        {
            int s = 0;
            int a = 0;
            int surf = 0;
            
            for (int i = 0; i < Densities.Length; i++)
            {
                byte d = Densities[i];
                if (d == 255) s++;
                else if (d == 0) a++;
                else surf++;
            }
            
            Stats[0] = s;
            Stats[1] = a;
            Stats[2] = surf;
        }
    }
}
