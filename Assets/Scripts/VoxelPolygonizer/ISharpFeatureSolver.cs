using Unity.Collections;
using Unity.Mathematics;

namespace VoxelPolygonizer
{
    public interface ISharpFeatureSolver<TCell> where TCell : struct, IVoxelCell
    {
        float3 Solve(TCell cell, NativeList<float3> points, NativeList<float3> normals, bool isEdge, float3 mean);
    }

    public struct MeanSharpFeatureSolver<TCell> : ISharpFeatureSolver<TCell> where TCell : struct, IVoxelCell
    {
        public float3 Solve(TCell cell, NativeList<float3> points, NativeList<float3> normals, bool isEdge, float3 mean)
        {
            return mean;
        }
    } 
}