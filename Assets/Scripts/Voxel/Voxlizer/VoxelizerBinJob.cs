using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerBinJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> vertices;
        [ReadOnly] public int width, height, depth;

        [WriteOnly] public NativeStream.Writer stream;

        public void Execute(int t)
        {
            var padding = new float3(0.0001f, 0.0001f, 0.0001f);

            var v = t * 3;

            var v1 = vertices[v];
            var v2 = vertices[v + 1];
            var v3 = vertices[v + 2];

            var min = (int3)math.ceil(math.min(v1, math.min(v2, v3)) - padding);
            var max = (int3)math.floor(math.max(v1, math.max(v2, v3)) + padding);

            stream.BeginForEachIndex(t);

            //Assign to X axis bins
            stream.Write((max.z - min.z + 1) * (max.y - min.y + 1));
            for (int z = min.z; z <= max.z; z++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    stream.Write(z * height + y); //Column index
                }
            }

            //Assign to Y axis bins
            stream.Write((max.z - min.z + 1) * (max.x - min.x + 1));
            for (int z = min.z; z <= max.z; z++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    stream.Write(z * width + x); //Column index
                }
            }

            //Assign to Z axis bins
            stream.Write((max.y - min.y + 1) * (max.x - min.x + 1));
            for (int y = min.y; y <= max.y; y++)
            {
                for (int x = min.x; x <= max.x; x++)
                {
                    stream.Write(y * width + x); //Column index
                }
            }

            stream.EndForEachIndex();
        }
    }
}
