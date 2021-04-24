using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerApplyPatchesJob<TIndexer> : IJob
        where TIndexer : struct, IIndexer
    {
        public NativeQueue<VoxelizerFindPatchesJob<TIndexer>.PatchedHole> queue;

        public NativeArray3D<Voxel, TIndexer> grid;

        public void Execute()
        {
            while (queue.TryDequeue(out VoxelizerFindPatchesJob<TIndexer>.PatchedHole patch))
            {
                grid[patch.x, patch.y, patch.z] = grid[patch.x, patch.y, patch.z].ModifyEdge(true, patch.edge, patch.intersection.w, patch.intersection.xyz);
            }
        }
    }
}
