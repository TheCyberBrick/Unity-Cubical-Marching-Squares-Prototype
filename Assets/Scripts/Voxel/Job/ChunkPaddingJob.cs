using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Voxel
{
    [BurstCompile]
    public struct ChunkPaddingJob<TSourceIndexer, TTargetIndexer> : IJob
        where TSourceIndexer : struct, IIndexer
        where TTargetIndexer : struct, IIndexer
    {
        [ReadOnly] public NativeArray3D<Voxel, TSourceIndexer> source;
        [ReadOnly] public int chunkSize;
        [ReadOnly] public int xOff, yOff, zOff;

        [WriteOnly] public NativeArray3D<Voxel, TTargetIndexer> target;

        public void Execute()
        {
            //xOff == 0 ==> xStart =         0, xEnd = chunkSize
            //xOff == 1 ==> xStart = chunkSize, xEnd = chunkSize + 1 
            var xStart = xOff * chunkSize;
            var xEnd = xStart + 1 + (1 - xOff) * (chunkSize - 1);
            var yStart = yOff * chunkSize;
            var yEnd = yStart + 1 + (1 - yOff) * (chunkSize - 1);
            var zStart = zOff * chunkSize;
            var zEnd = zStart + 1 + (1 - zOff) * (chunkSize - 1);

            for (int z = zStart; z < zEnd; z++)
            {
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = xStart; x < xEnd; x++)
                    {
                        target[x, y, z] = source[x % chunkSize, y % chunkSize, z % chunkSize];
                    }
                }
            }
        }
    }
}