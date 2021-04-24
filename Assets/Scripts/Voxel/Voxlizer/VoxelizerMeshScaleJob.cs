using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerMeshScaleJob : IJob
    {
        /// <summary>
        /// Vertices of the mesh to voxelize. 3 consecutive vertices form one triangle.
        /// </summary>
        [ReadOnly] public NativeArray<float3> inVertices;
        [ReadOnly] public int width, height, depth;
        [ReadOnly] public float padding;

        [WriteOnly] public NativeArray<float3> outVertices;

        public void Execute()
        {
            var numVerts = inVertices.Length;

            var center = new float3(width / 2.0f, height / 2.0f, depth / 2.0f);

            float3 maxBounds = 0.0f;
            float3 minBounds = 0.0f;
            for (int l = inVertices.Length, i = 0; i < l; i++)
            {
                maxBounds = math.max(inVertices[i], maxBounds);
                minBounds = math.min(inVertices[i], minBounds);
            }
            float3 midBounds = new float3((maxBounds.x + minBounds.x) / 2.0f, (maxBounds.y + minBounds.y) / 2.0f, (maxBounds.z + minBounds.z) / 2.0f);

            float3 maxDist = 0.0f;
            for (int l = inVertices.Length, i = 0; i < l; i++)
            {
                var dif = inVertices[i] - midBounds;
                maxDist = math.max(new float3(dif.x * dif.x, dif.y * dif.y, dif.z * dif.z), maxDist);
            }
            maxDist = math.sqrt(maxDist);
            float3 scales = new float3((width / 2.0f - padding) / maxDist.x, (height / 2.0f - padding) / maxDist.y, (depth / 2.0f - padding) / maxDist.z);
            float scale = math.cmin(scales);

            for (int l = inVertices.Length, i = 0; i < l; i++)
            {
                outVertices[i] = (inVertices[i] - midBounds) * scale + center;
            }
        }
    }
}
