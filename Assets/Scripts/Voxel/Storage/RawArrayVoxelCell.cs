using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelPolygonizer;

namespace Voxel
{
    public struct RawArrayVoxelCell : IVoxelCell
    {
        [StructLayout(LayoutKind.Sequential)]
        private readonly struct CellEdgesLookup
        {
            private readonly CellEdges zNeg;
            private readonly CellEdges xPos;
            private readonly CellEdges zPos;
            private readonly CellEdges xNeg;
            private readonly CellEdges yNeg;
            private readonly CellEdges yPos;

            private CellEdgesLookup(CellEdges zNeg, CellEdges xPos, CellEdges zPos, CellEdges xNeg, CellEdges yNeg, CellEdges yPos)
            {
                this.zNeg = zNeg;
                this.xPos = xPos;
                this.zPos = zPos;
                this.xNeg = xNeg;
                this.yNeg = yNeg;
                this.yPos = yPos;
            }

            internal static CellEdgesLookup Create()
            {
                return new CellEdgesLookup(
                    //ZNeg
                    new CellEdges(16, 17, 18, 19),
                    //XPos
                    new CellEdges(4, 5, 6, 7),
                    //ZPos
                    new CellEdges(20, 21, 22, 23),
                    //XNeg
                    new CellEdges(0, 1, 2, 3),
                    //YNeg
                    new CellEdges(8, 9, 10, 11),
                    //YPos
                    new CellEdges(12, 13, 14, 15)
                );
            }

            unsafe internal CellEdges this[int index]
            {
                get
                {
                    fixed (CellEdges* ptr = &zNeg)
                    {
                        return ptr[index];
                    }
                }
            }
        }

        public readonly NativeArray<int> Materials;
        public readonly NativeArray<float> Intersections;
        public readonly NativeArray<float3> Normals;

        public readonly int index;

        public readonly float3 pos;

        private readonly CellEdgesLookup edges;

        public RawArrayVoxelCell(int index, float3 pos, NativeArray<int> materials, NativeArray<float> intersections, NativeArray<float3> normals)
        {
            this.index = index;
            this.pos = pos;
            this.Materials = materials;
            this.Intersections = intersections;
            this.Normals = normals;
            edges = CellEdgesLookup.Create();
        }

        public int GetCellFaceCount(VoxelCellFace face)
        {
            return 1;
        }

        public int GetCellFace(VoxelCellFace face, int index)
        {
            return (int)face;
        }

        public float GetWidth()
        {
            return 1;
        }

        public float GetDepth()
        {
            return 1;
        }

        public float GetHeight()
        {
            return 1;
        }

        public CellEdges GetEdges(int cell)
        {
            return edges[cell];
        }

        public CellInfo GetInfo(int cell)
        {
            float3 cellPos;
            switch (cell)
            {
                default:
                case 0:
                    cellPos = new float3(0, 0, 0);
                    break;
                case 1:
                    cellPos = new float3(1, 0, 0);
                    break;
                case 2:
                    cellPos = new float3(1, 0, 1);
                    break;
                case 3:
                    cellPos = new float3(0, 0, 1);
                    break;
                case 4:
                    cellPos = new float3(0, 0, 1);
                    break;
                case 5:
                    cellPos = new float3(0, 1, 0);
                    break;
            }
            return new CellInfo(cellPos + pos, 1, 1);
        }

        public bool HasIntersection(int cell, int edge)
        {
            return math.lengthsq(GetNormal(cell, edge)) > 0.001f;
        }

        public float GetIntersection(int cell, int edge)
        {
            switch (edge)
            {
                case 0:
                    return 1 - Intersections[index * 12 + 3];
                case 1:
                    return Intersections[index * 12 + 8];
                case 2:
                    return Intersections[index * 12 + 7];
                case 3:
                    return 1 - Intersections[index * 12 + 11];

                case 4:
                    return Intersections[index * 12 + 1];
                case 5:
                    return Intersections[index * 12 + 10];
                case 6:
                    return 1 - Intersections[index * 12 + 5];
                case 7:
                    return 1 - Intersections[index * 12 + 9];

                case 8:
                    return Intersections[index * 12 + 2];
                case 9:
                    return 1 - Intersections[index * 12 + 1];
                case 10:
                    return 1 - Intersections[index * 12 + 0];
                case 11:
                    return Intersections[index * 12 + 3];

                case 12:
                    return Intersections[index * 12 + 4];
                case 13:
                    return Intersections[index * 12 + 5];
                case 14:
                    return 1 - Intersections[index * 12 + 6];
                case 15:
                    return 1 - Intersections[index * 12 + 7];

                case 16:
                    return Intersections[index * 12 + 0];
                case 17:
                    return Intersections[index * 12 + 9];
                case 18:
                    return 1 - Intersections[index * 12 + 4];
                case 19:
                    return 1 - Intersections[index * 12 + 8];

                case 20:
                    return 1 - Intersections[index * 12 + 2];
                case 21:
                    return Intersections[index * 12 + 11];
                case 22:
                    return Intersections[index * 12 + 6];
                case 23:
                    return 1 - Intersections[index * 12 + 10];
            }

            return 0;
        }

        public CellMaterials GetMaterials(int cell)
        {
            switch (cell)
            {
                case 0:
                    //Z-
                    return new CellMaterials(Materials[index * 8 + 0], Materials[index * 8 + 1], Materials[index * 8 + 5], Materials[index * 8 + 4]);
                case 1:
                    //X+
                    return new CellMaterials(Materials[index * 8 + 1], Materials[index * 8 + 2], Materials[index * 8 + 6], Materials[index * 8 + 5]);
                case 2:
                    //Z+
                    return new CellMaterials(Materials[index * 8 + 2], Materials[index * 8 + 3], Materials[index * 8 + 7], Materials[index * 8 + 6]);
                case 3:
                    //X-
                    return new CellMaterials(Materials[index * 8 + 3], Materials[index * 8 + 0], Materials[index * 8 + 4], Materials[index * 8 + 7]);
                case 4:
                    //Y-
                    return new CellMaterials(Materials[index * 8 + 3], Materials[index * 8 + 2], Materials[index * 8 + 1], Materials[index * 8 + 0]);
                case 5:
                    //Y+
                    return new CellMaterials(Materials[index * 8 + 4], Materials[index * 8 + 5], Materials[index * 8 + 6], Materials[index * 8 + 7]);
            }
            return new CellMaterials(0, 0, 0, 0);
        }

        public int GetNeighboringEdge(int cell, int edge)
        {
            return -1;
        }

        public float3 GetNormal(int cell, int edge)
        {
            switch (edge)
            {
                default:
                case 0:
                    return Normals[index * 12 + 3];
                case 1:
                    return Normals[index * 12 + 8];
                case 2:
                    return Normals[index * 12 + 7];
                case 3:
                    return Normals[index * 12 + 11];

                case 4:
                    return Normals[index * 12 + 1];
                case 5:
                    return Normals[index * 12 + 10];
                case 6:
                    return Normals[index * 12 + 5];
                case 7:
                    return Normals[index * 12 + 9];

                case 8:
                    return Normals[index * 12 + 2];
                case 9:
                    return Normals[index * 12 + 1];
                case 10:
                    return Normals[index * 12 + 0];
                case 11:
                    return Normals[index * 12 + 3];

                case 12:
                    return Normals[index * 12 + 4];
                case 13:
                    return Normals[index * 12 + 5];
                case 14:
                    return Normals[index * 12 + 6];
                case 15:
                    return Normals[index * 12 + 7];

                case 16:
                    return Normals[index * 12 + 0];
                case 17:
                    return Normals[index * 12 + 9];
                case 18:
                    return Normals[index * 12 + 4];
                case 19:
                    return Normals[index * 12 + 8];

                case 20:
                    return Normals[index * 12 + 2];
                case 21:
                    return Normals[index * 12 + 11];
                case 22:
                    return Normals[index * 12 + 6];
                case 23:
                    return Normals[index * 12 + 10];
            }
        }
    }
}