using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelPolygonizer;
using VoxelPolygonizer.CMS;

namespace Voxel
{
    [BurstCompile]
    public struct ChunkBuildJob<TIndexer> : IJob
        where TIndexer : struct, IIndexer
    {
        [ReadOnly] public CMSProperties.DataStruct PolygonizationProperties;

        [ReadOnly] public NativeArray3D<Voxel, TIndexer> Voxels;

        public NativeList<float3> MeshVertices;
        public NativeList<float3> MeshNormals;
        public NativeList<int> MeshTriangles;
        public NativeList<Color32> MeshColors;
        public NativeList<int> MeshMaterials;

        public void Execute()
        {
            int nVoxels = Voxels.Length(0) * Voxels.Length(1) * Voxels.Length(2);
            int nCells = (Voxels.Length(0) - 1) * (Voxels.Length(1) - 1) * (Voxels.Length(2) - 1);

            var MemoryCache = new NativeMemoryCache(Allocator.Temp);
            var DedupeCache = new VoxelMeshTessellation.NativeDeduplicationCache(Allocator.Temp);

            var Components = new NativeList<VoxelMeshComponent>(Allocator.Temp);
            var Indices = new NativeList<PackedIndex>(Allocator.Temp);
            var Vertices = new NativeList<VoxelMeshComponentVertex>(Allocator.Temp);

            var Materials = new NativeArray<int>(8, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var Intersections = new NativeArray<float>(12, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var Normals = new NativeArray<float3>(12, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var solver = new SvdQefSolver<RawArrayVoxelCell>
            {
                Clamp = false
            };
            var polygonizer = new CMSVoxelPolygonizer<RawArrayVoxelCell, CMSProperties.DataStruct, SvdQefSolver<RawArrayVoxelCell>, IntersectionSharpFeatureSolver<RawArrayVoxelCell>>(PolygonizationProperties, solver, new IntersectionSharpFeatureSolver<RawArrayVoxelCell>(), MemoryCache);

            int xSize = Voxels.Length(0);
            int ySize = Voxels.Length(1);
            int zSize = Voxels.Length(2);

            TIndexer indexer = Voxels.Indexer;
            for (int index = 0; index < nVoxels; ++index)
            {
                int x = 0, y = 0, z = 0;
                indexer.FromIndex(index, ref x, ref y, ref z);

                if(x < xSize - 1 && y < ySize - 1 && z < zSize - 1 && FillCell(Voxels, x, y, z, 0, Materials, Intersections, Normals))
                {
                    //TODO Directly operate on voxel array
                    RawArrayVoxelCell cell = new RawArrayVoxelCell(0, new float3(x, y, z), Materials, Intersections, Normals);

                    polygonizer.Polygonize(cell, Components, Indices, Vertices);
                }
            }

            VoxelMeshTessellation.Tessellate(Components, Indices, Vertices, Matrix4x4.identity, MeshVertices, MeshTriangles, MeshNormals, MeshMaterials, new MaterialColors(), MeshColors, DedupeCache);

            MemoryCache.Dispose();
            DedupeCache.Dispose();

            Materials.Dispose();
            Intersections.Dispose();
            Normals.Dispose();

            Components.Dispose();
            Indices.Dispose();
            Vertices.Dispose();

            //Cells.Dispose();
        }

        public static bool FillCell(NativeArray3D<Voxel, TIndexer> voxels, int x, int y, int z, int cellIndex, NativeArray<int> materials, NativeArray<float> intersections, NativeArray<float3> normals)
        {
            var v000 = voxels[x, y, z];
            var v100 = voxels[x + 1, y, z];
            var v101 = voxels[x + 1, y, z + 1];
            var v001 = voxels[x, y, z + 1];
            var v010 = voxels[x, y + 1, z];
            var v110 = voxels[x + 1, y + 1, z];
            var v111 = voxels[x + 1, y + 1, z + 1];
            var v011 = voxels[x, y + 1, z + 1];

            var d000 = (float4x3)v000.Data;
            var d100 = (float4x3)v100.Data;
            var d101 = (float4x3)v101.Data;
            var d001 = (float4x3)v001.Data;
            var d010 = (float4x3)v010.Data;
            var d110 = (float4x3)v110.Data;
            var d111 = (float4x3)v111.Data;
            var d011 = (float4x3)v011.Data;

            int solids = 0;

            if ((materials[cellIndex * 8 + 0] = v000.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 1] = v100.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 2] = v101.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 3] = v001.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 4] = v010.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 5] = v110.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 6] = v111.Material) != 0) solids++;
            if ((materials[cellIndex * 8 + 7] = v011.Material) != 0) solids++;

            if (solids == 0 || solids == 8)
            {
                //No surface in cell
                return false;
            }

            intersections[cellIndex * 12 + 0] = d000[0].w;
            intersections[cellIndex * 12 + 1] = d100[2].w;
            intersections[cellIndex * 12 + 2] = d001[0].w;
            intersections[cellIndex * 12 + 3] = d000[2].w;
            intersections[cellIndex * 12 + 4] = d010[0].w;
            intersections[cellIndex * 12 + 5] = d110[2].w;
            intersections[cellIndex * 12 + 6] = d011[0].w;
            intersections[cellIndex * 12 + 7] = d010[2].w;
            intersections[cellIndex * 12 + 8] = d000[1].w;
            intersections[cellIndex * 12 + 9] = d100[1].w;
            intersections[cellIndex * 12 + 10] = d101[1].w;
            intersections[cellIndex * 12 + 11] = d001[1].w;

            normals[cellIndex * 12 + 0] = d000[0].xyz;
            normals[cellIndex * 12 + 1] = d100[2].xyz;
            normals[cellIndex * 12 + 2] = d001[0].xyz;
            normals[cellIndex * 12 + 3] = d000[2].xyz;
            normals[cellIndex * 12 + 4] = d010[0].xyz;
            normals[cellIndex * 12 + 5] = d110[2].xyz;
            normals[cellIndex * 12 + 6] = d011[0].xyz;
            normals[cellIndex * 12 + 7] = d010[2].xyz;
            normals[cellIndex * 12 + 8] = d000[1].xyz;
            normals[cellIndex * 12 + 9] = d100[1].xyz;
            normals[cellIndex * 12 + 10] = d101[1].xyz;
            normals[cellIndex * 12 + 11] = d001[1].xyz;

            return true;
        }
    }
}