using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelPolygonizer
{
    public readonly struct FeatureType2D
    {
        /// <summary>
        /// A regular sharp 2D feature.
        /// </summary>
        public static readonly FeatureType2D SHARP = new FeatureType2D(true, true, true);

        /// <summary>
        /// A sharp 2D feature that is out of bounds but otherwise regular.
        /// </summary>
        public static readonly FeatureType2D SHARP_OUT_OF_BOUNDS = new FeatureType2D(true, false, true);

        /// <summary>
        /// An invalid sharp 2D feature, i.e. when it can't be calculated. E.g. when
        /// the two tangent lines are (nearly) parallel.
        /// </summary>
        public static readonly FeatureType2D INVALID = new FeatureType2D(false, true, false);

        /// <summary>
        /// No sharp feature.
        /// </summary>
        public static readonly FeatureType2D NONE = new FeatureType2D(false, true, true);

        public readonly bool IsSharp;
        public readonly bool IsInBounds;
        public readonly bool IsValid;

        private FeatureType2D(bool isSharp, bool isInBounds, bool isValid)
        {
            this.IsSharp = isSharp;
            this.IsInBounds = isInBounds;
            this.IsValid = isValid;
        }
    }

    public interface ISharpFeatureSolver2D<TCell> where TCell : struct, IVoxelCell
    {
        /// <summary>
        /// Finds the sharp feature on the given face.
        /// This function <b>must</b> always return a feature point, even if there is no sharp feature, since
        /// it is used to resolve internal ambiguity. In case there is no sharp feature e.g. the midpoint between
        /// the edge points can be returned.
        /// </summary>
        /// <param name="cell">The voxel cell.</param>
        /// <param name="cellFace">The voxel cell face.</param>
        /// <param name="pos1">First intersection point.</param>
        /// <param name="normal1">First projected intersection normal.</param>
        /// <param name="pos2">Second intersection point.</param>
        /// <param name="normal2">Second projected intersection normal.</param>
        /// <param name="featureAngle3D">The cosine of the angle between the two 3D normals.</param>
        /// <param name="width">Width of the cell.</param>
        /// <param name="height">Height of the cell.</param>
        /// <param name="feature">
        /// Reconstructed feature point.
        /// This function <b>must</b> always return a feature point, even if there is no sharp feature, since
        /// it is used to resolve internal ambiguity. In case there is no sharp feature e.g. the midpoint between
        /// the edge points can be returned.
        /// </param>
        /// <param name="type">
        /// Feature type that is used to determine which feature points
        /// are used for disambiguation and geometry.
        /// </param>
        void Solve(
            TCell cell, int cellFace,
            float2 pos1, float2 normal1, float2 pos2, float2 normal2, float featureAngle3D,
            float width, float height, out float2 feature, out FeatureType2D type
            );


        /// <summary>
        /// Returns whether the two specified features intersect.
        /// Each feature is made up of three points and form a triangle.
        /// </summary>
        /// <param name="a1">First feature, first position.</param>
        /// <param name="s1">
        /// First feature, second position.
        /// This is the first sharp feature point.
        /// </param>
        /// <param name="s1t">Feature type of the first sharp feature point.</param>
        /// <param name="b1">First feature, third position.</param>
        /// <param name="a2">Second feature, first position.</param>
        /// <param name="s2">
        /// Second feature, second position.
        /// This is the second sharp feature point.
        /// </param>
        /// <param name="s2t">Feature type of the second sharp feature point.</param>
        /// <param name="b2">Second feature, third position.</param>
        /// <returns>Returns whether the two specified features intersect.</returns>
        bool CheckFeatureIntersection(
            float2 a1, float2 s1, FeatureType2D s1t, float2 b1,
            float2 a2, float2 s2, FeatureType2D s2t, float2 b2
            );
    }

    public struct IntersectionSharpFeatureSolver<TCell> : ISharpFeatureSolver2D<TCell> where TCell : struct, IVoxelCell
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Solve(
            TCell cell, int cellFace,
            float2 pos1, float2 normal1, float2 pos2, float2 normal2, float featureAngle3D,
            float width, float height, out float2 feature, out FeatureType2D type)
        {
            //There is a sharp feature, lines need to be intersected
            if (FindLineIntersection(pos1.x, pos1.y, pos1.x + normal1.y, pos1.y - normal1.x, pos2.x, pos2.y, pos2.x + normal2.y, pos2.y - normal2.x, out feature))
            {
                //Lines successfully intersected, potentially sharp feature found.
                type = (feature.x < 0 || feature.x > width || feature.y < 0 || feature.y > height) ? FeatureType2D.SHARP_OUT_OF_BOUNDS : FeatureType2D.SHARP;
                return;
            }
            else
            {
                type = FeatureType2D.INVALID;
            }

            //Lines couldn't be intersected properly, assume a direct line between vertices
            //and place the feature in the middle between the two vertices.
            feature = new float2((pos1.x + pos2.x) / 2.0F, (pos1.y + pos2.y) / 2.0F);
        }

        public static bool FindLineIntersection(float x1, float y1, float x2, float y2,
           float x3, float y3, float x4, float y4,
           out float2 feature)
        {
            float d = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);

            float rx = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / d;
            float ry = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / d;

            if (float.IsNaN(rx) || float.IsInfinity(rx) || float.IsNaN(ry) || float.IsInfinity(ry) || d == 0)
            {
                feature = new float2(0, 0);
                return false;
            }

            feature = new float2(rx, ry);

            return true;
        }

        private bool CheckLineIntersection(float a, float b, float c, float d, float p, float q, float r, float s)
        {
            float det, gamma, lambda;
            det = (c - a) * (s - q) - (r - p) * (d - b);
            if (det == 0)
            {
                return false;
            }
            else
            {
                lambda = ((s - q) * (r - a) + (p - r) * (s - b)) / det;
                gamma = ((b - d) * (r - a) + (c - a) * (s - b)) / det;
                return (-0.001f < lambda && lambda < 1.001f) && (-0.001f < gamma && gamma < 1.001f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckFeatureIntersection(
            float2 a1, float2 s1, FeatureType2D s1t, float2 b1,
            float2 a2, float2 s2, FeatureType2D s2t, float2 b2)
        {
            //This checks if the 2D feature triangles overlap
            //(except for triangle base overlaps which should not happen by definition of the MS cases)
            return CheckLineIntersection(a1.x, a1.y, s1.x, s1.y, a2.x, a2.y, s2.x, s2.y) ||
                    CheckLineIntersection(a1.x, a1.y, s1.x, s1.y, b2.x, b2.y, s2.x, s2.y) ||
                    CheckLineIntersection(b1.x, b1.y, s1.x, s1.y, a2.x, a2.y, s2.x, s2.y) ||
                    CheckLineIntersection(b1.x, b1.y, s1.x, s1.y, b2.x, b2.y, s2.x, s2.y) ||
                    CheckLineIntersection(a1.x, a1.y, b1.x, b1.y, a2.x, a2.y, s2.x, s2.y) ||
                    CheckLineIntersection(a1.x, a1.y, b1.x, b1.y, b2.x, b2.y, s2.x, s2.y) ||
                    CheckLineIntersection(a1.x, a1.y, s1.x, s1.y, a2.x, a2.y, b2.x, b2.y) ||
                    CheckLineIntersection(b1.x, b1.y, s1.x, s1.y, a2.x, a2.y, b2.x, b2.y);
        } 
    }
}