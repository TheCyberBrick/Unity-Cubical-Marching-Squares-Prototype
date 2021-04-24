using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Mathematics;

namespace Voxel
{
    public struct DefaultCustomBrushSdfEvaluator : IBrushSdfEvaluator<DefaultCustomBrushType>
    {
        private static readonly float BASE_SIZE = 5.0f;

        public float Eval(CustomBrushPrimitive<DefaultCustomBrushType> primitive, float3 pos)
        {
            if(primitive.type == DefaultCustomBrushType.BOX)
            {
                return new BoxSDF(BASE_SIZE).Eval(pos);
            }
            else if(primitive.type == DefaultCustomBrushType.SPHERE)
            {
                return new SphereSDF(BASE_SIZE).Eval(pos);
            }
            return 0.0f;
        }

        public float3 Max(CustomBrushPrimitive<DefaultCustomBrushType> primitive)
        {
            if (primitive.type == DefaultCustomBrushType.BOX)
            {
                return new BoxSDF(BASE_SIZE).Max();
            }
            else if (primitive.type == DefaultCustomBrushType.SPHERE)
            {
                return new SphereSDF(BASE_SIZE).Max();
            }
            return 0;
        }

        public float3 Min(CustomBrushPrimitive<DefaultCustomBrushType> primitive)
        {
            if (primitive.type == DefaultCustomBrushType.BOX)
            {
                return new BoxSDF(BASE_SIZE).Min();
            }
            else if (primitive.type == DefaultCustomBrushType.SPHERE)
            {
                return new SphereSDF(BASE_SIZE).Min();
            }
            return 0;
        }

        [BurstDiscard]
        public ISdf GetRenderSdf(CustomBrushPrimitive<DefaultCustomBrushType> primitive)
        {
            if (primitive.type == DefaultCustomBrushType.BOX)
            {
                return new BoxSDF(BASE_SIZE);
            }
            else if (primitive.type == DefaultCustomBrushType.SPHERE)
            {
                return new SphereSDF(BASE_SIZE);
            }
            return null;
        }
    }
}
