using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerCollectIntersectionsJob : IJob
    {
        [ReadOnly] public NativeStream.Reader stream;
        [ReadOnly] public NativeList<VoxelizerCollectBinsJob.Column> binColumns;

        [WriteOnly] public NativeList<VoxelizerCollectBinsJob.Column> columns;
        [WriteOnly] public NativeList<float4> intersections;

        public void Execute()
        {
            int size = 0;

            for (int s = 0; s < binColumns.Length; s++)
            {
                var binColumn = binColumns[s];

                stream.BeginForEachIndex(s);

                //Read intersection count in the s'th stream
                var numIntersections = stream.Read<ushort>();

                columns.Add(new VoxelizerCollectBinsJob.Column(binColumn.colIndex, size, numIntersections));

                //Transfer all intersections to list
                for (int i = 0; i < numIntersections; i++)
                {
                    intersections.Add(stream.Read<float4>());
                }

                size += numIntersections;

                stream.EndForEachIndex();
            }
        }
    }
}
