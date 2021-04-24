using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Voxel
{
    public struct CustomBrushSdf<TBrushType, TEvaluator> : ISdf, IDisposable
        where TBrushType : struct
        where TEvaluator : struct, IBrushSdfEvaluator<TBrushType>
    {
        private readonly NativeArray<CustomBrushPrimitive<TBrushType>> primitives;
        private readonly TEvaluator evaluator;

        public CustomBrushSdf(NativeArray<CustomBrushPrimitive<TBrushType>> primitives, TEvaluator evaluator)
        {
            this.primitives = primitives;
            this.evaluator = evaluator;
        }

        public float Eval(float3 pos)
        {
            float value = 0.0f;

            for (int i = 0, len = primitives.Length; i < len; i++)
            {
                var primitive = primitives[i];

                //Evaluate primitive
                float primitiveValue = this.evaluator.Eval(primitive, math.mul(primitive.invTransform, new float4(pos, 1.0f)).xyz);

                if (i == 0)
                {
                    value = primitiveValue;
                }
                else
                {
                    //Blend primitive smoothly
                    float h;
                    switch (primitive.operation)
                    {
                        case BrushOperation.Union:
                            h = math.max(primitive.blend - math.abs(primitiveValue - value), 0.0f);
                            value = math.min(value, math.min(primitiveValue, value) - h * h * 0.25f / primitive.blend);
                            break;
                        case BrushOperation.Difference:
                            h = math.max(primitive.blend - math.abs(-primitiveValue - value), 0.0f);
                            value = math.max(value, math.max(-primitiveValue, value) + h * h * 0.25f / primitive.blend);
                            break;
                    }
                }
            }

            return value;
        }

        public float3 Max()
        {
            if (primitives.Length == 0)
            {
                return new float3(0, 0, 0);
            }

            float3 max = new float3(float.MinValue, float.MinValue, float.MinValue);
            float maxBlend = 0;

            for (int i = 0, len = primitives.Length; i < len; i++)
            {
                var primitive = primitives[i];
                float3 localMin = evaluator.Min(primitive);
                float3 localMax = evaluator.Max(primitive);
                for (int mx = 0; mx < 2; mx++)
                {
                    for (int my = 0; my < 2; my++)
                    {
                        for (int mz = 0; mz < 2; mz++)
                        {
                            float3 corner = math.mul(primitive.transform, new float4(
                                mx == 0 ? localMin.x : localMax.x,
                                my == 0 ? localMin.y : localMax.y,
                                mz == 0 ? localMin.z : localMax.z,
                                1.0f
                                )).xyz;
                            max = math.max(max, corner);
                        }
                    }
                }
                maxBlend = math.max(maxBlend, primitive.blend);
            }

            return max + maxBlend;
        }

        public float3 Min()
        {
            if (primitives.Length == 0)
            {
                return new float3(0, 0, 0);
            }

            float3 min = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            float maxBlend = 0;

            for (int i = 0, len = primitives.Length; i < len; i++)
            {
                var primitive = primitives[i];
                float3 localMin = evaluator.Min(primitive);
                float3 localMax = evaluator.Max(primitive);
                for (int mx = 0; mx < 2; mx++)
                {
                    for (int my = 0; my < 2; my++)
                    {
                        for (int mz = 0; mz < 2; mz++)
                        {
                            float3 corner = math.mul(primitive.transform, new float4(
                                mx == 0 ? localMin.x : localMax.x,
                                my == 0 ? localMin.y : localMax.y,
                                mz == 0 ? localMin.z : localMax.z,
                                1.0f
                                )).xyz;
                            min = math.min(min, corner);
                        }
                    }
                }
                maxBlend = math.max(maxBlend, primitive.blend);
            }

            return min - maxBlend;
        }

        public ISdf RenderChild()
        {
            return null;
        }

        public Matrix4x4? RenderingTransform()
        {
            return null;
        }

        public void Dispose()
        {
            primitives.Dispose();
        }
    }
}
