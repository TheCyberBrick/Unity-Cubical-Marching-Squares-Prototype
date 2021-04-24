using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// This interface represents a Signed Distance Field (SDF). The signed distance field
    /// can be evaluated at any 3D position. The resulting value is the distance from the surface
    /// represented by the SDF (or a close approximation). Values inside the surface are negative, values outside
    /// the surface are positive.
    /// Additionally this interface also provides the axis aligned min and max bounds of the SDF.
    /// </summary>
    public interface ISdf
    {
        float Eval(float3 pos);
        float3 Min();
        float3 Max();

        /// <summary>
        /// The transform to be applied before rendering
        /// </summary>
        /// <returns></returns>
        [BurstDiscard]
        Matrix4x4? RenderingTransform();

        /// <summary>
        /// Returns the underlying child SDF. If a non-null value is returned then
        /// not this SDF but instead the returned SDF is rendered. Useful for space transforming "adapter" SDFs,
        /// e.g. such that offset or rotate an underlying SDF.
        /// </summary>
        /// <returns></returns>
        [BurstDiscard]
        ISdf RenderChild();
    }
}