using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerFindPatchesJob<TIndexer> : IJobParallelForDefer
        where TIndexer : struct, IIndexer
    {
        [ReadOnly] public NativeArray<float3> vertices;
        [ReadOnly] public NativeArray<float3> normals;
        [ReadOnly] public NativeArray<VoxelizerFillJob<TIndexer>.Hole> holes;
        [ReadOnly] public float angleThreshold;
        [ReadOnly] public bool smoothNormals;

        [WriteOnly] public NativeQueue<PatchedHole>.ParallelWriter queue;

        public readonly struct PatchedHole
        {
            public readonly int x, y, z, edge;
            public readonly float4 intersection;

            internal PatchedHole(int x, int y, int z, int edge, float4 intersection)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.edge = edge;
                this.intersection = intersection;
            }
        }

        public void Execute(int i)
        {
            int numVertices = vertices.Length;

            var hole = holes[i];

            float3 edge;
            float3 corner;
            switch (hole.edge)
            {
                default:
                case 0:
                    corner = new float3(hole.x + (hole.inside ? 0 : 1), hole.y, hole.z);
                    edge = new float3(1, 0, 0);
                    break;
                case 1:
                    corner = new float3(hole.x, hole.y + (hole.inside ? 0 : 1), hole.z);
                    edge = new float3(0, 1, 0);
                    break;
                case 2:
                    corner = new float3(hole.x, hole.y, hole.z + (hole.inside ? 0 : 1));
                    edge = new float3(0, 0, 1);
                    break;
            }

            float closestDistSq = float.MaxValue;
            float3 closestPointOnTriangle = 0;
            float3 closestNormal = 0;
            float3 closestCrossNormal = 0;
            int closestTriangleIndex = 0;

            //Find closest triangle to corner that is missing an intersection & normal
            for (int j = 0; j < numVertices; j += 3)
            {
                var dstSq = PointTriangleDistanceSq(corner, vertices[j], vertices[j + 1], vertices[j + 2], out float3 pointOnTriangle);

                if (dstSq < closestDistSq)
                {
                    closestDistSq = dstSq;
                    closestTriangleIndex = j;
                    closestPointOnTriangle = pointOnTriangle;

                    if (smoothNormals)
                    {
                        Barycentric(pointOnTriangle, vertices[j], vertices[j + 1], vertices[j + 2], out float bu, out float bv, out float bw);
                        closestNormal = math.normalize(bu * normals[j] + bv * normals[j + 1] + bw * normals[j + 2]);

                        closestCrossNormal = math.normalize(math.cross(vertices[j + 2] - vertices[j + 1], vertices[j] - vertices[j + 1]));
                    }
                    else
                    {
                        closestNormal = closestCrossNormal = math.normalize(math.cross(vertices[j + 2] - vertices[j + 1], vertices[j] - vertices[j + 1]));
                    }
                }
            }

            float3 normal;

            //If interpolated normal gets too close to perpendicular to axis
            //then fall back to cross normal
            if (smoothNormals && math.dot(closestNormal, edge) * (hole.inside ? 1 : -1) <= angleThreshold)
            {
                normal = closestCrossNormal;
            }
            else
            {
                normal = closestNormal;
            }

            var intersection = math.clamp(math.dot(edge, closestPointOnTriangle - new float3(hole.x, hole.y, hole.z)), 0, 1);

            queue.Enqueue(new PatchedHole(hole.x, hole.y, hole.z, hole.edge, new float4(normal, intersection)));
        }

        private void Barycentric(float3 p, float3 a, float3 b, float3 c, out float u, out float v, out float w)
        {
            float3 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = math.dot(v0, v0);
            float d01 = math.dot(v0, v1);
            float d11 = math.dot(v1, v1);
            float d20 = math.dot(v2, v0);
            float d21 = math.dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            v = (d11 * d20 - d01 * d21) / denom;
            w = (d00 * d21 - d01 * d20) / denom;
            u = 1.0f - v - w;
        }

        private float PointSegmentDistanceSq(float3 x0, float3 x1, float3 x2, out float3 r)
        {
            float3 dx = x2 - x1;
            float m2 = math.lengthsq(dx);
            float s12 = math.dot(x2 - x0, dx) / m2;
            if (s12 < 0)
            {
                s12 = 0;
            }
            else
            {
                s12 = 1;
            }
            r = s12 * x1 + (1 - s12) * x2;
            return math.distancesq(x0, r);
        }

        private float PointTriangleDistanceSq(float3 x0, float3 x1, float3 x2, float3 x3, out float3 r)
        {
            float3 x13 = x1 - x3;
            float3 x23 = x2 - x3;
            float3 x03 = x0 - x3;
            float m13 = math.lengthsq(x13);
            float m23 = math.lengthsq(x23);
            float d = math.dot(x13, x23);
            float invdet = 1.0f / math.max(m13 * m23 - d * d, 1e-30f);
            float a = math.dot(x13, x03);
            float b = math.dot(x23, x03);
            float w23 = invdet * (m23 * a - d * b);
            float w31 = invdet * (m13 * b - d * a);
            float w12 = 1.0f - w23 - w31;
            if (w23 >= 0 && w31 >= 0 && w12 >= 0)
            {
                r = w23 * x1 + w31 * x2 + w12 * x3;
                return math.distancesq(x0, r);
            }
            else
            {
                if (w23 > 0)
                {
                    float d1 = PointSegmentDistanceSq(x0, x1, x2, out float3 r1);
                    float d2 = PointSegmentDistanceSq(x0, x1, x3, out float3 r2);

                    if (d1 < d2)
                    {
                        r = r1;
                        return d1;
                    }
                    else
                    {
                        r = r2;
                        return d2;
                    }
                }
                else if (w31 > 0)
                {
                    float d1 = PointSegmentDistanceSq(x0, x1, x2, out float3 r1);
                    float d2 = PointSegmentDistanceSq(x0, x2, x3, out float3 r2);

                    if (d1 < d2)
                    {
                        r = r1;
                        return d1;
                    }
                    else
                    {
                        r = r2;
                        return d2;
                    }
                }
                else
                {
                    float d1 = PointSegmentDistanceSq(x0, x1, x3, out float3 r1);
                    float d2 = PointSegmentDistanceSq(x0, x2, x3, out float3 r2);

                    if (d1 < d2)
                    {
                        r = r1;
                        return d1;
                    }
                    else
                    {
                        r = r2;
                        return d2;
                    }
                }
            }
        }
    }
}
