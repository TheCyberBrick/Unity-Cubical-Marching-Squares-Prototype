using Unity.Mathematics;

namespace Voxel
{
    public class SdfDerivative
    {
        public static float3 FirstOrderCentralFiniteDifferenceNormalized<TSdf>(float3 pos, float epsilon, TSdf sdf)
            where TSdf : struct, ISdf
        {
            float3 normal = new float3(
                sdf.Eval(pos + new float3(epsilon, 0, 0)) - sdf.Eval(pos - new float3(epsilon, 0, 0)),
                sdf.Eval(pos + new float3(0, epsilon, 0)) - sdf.Eval(pos - new float3(0, epsilon, 0)),
                sdf.Eval(pos + new float3(0, 0, epsilon)) - sdf.Eval(pos - new float3(0, 0, epsilon))
                );
            normal = math.normalize(normal);
            return normal;
        }
    }
}