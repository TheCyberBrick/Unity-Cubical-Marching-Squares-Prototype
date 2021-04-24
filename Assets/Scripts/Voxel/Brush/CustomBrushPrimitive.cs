using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using static CreateVoxelTerrain;

namespace Voxel
{
    /// <summary>
    /// Representation of a primitive of a custom brush.
    /// </summary>
    /// <typeparam name="TBrushType">Custom brush datatype</typeparam>
    public readonly struct CustomBrushPrimitive<TBrushType>
        where TBrushType : struct
    {
        /// <summary>
        /// Type of the brush, i.e. what kind of SDF it is
        /// </summary>
        public readonly TBrushType type;

        /// <summary>
        /// CSG operation used to apply the SDF of this primitive
        /// </summary>
        public readonly BrushOperation operation;

        /// <summary>
        /// Smooth blend distance in voxel units
        /// </summary>
        public readonly float blend;

        /// <summary>
        /// Transform applied to the SDF
        /// </summary>
        public readonly float4x4 transform;

        /// <summary>
        /// Inverse of <see cref="transform"/>
        /// </summary>
        public readonly float4x4 invTransform;

        internal CustomBrushPrimitive(TBrushType type, BrushOperation operation, float blend, float4x4 transform)
        {
            this.type = type;
            this.operation = operation;
            this.blend = blend;
            this.transform = transform;
            this.invTransform = math.inverse(transform);
        }
    }
}
