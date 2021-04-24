using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using static CreateVoxelTerrain;

namespace Voxel
{
    /// <summary>
    /// A collection of custom brush primitives that form one custom compound SDF brush-
    /// </summary>
    /// <typeparam name="TBrushType">Custom brush datatype</typeparam>
    /// <typeparam name="TEvaluator"><see cref="IBrushSdfEvaluator{TBrushType}"/> that evaluates the SDF of a given <see cref="TBrushType"/></typeparam>
    public class CustomBrush<TBrushType, TEvaluator>
        where TBrushType : struct
        where TEvaluator : struct, IBrushSdfEvaluator<TBrushType>
    {
        /// <summary>
        /// The brush primitives that this custom brush is made of
        /// </summary>
        public List<CustomBrushPrimitive<TBrushType>> Primitives
        {
            private set;
            get;
        } = new List<CustomBrushPrimitive<TBrushType>>();

        /// <summary>
        /// The evaluator that evaluates the SDF of a given <see cref="TBrushType"/>
        /// </summary>
        public TEvaluator Evaluator
        {
            private set;
            get;
        }

        public CustomBrush(TEvaluator evaluator)
        {
            Evaluator = evaluator;
        }

        /// <summary>
        /// Adds a brush primitive
        /// </summary>
        /// <param name="type">Type of the brush</param>
        /// <param name="operation">CSG operation of the brush</param>
        /// <param name="blend">Smooth blend distance in voxel units</param>
        /// <param name="transform">Transform to be applied to the brush primitive</param>
        public void AddPrimitive(TBrushType type, BrushOperation operation, float blend, float4x4 transform)
        {
            Primitives.Add(new CustomBrushPrimitive<TBrushType>(type, operation, blend, transform));
        }

        /// <summary>
        /// Creates an <see cref="ISdf"/> that returns the values of the compound SDF of this custom brush.
        /// The SDF must be disposed of when it is no longer needed!
        /// </summary>
        /// <param name="allocator">Allocator with which the SDF data should be allocated</param>
        /// <returns></returns>
        public CustomBrushSdf<TBrushType, TEvaluator> CreateSdf(Allocator allocator)
        {
            var nativePrimitives = new NativeArray<CustomBrushPrimitive<TBrushType>>(Primitives.ToArray(), allocator);
            return new CustomBrushSdf<TBrushType, TEvaluator>(nativePrimitives, Evaluator);
        }

        /// <summary>
        /// Returns the SDF type.
        /// Used in the brush renderer.
        /// </summary>
        /// <returns></returns>
        public Type GetSdfType()
        {
            return typeof(CustomBrushSdf<TBrushType, TEvaluator>);
        }
    }
}
