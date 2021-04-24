using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel
{
    [BurstCompile]
    public struct ChunkSdfJob<TSdf, TIndexer> : IJob
        where TSdf : struct, ISdf
        where TIndexer : struct, IIndexer
    {
        [ReadOnly] public float3 origin;

        [ReadOnly] public TSdf sdf;

        [ReadOnly] public int material;

        [ReadOnly] public bool replace;

        [ReadOnly] public NativeArray3D<Voxel, TIndexer> snapshot;

        public NativeArray<bool> changed; //Using array so that result can be read even when job is copied

        public NativeArray3D<Voxel, TIndexer> outVoxels;

        public void Execute()
        {
            changed[0] = ApplySdf(snapshot, origin, sdf, material, replace, outVoxels, Allocator.Temp);
        }

        public static bool ApplySdf(NativeArray3D<Voxel, TIndexer> snapshot, float3 origin, TSdf sdf, int material, bool replace, NativeArray3D<Voxel, TIndexer> outVoxels, Allocator allocator)
        {
            int chunkSize = snapshot.Length(0) - 1;

            float3 minBound = math.floor(origin + sdf.Min());
            float3 maxBound = math.ceil(origin + sdf.Max());

            snapshot.CopyTo(outVoxels);

            var evaluatedSdf = new NativeArray3D<float, LinearIndexer>(new LinearIndexer(chunkSize + 1, chunkSize + 1, chunkSize + 1), chunkSize + 1, chunkSize + 1, chunkSize + 1, allocator);

            bool changed = false;

            //Apply materials
            for (int z = (int)minBound.z; z <= maxBound.z + 1; z++)
            {
                for (int y = (int)minBound.y; y <= maxBound.y + 1; y++)
                {
                    for (int x = (int)minBound.x; x <= maxBound.x + 1; x++)
                    {
                        if (x >= 0 && x < chunkSize + 1 && y >= 0 && y < chunkSize + 1 && z >= 0 && z < chunkSize + 1)
                        {
                            bool isInside = (evaluatedSdf[x, y, z] = sdf.Eval(new float3(x, y, z) - origin)) < 0;

                            if (isInside)
                            {
                                if (replace && outVoxels[x, y, z].Material != 0)
                                {
                                    //TODO Does this need intersection value checks?
                                    outVoxels[x, y, z] = outVoxels[x, y, z].ModifyMaterial(true, material);
                                    changed = true;
                                }
                                else if (!replace && outVoxels[x, y, z].Material != material)
                                {
                                    outVoxels[x, y, z] = outVoxels[x, y, z].ModifyMaterial(true, material);
                                    changed = true;
                                }
                            }
                        }
                    }
                }
            }

            //Apply intersections and normals in a second pass, such that they aren't unnecessarily
            //asigned to edges with no material change
            for (int z = (int)minBound.z; z <= maxBound.z; z++)
            {
                for (int y = (int)minBound.y; y <= maxBound.y; y++)
                {
                    for (int x = (int)minBound.x; x <= maxBound.x; x++)
                    {
                        if (x >= 0 && x < chunkSize && y >= 0 && y < chunkSize && z >= 0 && z < chunkSize)
                        {
                            ApplySdfIntersection(snapshot, origin, x, y, z, chunkSize, 0, 1, 0, 0, sdf, evaluatedSdf, material, replace, outVoxels);
                            ApplySdfIntersection(snapshot, origin, x, y, z, chunkSize, 1, 0, 1, 0, sdf, evaluatedSdf, material, replace, outVoxels);
                            ApplySdfIntersection(snapshot, origin, x, y, z, chunkSize, 2, 0, 0, 1, sdf, evaluatedSdf, material, replace, outVoxels);
                        }
                    }
                }
            }

            evaluatedSdf.Dispose();

            return changed;
        }

        private static void ApplySdfIntersection(NativeArray3D<Voxel, TIndexer> snapshot, float3 origin, int x, int y, int z, int chunkSize, int edge, int xo, int yo, int zo, TSdf sdf, NativeArray3D<float, LinearIndexer> evaluatedSdf, int material, bool replace, NativeArray3D<Voxel, TIndexer> outVoxels)
        {
            if (x < 0 || y < 0 || z < 0 || x >= chunkSize || y >= chunkSize || z >= chunkSize)
            {
                return;
            }

            //Remove intersections and normals from edges that do not have
            //a material change
            /*if (voxels[x, y, z].material == voxels[x + xo, y + yo, z + zo].material)
            {
                intersections[x][y][z][edge] = 0;
                normals[x][y][z][edge] = Vector3.zero;
                return;
            }*/

            bool isIgnoredReplacement = replace && (outVoxels[x, y, z].Material == 0 || outVoxels[x + xo, y + yo, z + zo].Material == 0);

            float d1 = evaluatedSdf[x, y, z];
            float d2 = evaluatedSdf[x + xo, y + yo, z + zo];

            float normalFacing = material == 0 ? -1.0f : 1.0f;
            const float epsilon = 0.001F;

            if (!isIgnoredReplacement)
            {
                if ((d1 < 0) != (d2 < 0))
                {
                    float3 edgeStart = new float3(x, y, z) - origin;
                    float3 newIntersectionPoint = FindIntersection(edgeStart, d1, new float3(x + xo, y + yo, z + zo) - origin, d2, sdf, 0.001f, 4);

                    float newIntersection = math.length(newIntersectionPoint - edgeStart);

                    if (snapshot[x, y, z].Material == snapshot[x + xo, y + yo, z + zo].Material)
                    {
                        //Currently no existing intersection, can just set
                        outVoxels[x, y, z] = outVoxels[x, y, z].ModifyEdge(true, edge, newIntersection, normalFacing * SdfDerivative.FirstOrderCentralFiniteDifferenceNormalized(newIntersectionPoint, epsilon, sdf));
                    }
                    else
                    {
                        //An intersection already exists, need to check where the already existing intersection is and compare to the new one
                        float intersection = outVoxels[x, y, z].Data[edge].w;

                        bool overwrite = false;

                        bool s1 = snapshot[x, y, z].Material != 0;
                        bool s2 = snapshot[x + xo, y + yo, z + zo].Material != 0;

                        if (material != 0)
                        {
                            if (d1 < 0 && s1 && newIntersection > intersection)
                            {
                                overwrite = true;
                            }
                            else if (d2 < 0 && s2 && newIntersection < intersection)
                            {
                                overwrite = true;
                            }
                        }
                        else
                        {
                            if (d1 < 0 && s2 && newIntersection > intersection)
                            {
                                overwrite = true;
                            }
                            else if (d2 < 0 && s1 && newIntersection < intersection)
                            {
                                overwrite = true;
                            }
                        }

                        if (overwrite)
                        {
                            outVoxels[x, y, z] = outVoxels[x, y, z].ModifyEdge(true, edge, newIntersection, normalFacing * SdfDerivative.FirstOrderCentralFiniteDifferenceNormalized(newIntersectionPoint, epsilon, sdf));
                        }
                    }
                }
                else if (d1 < 0 && d2 < 0)
                {
                    outVoxels[x, y, z] = outVoxels[x, y, z].ModifyEdge(true, edge, 0, 0);
                }
            }
            else if ((d1 < 0) != (d2 < 0))
            {
                float3 intersection = FindIntersection(new float3(x, y, z) - origin, d1, new float3(x + xo, y + yo, z + zo) - origin, d2, sdf, 0.001F, 4);

                float intersectionDist = math.length(intersection - new float3(x, y, z) + origin);

                //TODO Use inVoxels for comparison?
                if (outVoxels[x, y, z].Material == outVoxels[x + xo, y + yo, z + zo].Material)
                {
                    outVoxels[x, y, z] = outVoxels[x, y, z].ModifyEdge(true, edge, math.length(intersection - new float3(x, y, z) + origin), normalFacing * SdfDerivative.FirstOrderCentralFiniteDifferenceNormalized(intersection, epsilon, sdf));
                }
            }
        }

        private static float3 FindIntersection(float3 v1, float d1, float3 v2, float d2, TSdf sdf, float epsilon, int maxSteps)
        {
            //Finds the intersection point with an adaptive binary search.
            //Instead of just halving the intervals it uses the distance returned by the SDF
            //to further narrow the interval.

            float3 dir = math.normalize(v2 - v1);

            float3 p1 = v1;
            float3 p2 = v2;

            float abs1 = math.abs(d1);
            float abs2 = math.abs(d2);

            for (int i = 0; i < maxSteps; i++)
            {
                float3 p3 = (p1 + dir * abs1 + p2 - dir * abs2) * 0.5f;
                float d3 = sdf.Eval(p3);

                if ((d3 < 0) == (d1 < 0))
                {
                    p1 = p3;
                    d1 = d3;
                    abs1 = math.abs(d3);
                }
                else
                {
                    p2 = p3;
                    d2 = d3;
                    abs2 = math.abs(d3);
                }

                if (abs1 < epsilon || abs2 < epsilon)
                {
                    break;
                }
            }

            if (abs1 < abs2)
            {
                return p1;
            }
            else
            {
                return p2;
            }
        }
    }
}