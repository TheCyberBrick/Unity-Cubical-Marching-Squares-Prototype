using Unity.Collections;
using Unity.Mathematics;
using VoxelPolygonizer;

namespace Voxel
{
    public struct SvdQefSolver<TCell> : ISharpFeatureSolver<TCell> where TCell : struct, IVoxelCell
    {
        public bool Clamp { get; set; }

        public float3 Solve(TCell cell, NativeList<float3> points, NativeList<float3> normals, bool isEdge, float3 mean)
        {
            float4 pointaccum = new float4(0.0f);
            float3x3 ATA = new float3x3(0.0f);
            float3 ATb = new float3(0.0f);

            for (int i = 0; i < points.Length; i++)
            {
                qef_add(normals[i], points[i] - mean, ref ATA, ref ATb, ref pointaccum);
            }

            float error = qef_solve(ATA, ATb, pointaccum, out float3 result, isEdge);

            float3 feature = result + mean;

            if (!Clamp)
            {
                return feature;
            }

            var cellPos = cell.GetInfo(0).Position;

            if (feature.x >= cellPos.x - 0.1f && feature.x < cellPos.x + cell.GetWidth() + 0.1f &&
                feature.y >= cellPos.y - 0.1f && feature.y < cellPos.y + cell.GetHeight() + 0.1f &&
                feature.z >= cellPos.z - 0.1f && feature.z < cellPos.z + cell.GetDepth() + 0.1f)
            {
                return feature;
            }
            else
            {
                return mean;
            }
        }

        //Credit for QEF solver goes to nickgildea, thanks!
        //https://github.com/nickgildea/qef

        private void givens_coeffs_sym(float a_pp, float a_pq, float a_qq, out float c, out float s)
        {
            if (a_pq == 0.0f)
            {
                c = 1.0f;
                s = 0.0f;
                return;
            }
            float tau = (a_qq - a_pp) / (2.0f * a_pq);
            float stt = math.sqrt(1.0f + tau * tau);
            float tan = 1.0f / ((tau >= 0.0) ? (tau + stt) : (tau - stt));
            c = math.rsqrt(1.0f + tan * tan);
            s = tan * c;
        }

        private void svd_rotate_xy(ref float x, ref float y, in float c, in float s)
        {
            float u = x; float v = y;
            x = c * u - s * v;
            y = s * u + c * v;
        }

        private void svd_rotateq_xy(ref float x, ref float y, ref float a, in float c, in float s)
        {
            float cc = c * c; float ss = s * s;
            float mx = 2.0f * c * s * a;
            float u = x; float v = y;
            x = cc * u - mx + ss * v;
            y = ss * u + mx + cc * v;
        }

        private void svd_rotate(ref float3x3 vtav, ref float3x3 v, in int a, in int b)
        {
            if (vtav[a][b] == 0.0f) return;

            givens_coeffs_sym(vtav[a][a], vtav[a][b], vtav[b][b], out float c, out float s);
            float vtav_a_a = vtav[a][a];
            float vtav_b_b = vtav[b][b];
            float vtav_a_b = vtav[a][b];
            svd_rotateq_xy(ref vtav_a_a, ref vtav_b_b, ref vtav_a_b, c, s);
            vtav[a][a] = vtav_a_a;
            vtav[b][b] = vtav_b_b;
            vtav[a][b] = vtav_a_b;
            float vtav_0_3mb = vtav[0][3 - b];
            float vtav_1ma_2 = vtav[1 - a][2];
            svd_rotate_xy(ref vtav_0_3mb, ref vtav_1ma_2, c, s);
            vtav[0][3 - b] = vtav_0_3mb;
            vtav[1 - a][2] = vtav_1ma_2;
            vtav[a][b] = 0.0f;

            float v_0_a = v[0][a];
            float v_0_b = v[0][b];
            svd_rotate_xy(ref v_0_a, ref v_0_b, c, s);
            v[0][a] = v_0_a;
            v[0][b] = v_0_b;
            float v_1_a = v[1][a];
            float v_1_b = v[1][b];
            svd_rotate_xy(ref v_1_a, ref v_1_b, c, s);
            v[1][b] = v_1_b;
            v[1][a] = v_1_a;
            float v_2_a = v[2][a];
            float v_2_b = v[2][b];
            svd_rotate_xy(ref v_2_a, ref v_2_b, c, s);
            v[2][a] = v_2_a;
            v[2][b] = v_2_b;
        }

        const int SVD_NUM_SWEEPS = 5;

        private void svd_solve_sym(in float3x3 a, out float3 sigma, ref float3x3 v)
        {
            // assuming that A is symmetric: can optimize all operations for 
            // the upper right triagonal
            float3x3 vtav = a;
            // assuming V is identity: you can also pass a matrix the rotations
            // should be applied to
            // U is not computed
            for (int i = 0; i < SVD_NUM_SWEEPS; ++i)
            {
                svd_rotate(ref vtav, ref v, 0, 1);
                svd_rotate(ref vtav, ref v, 0, 2);
                svd_rotate(ref vtav, ref v, 1, 2);
            }
            sigma = new float3(vtav[0][0], vtav[1][1], vtav[2][2]);
        }

        private float svd_invdet(float x, float tol)
        {
            return (math.abs(x) < tol || math.abs(1.0 / x) < tol) ? 0.0f : (1.0f / x);
        }

        private void svd_pseudoinverse(out float3x3 o, float3 sigma, in float3x3 v, bool zeroSmallesSv)
        {
            if (zeroSmallesSv)
            {
                float smallest = float.MaxValue;
                int smallestIndex = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (i == 0 || sigma[i] < smallest)
                    {
                        smallest = sigma[i];
                        smallestIndex = i;
                    }
                }
                sigma[smallestIndex] = 0;
            }
            const float pInvThreshold = 0.01f;
            float singularValueThreshold = pInvThreshold * math.max(math.abs(sigma[0]), math.max(math.abs(sigma[1]), math.abs(sigma[2])));
            float d0 = svd_invdet(sigma[0], singularValueThreshold);
            float d1 = svd_invdet(sigma[1], singularValueThreshold);
            float d2 = svd_invdet(sigma[2], singularValueThreshold);
            o = new float3x3(v[0][0] * d0 * v[0][0] + v[0][1] * d1 * v[0][1] + v[0][2] * d2 * v[0][2],
                     v[0][0] * d0 * v[1][0] + v[0][1] * d1 * v[1][1] + v[0][2] * d2 * v[1][2],
                     v[0][0] * d0 * v[2][0] + v[0][1] * d1 * v[2][1] + v[0][2] * d2 * v[2][2],
                     v[1][0] * d0 * v[0][0] + v[1][1] * d1 * v[0][1] + v[1][2] * d2 * v[0][2],
                     v[1][0] * d0 * v[1][0] + v[1][1] * d1 * v[1][1] + v[1][2] * d2 * v[1][2],
                     v[1][0] * d0 * v[2][0] + v[1][1] * d1 * v[2][1] + v[1][2] * d2 * v[2][2],
                     v[2][0] * d0 * v[0][0] + v[2][1] * d1 * v[0][1] + v[2][2] * d2 * v[0][2],
                     v[2][0] * d0 * v[1][0] + v[2][1] * d1 * v[1][1] + v[2][2] * d2 * v[1][2],
                     v[2][0] * d0 * v[2][0] + v[2][1] * d1 * v[2][1] + v[2][2] * d2 * v[2][2]);
        }

        private void svd_solve_ATA_ATb(in float3x3 ATA, in float3 ATb, out float3 x, bool zeroSmallesSv)
        {
            float3x3 V = new float3x3(0.0f);
            V[0][0] = 1.0f;
            V[1][1] = 1.0f;
            V[2][2] = 1.0f;

            svd_solve_sym(ATA, out float3 sigma, ref V);

            // A = UEV^T; U = A / (E*V^T)

            svd_pseudoinverse(out float3x3 Vinv, sigma, V, zeroSmallesSv);
            x = math.mul(Vinv, ATb);
        }

        private float3 svd_vmul_sym(in float3x3 a, in float3 v)
        {
            return new float3(
                math.dot(a[0], v),
                (a[0][1] * v.x) + (a[1][1] * v.y) + (a[1][2] * v.z),
                (a[0][2] * v.x) + (a[1][2] * v.y) + (a[2][2] * v.z)
            );
        }

        private void svd_mul_ata_sym(out float3x3 o, in float3x3 a)
        {
            o = new float3x3(0.0f);
            o[0][0] = a[0][0] * a[0][0] + a[1][0] * a[1][0] + a[2][0] * a[2][0];
            o[0][1] = a[0][0] * a[0][1] + a[1][0] * a[1][1] + a[2][0] * a[2][1];
            o[0][2] = a[0][0] * a[0][2] + a[1][0] * a[1][2] + a[2][0] * a[2][2];
            o[1][1] = a[0][1] * a[0][1] + a[1][1] * a[1][1] + a[2][1] * a[2][1];
            o[1][2] = a[0][1] * a[0][2] + a[1][1] * a[1][2] + a[2][1] * a[2][2];
            o[2][2] = a[0][2] * a[0][2] + a[1][2] * a[1][2] + a[2][2] * a[2][2];
        }

        private void svd_solve_Ax_b(in float3x3 a, in float3 b, out float3x3 ATA, out float3 ATb, out float3 x, bool zeroSmallesSv)
        {
            svd_mul_ata_sym(out ATA, a);
            ATb = math.mul(b, a); // transpose(a) * b;
            svd_solve_ATA_ATb(ATA, ATb, out x, zeroSmallesSv);
        }

        private void qef_add(in float3 n, in float3 p, ref float3x3 ATA, ref float3 ATb, ref float4 pointaccum)
        {

            ATA[0][0] += n.x * n.x;
            ATA[0][1] += n.x * n.y;
            ATA[0][2] += n.x * n.z;
            ATA[1][1] += n.y * n.y;
            ATA[1][2] += n.y * n.z;
            ATA[2][2] += n.z * n.z;

            float b = math.dot(p, n);
            ATb += n * b;
            pointaccum += new float4(p, 1.0f);
        }

        private float qef_calc_error(in float3x3 A, in float3 x, in float3 b)
        {
            float3 vtmp = b - svd_vmul_sym(A, x);
            return math.dot(vtmp, vtmp);
        }

        private float qef_solve(in float3x3 ATA, float3 ATb, in float4 pointaccum, out float3 x, bool zeroSmallesSv)
        {
            float3 masspoint = pointaccum.xyz / pointaccum.w;
            ATb -= svd_vmul_sym(ATA, masspoint);
            svd_solve_ATA_ATb(ATA, ATb, out x, zeroSmallesSv);
            float result = qef_calc_error(ATA, x, ATb);

            x += masspoint;

            return result;
        }
    }
}