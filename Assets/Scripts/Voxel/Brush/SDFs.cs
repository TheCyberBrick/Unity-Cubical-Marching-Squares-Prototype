using Unity.Mathematics;
using UnityEngine;

namespace Voxel
{
    readonly struct OffsetSDF<TSdf> : ISdf
        where TSdf : struct, ISdf
    {
        private readonly float3 offset;
        private readonly TSdf sdf;

        public OffsetSDF(float3 offset, TSdf sdf)
        {
            this.offset = offset;
            this.sdf = sdf;
        }

        public float Eval(float3 pos)
        {
            return sdf.Eval(pos + offset);
        }

        public float3 Max()
        {
            return sdf.Max() - offset;
        }

        public float3 Min()
        {
            return sdf.Min() - offset;
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.identity;
        }

        public ISdf RenderChild()
        {
            return null;
        }
    }

    readonly struct BoxSDF : ISdf
    {
        private readonly float radius;

        public BoxSDF(float radius)
        {
            this.radius = radius;
        }

        public float Eval(float3 pos)
        {
            float dx = Mathf.Abs(pos.x) - radius;
            float dy = Mathf.Abs(pos.y) - radius;
            float dz = Mathf.Abs(pos.z) - radius;
            return new Vector3(Mathf.Max(dx, 0), Mathf.Max(dy, 0), Mathf.Max(dz, 0)).magnitude + Mathf.Min(Mathf.Max(dx, Mathf.Max(dy, dz)), 0);
        }

        public float3 Max()
        {
            return new float3(radius, radius, radius);
        }

        public float3 Min()
        {
            return new float3(-radius, -radius, -radius);
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.Scale(Vector3.one * radius);
        }

        public ISdf RenderChild()
        {
            return null;
        }
    }

    readonly struct SphereSDF : ISdf
    {
        private readonly float radius;

        public SphereSDF(float radius)
        {
            this.radius = radius;
        }

        public float Eval(float3 pos)
        {
            return math.length(pos) - radius;
        }

        public float3 Max()
        {
            return new float3(radius, radius, radius);
        }

        public float3 Min()
        {
            return new float3(-radius, -radius, -radius);
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.Scale(Vector3.one * radius);
        }

        public ISdf RenderChild()
        {
            return null;
        }
    }

    readonly struct CylinderSDF : ISdf
    {
        private readonly float height, radius;

        public CylinderSDF(float height, float radius)
        {
            this.height = height;
            this.radius = radius;
        }

        public float Eval(float3 pos)
        {
            var d = math.abs(new float2(math.length(pos.xz), pos.y)) - new float2(radius, height);
            return math.min(math.max(d.x, d.y), 0.0f) + math.length(math.max(d, 0.0f));
        }

        public float3 Max()
        {
            return new float3(radius, height, radius);
        }

        public float3 Min()
        {
            return new float3(-radius, -height, -radius);
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.Scale(new Vector3(radius, height, radius));
        }

        public ISdf RenderChild()
        {
            return null;
        }
    }

    readonly struct PyramidSDF : ISdf
    {
        private readonly float h, b;
        private readonly float h2, b2;

        public PyramidSDF(float h, float b)
        {
            this.b2 = b;
            this.h2 = h / b;
            this.b = b * 1.1f;
            this.h = h / this.b + 0.1f;
        }

        public float Eval(float3 p)
        {
            p = p / this.b;
            p = p + new float3(0, 0.1f, 0);

            float m2 = h * h + 0.25f;

            // symmetry
            /*p.xz = math.abs(p.xz);
            p.xz = (p.z > p.x) ? p.zx : p.xz;
            p.xz -= 0.5f;*/
            var a1 = math.abs(p.xz);
            p = new float3(a1.x, p.y, a1.y);
            var a2 = (p.z > p.x) ? p.zx : p.xz;
            p = new float3(a2.x, p.y, a2.y);
            p = p - new float3(0.5f, 0, 0.5f);

            // project into face plane (2D)
            var q = new float3(p.z, h * p.y - 0.5f * p.x, h * p.x + 0.5f * p.y);

            float s = math.max(-q.x, 0.0f);
            float t = math.clamp((q.y - 0.5f * p.z) / (m2 + 0.25f), 0.0f, 1.0f);

            float a = m2 * (q.x + s) * (q.x + s) + q.y * q.y;
            float b = m2 * (q.x + 0.5f * t) * (q.x + 0.5f * t) + (q.y - m2 * t) * (q.y - m2 * t);

            float d2 = math.min(q.y, -q.x * m2 - q.y * 0.5f) > 0.0f ? 0.0f : math.min(a, b);

            // recover 3D and scale, and add sign
            return math.max(math.sqrt((d2 + q.z * q.z) / m2) * math.sign(math.max(q.z, -p.y)), -(p.y - 0.1f)) * this.b;
        }

        public float3 Max()
        {
            return new float3(h2 * b2, h2 * b2, h2 * b2);
        }

        public float3 Min()
        {
            return new float3(-h2 * b2, -h2 * b2, -h2 * b2);
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.Scale(new Vector3(h2 * b2, h2 * b2, h2 * b2));
        }

        public ISdf RenderChild()
        {
            return null;
        }
    }

    readonly struct TransformSDF<TSdf> : ISdf
        where TSdf : struct, ISdf
    {
        private readonly float4x4 transform;
        private readonly float4x4 invTransform;
        private readonly TSdf sdf;

        public TransformSDF(float4x4 transform, TSdf sdf)
        {
            this.invTransform = math.inverse(transform);
            this.transform = transform;
            this.sdf = sdf;
        }

        public TransformSDF(float4x4 transform, float4x4 invTransform, TSdf sdf)
        {
            this.invTransform = invTransform;
            this.transform = transform;
            this.sdf = sdf;
        }

        public float Eval(float3 pos)
        {
            float3 point = math.mul(invTransform, new float4(pos, 1.0f)).xyz;
            return sdf.Eval(point);
        }

        public float3 Max()
        {
            float3 min = sdf.Min();
            float3 max = sdf.Max();
            float3 worldMax = new float3(float.MinValue, float.MinValue, float.MinValue);
            for (int mx = 0; mx < 2; mx++)
            {
                for (int my = 0; my < 2; my++)
                {
                    for (int mz = 0; mz < 2; mz++)
                    {
                        float3 corner = math.mul(transform, new float4(
                            mx == 0 ? min.x : max.x,
                            my == 0 ? min.y : max.y,
                            mz == 0 ? min.z : max.z,
                            1.0f
                            )).xyz;
                        if (corner.x > worldMax.x)
                        {
                            worldMax.x = corner.x;
                        }
                        if (corner.y > worldMax.y)
                        {
                            worldMax.y = corner.y;
                        }
                        if (corner.z > worldMax.z)
                        {
                            worldMax.z = corner.z;
                        }
                    }
                }
            }
            return worldMax;
        }

        public float3 Min()
        {
            float3 min = sdf.Min();
            float3 max = sdf.Max();
            float3 worldMin = new float3(float.MaxValue, float.MaxValue, float.MaxValue);
            for (int mx = 0; mx < 2; mx++)
            {
                for (int my = 0; my < 2; my++)
                {
                    for (int mz = 0; mz < 2; mz++)
                    {
                        Vector3 corner = math.mul(transform, new float4(
                            mx == 0 ? min.x : max.x,
                            my == 0 ? min.y : max.y,
                            mz == 0 ? min.z : max.z,
                            1.0f
                            )).xyz;
                        if (corner.x < worldMin.x)
                        {
                            worldMin.x = corner.x;
                        }
                        if (corner.y < worldMin.y)
                        {
                            worldMin.y = corner.y;
                        }
                        if (corner.z < worldMin.z)
                        {
                            worldMin.z = corner.z;
                        }
                    }
                }
            }
            return worldMin;
        }

        public Matrix4x4? RenderingTransform()
        {
            return transform;
        }

        public ISdf RenderChild()
        {
            return sdf;
        }
    }

    readonly struct ScaleSDF<TSdf> : ISdf
        where TSdf : struct, ISdf
    {
        private readonly float scale;
        private readonly TSdf sdf;

        public ScaleSDF(float scale, TSdf sdf)
        {
            this.scale = scale;
            this.sdf = sdf;
        }

        public float Eval(float3 pos)
        {
            return sdf.Eval(pos / scale) * scale;
        }

        public float3 Max()
        {
            return sdf.Max() * scale;
        }

        public float3 Min()
        {
            return sdf.Min() * scale;
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.Scale(new Vector3(scale, scale, scale));
        }

        public ISdf RenderChild()
        {
            return sdf;
        }
    }

    readonly struct PerlinSDF : ISdf
    {
        private readonly Vector2 sampleOffset;
        private readonly Vector3 min, max;
        private readonly Vector2 scale;
        private readonly float amplitude;
        private readonly int octaves;
        private readonly float octaveScale, octaveAmplitude;

        public PerlinSDF(Vector3 min, Vector3 max, Vector2 sampleOffset, Vector2 scale, float amplitude, int octaves, float octaveScale, float octaveAmplitude)
        {
            this.sampleOffset = sampleOffset;
            this.min = min;
            this.max = max;
            this.scale = scale;
            this.amplitude = amplitude;
            this.octaves = octaves;
            this.octaveScale = octaveScale;
            this.octaveAmplitude = octaveAmplitude;
        }

        private float CalculateNoise(float x, float y)
        {
            float noise = 0.0f;
            Vector2 scale = this.scale;
            float amplitude = this.amplitude;
            for (int i = 0; i < octaves; i++)
            {
                noise += Mathf.PerlinNoise((float)x * scale.x, (float)y * scale.y) * amplitude;
                scale *= this.octaveScale;
                amplitude *= this.octaveAmplitude;
            }
            return noise;
        }

        public float Eval(float3 pos)
        {
            float dx = Mathf.Abs((float)pos.x) - (max.x - min.x);
            float dy = Mathf.Abs((float)pos.y) - (max.y - min.y);
            float dz = Mathf.Abs((float)pos.z) - (max.z - min.z);
            float distFromBounds = new Vector3(Mathf.Max(dx, 0), Mathf.Max(dy, 0), Mathf.Max(dz, 0)).magnitude + Mathf.Min(Mathf.Max(dx, Mathf.Max(dy, dz)), 0);
            float distFromNoise = (float)pos.y - CalculateNoise((float)pos.x - min.x + sampleOffset.x, (float)pos.z - min.z + sampleOffset.y);
            return Mathf.Max(distFromBounds, distFromNoise);
        }

        public float3 Max()
        {
            return max;
        }

        public float3 Min()
        {
            return min;
        }

        public Matrix4x4? RenderingTransform()
        {
            return Matrix4x4.identity;
        }

        public ISdf RenderChild()
        {
            return null;
        }
    }
}