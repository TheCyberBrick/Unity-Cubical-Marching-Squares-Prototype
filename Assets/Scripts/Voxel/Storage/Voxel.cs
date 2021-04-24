using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Voxel
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Voxel
    {
        public readonly QuantizedHermiteData Data;
        public readonly int Material;

        public Voxel(bool isVoxelSet, int material, float3 intersections, float3x3 normals)
        {
            Material = material;
            Data = new QuantizedHermiteData(isVoxelSet, intersections, normals);
        }

        public Voxel(bool isVoxelSet, int material, QuantizedHermiteData data)
        {
            Material = material;
            Data = new QuantizedHermiteData(isVoxelSet, data);
        }

        public Voxel ModifyMaterial(bool isVoxelSet, int material)
        {
            return new Voxel(isVoxelSet, material, Data);
        }

        public Voxel ModifyEdge(bool isVoxelSet, int edge, float intersection, float3 normal)
        {
            float4x3 newData = (float4x3)Data;
            newData[edge] = new float4(normal, intersection);
            return new Voxel(isVoxelSet, Material, new float3(newData[0].w, newData[1].w, newData[2].w), new float3x3(newData[0].xyz, newData[1].xyz, newData[2].xyz));
        }
    }

    //A simple uncompressed hermite data voxel
    /*public readonly struct Voxel
    {
        public readonly int Material;
        public readonly float3 Intersections;
        public readonly float3x3 Normals;

        public Voxel(int material, float3 intersections, float3x3 normals)
        {
            this.Material = material;
            this.Intersections = intersections;
            this.Normals = normals;
        }

        public Voxel ModifyMaterial(int material)
        {
            return new Voxel(material, Intersections, Normals);
        }

        public Voxel ModifyEdge(int edge, float intersection, float3 normal)
        {
            float3 newIntersections = Intersections;
            newIntersections[edge] = intersection;

            float3x3 newNormals = Normals;
            newNormals[edge] = normal;

            return new Voxel(Material, newIntersections, newNormals);
        }
    }*/
}
