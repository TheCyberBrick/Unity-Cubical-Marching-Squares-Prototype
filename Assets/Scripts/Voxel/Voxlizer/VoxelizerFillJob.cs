using Voxel.Voxelizer;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerFillJob<TIndexer> : IJob
        where TIndexer : struct, IIndexer
    {
        [ReadOnly] public NativeList<VoxelizerCollectBinsJob.Column> colsX, colsY, colsZ;
        [ReadOnly] public NativeList<float4> intersectionsX, intersectionsY, intersectionsZ;
        [ReadOnly] public int material;
        [ReadOnly] public float angleThreshold;
        [ReadOnly] public float snapThreshold;

        public NativeArray3D<Voxel, TIndexer> grid;

        /// <summary>
        /// Found "holes", i.e. missing intersections & normals. These
        /// need to be patched in a second pass.
        /// </summary>
        [WriteOnly] public NativeList<VoxelizerFillJob<TIndexer>.Hole> holes;

        public readonly struct Hole
        {
            public readonly int x, y, z, edge;
            public readonly bool inside;

            internal Hole(int x, int y, int z, int edge, bool inside)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.edge = edge;
                this.inside = inside;
            }
        }

        private struct IntersectionSorter : IComparer<float4>
        {
            public int Compare(float4 x, float4 y)
            {
                return (int)math.sign(x.w - y.w);
            }
        }

        public void Execute()
        {
            int width = grid.Length(0);
            int height = grid.Length(1);
            int depth = grid.Length(2);

            //Intersect X axis
            for (int l = colsX.Length, j = 0; j < l; j++)
            {
                var col = colsX[j];

                ColumnToPositionX(col.colIndex, height, out int y, out int z);

                //Alternate between inside/outside
                bool inside = false;
                int pix = 0;
                for (int i = 0; i < col.length; i++)
                {
                    var intersection = intersectionsX[col.start + i];
                    var ix = (int)math.floor(intersection.w);

                    if (inside)
                    {
                        //Fill voxel materials
                        for (int x = pix + 1; x <= ix; x++)
                        {
                            grid[x, y, z] = grid[x, y, z].ModifyMaterial(true, material);
                        }
                    }

                    pix = ix;
                    inside = !inside;
                }
            }

            //Intersect Y axis
            for (int l = colsY.Length, j = 0; j < l; j++)
            {
                var col = colsY[j];

                ColumnToPositionY(col.colIndex, width, out int x, out int z);

                //Alternate between inside/outside
                bool inside = false;
                int piy = 0;
                for (int i = 0; i < col.length; i++)
                {
                    var intersection = intersectionsY[col.start + i];
                    var iy = (int)math.floor(intersection.w);

                    if (inside)
                    {
                        //Fill voxel materials
                        for (int y = piy + 1; y <= iy; y++)
                        {
                            grid[x, y, z] = grid[x, y, z].ModifyMaterial(true, material);
                        }
                    }

                    piy = iy;
                    inside = !inside;
                }
            }

            //Intersect Z axis
            for (int l = colsZ.Length, j = 0; j < l; j++)
            {
                var col = colsZ[j];

                ColumnToPositionZ(col.colIndex, width, out int x, out int y);

                //Alternate between inside/outside
                bool inside = false;
                int piz = 0;
                for (int i = 0; i < col.length; i++)
                {
                    var intersection = intersectionsZ[col.start + i];
                    var iz = (int)math.floor(intersection.w);

                    if (inside)
                    {
                        //Fill voxel materials
                        for (int z = piz + 1; z <= iz; z++)
                        {
                            grid[x, y, z] = grid[x, y, z].ModifyMaterial(true, material);
                        }
                    }

                    piz = iz;
                    inside = !inside;
                }
            }

            //Populate X axis
            for (int l = colsX.Length, j = 0; j < l; j++)
            {
                var col = colsX[j];

                ColumnToPositionX(col.colIndex, height, out int y, out int z);

                bool prevSolid = grid[0, y, z].Material == material;

                for (int x = 1; x < width; x++)
                {
                    bool solid = grid[x, y, z].Material == material;

                    if (solid != prevSolid)
                    {
                        float4 closestIntersection = 0;

                        for (int i = 0; i < col.length; i++)
                        {
                            var intersection = intersectionsX[col.start + i];
                            if (math.abs(intersection.w - x + 0.5f) < math.abs(closestIntersection.w - x + 0.5f) && math.abs(math.dot(intersection.xyz, new float3(1, 0, 0))) > angleThreshold)
                            {
                                closestIntersection = intersection;
                            }
                        }

                        if (math.abs(closestIntersection.w - x + 0.5f) < snapThreshold)
                        {
                            grid[x - 1, y, z] = grid[x - 1, y, z].ModifyEdge(true, 0, math.clamp(closestIntersection.w - x + 1, 0, 1), closestIntersection.xyz);
                        }
                        else
                        {
                            holes.Add(new VoxelizerFillJob<TIndexer>.Hole(x - 1, y, z, 0, prevSolid));
                        }
                    }

                    prevSolid = solid;
                }
            }

            //Populate Y axis
            for (int l = colsY.Length, j = 0; j < l; j++)
            {
                var col = colsY[j];

                ColumnToPositionY(col.colIndex, width, out int x, out int z);

                bool prevSolid = grid[x, 0, z].Material == material;

                for (int y = 1; y < height; y++)
                {
                    bool solid = grid[x, y, z].Material == material;

                    if (solid != prevSolid)
                    {
                        float4 closestIntersection = 0;

                        for (int i = 0; i < col.length; i++)
                        {
                            var intersection = intersectionsY[col.start + i];
                            if (math.abs(intersection.w - y + 0.5f) < math.abs(closestIntersection.w - y + 0.5f) && math.abs(math.dot(intersection.xyz, new float3(0, 1, 0))) > angleThreshold)
                            {
                                closestIntersection = intersection;
                            }
                        }

                        if (math.abs(closestIntersection.w - y + 0.5f) < snapThreshold)
                        {
                            grid[x, y - 1, z] = grid[x, y - 1, z].ModifyEdge(true, 1, math.clamp(closestIntersection.w - y + 1, 0, 1), closestIntersection.xyz);
                        }
                        else
                        {
                            holes.Add(new VoxelizerFillJob<TIndexer>.Hole(x, y - 1, z, 1, prevSolid));
                        }
                    }

                    prevSolid = solid;
                }
            }

            //Populate Z axis
            for (int l = colsZ.Length, j = 0; j < l; j++)
            {
                var col = colsZ[j];

                ColumnToPositionZ(col.colIndex, width, out int x, out int y);

                bool prevSolid = grid[x, y, 0].Material == material;

                for (int z = 1; z < depth; z++)
                {
                    bool solid = grid[x, y, z].Material == material;

                    if (solid != prevSolid)
                    {
                        float4 closestIntersection = 0;

                        for (int i = 0; i < col.length; i++)
                        {
                            var intersection = intersectionsZ[col.start + i];
                            if (math.abs(intersection.w - z + 0.5f) < math.abs(closestIntersection.w - z + 0.5f) && math.abs(math.dot(intersection.xyz, new float3(0, 0, 1))) > angleThreshold)
                            {
                                closestIntersection = intersection;
                            }
                        }

                        if (math.abs(closestIntersection.w - z + 0.5f) < snapThreshold)
                        {
                            grid[x, y, z - 1] = grid[x, y, z - 1].ModifyEdge(true, 2, math.clamp(closestIntersection.w - z + 1, 0, 1), closestIntersection.xyz);
                        }
                        else
                        {
                            holes.Add(new VoxelizerFillJob<TIndexer>.Hole(x, y, z - 1, 2, prevSolid));
                        }
                    }

                    prevSolid = solid;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ColumnToPositionX(int colIndex, int height, out int y, out int z)
        {
            y = colIndex % height;
            z = colIndex / height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ColumnToPositionY(int colIndex, int width, out int x, out int z)
        {
            x = colIndex % width;
            z = colIndex / width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ColumnToPositionZ(int colIndex, int width, out int x, out int y)
        {
            x = colIndex % width;
            y = colIndex / width;
        }
    }
}
