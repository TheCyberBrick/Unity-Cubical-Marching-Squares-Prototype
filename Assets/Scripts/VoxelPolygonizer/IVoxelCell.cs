using Unity.Mathematics;

namespace VoxelPolygonizer
{
    public enum VoxelCellFace
    {
        ZNeg = 0,
        XPos = 1,
        ZPos = 2,
        XNeg = 3,
        YNeg = 4,
        YPos = 5
    }

    public static class VoxelCellFaceExtensions
    {
        private static readonly float3 ZNegNormal = new float3(0, 0, -1);
        private static readonly float3 XPosNormal = new float3(1, 0, 0);
        private static readonly float3 ZPosNormal = new float3(0, 0, 1);
        private static readonly float3 XNegNormal = new float3(-1, 0, 0);
        private static readonly float3 YNegNormal = new float3(0, -1, 0);
        private static readonly float3 YPosNormal = new float3(0, 1, 0);

        private static readonly float3 ZNegBasisX = new float3(1, 0, 0);
        private static readonly float3 XPosBasisX = new float3(0, 0, 1);
        private static readonly float3 ZPosBasisX = new float3(-1, 0, 0);
        private static readonly float3 XNegBasisX = new float3(0, 0, -1);
        private static readonly float3 YNegBasisX = new float3(1, 0, 0);
        private static readonly float3 YPosBasisX = new float3(1, 0, 0);

        private static readonly float3 ZNegBasisY = new float3(0, 1, 0);
        private static readonly float3 XPosBasisY = new float3(0, 1, 0);
        private static readonly float3 ZPosBasisY = new float3(0, 1, 0);
        private static readonly float3 XNegBasisY = new float3(0, 1, 0);
        private static readonly float3 YNegBasisY = new float3(0, 0, -1);
        private static readonly float3 YPosBasisY = new float3(0, 0, 1);

        public static float3 Normal(this VoxelCellFace face)
        {
            switch (face)
            {
                default:
                case VoxelCellFace.ZNeg:
                    return ZNegNormal;
                case VoxelCellFace.XPos:
                    return XPosNormal;
                case VoxelCellFace.ZPos:
                    return ZPosNormal;
                case VoxelCellFace.XNeg:
                    return XNegNormal;
                case VoxelCellFace.YNeg:
                    return YNegNormal;
                case VoxelCellFace.YPos:
                    return YPosNormal;
            }
        }

        public static float3 BasisX(this VoxelCellFace face)
        {
            switch (face)
            {
                default:
                case VoxelCellFace.ZNeg:
                    return ZNegBasisX;
                case VoxelCellFace.XPos:
                    return XPosBasisX;
                case VoxelCellFace.ZPos:
                    return ZPosBasisX;
                case VoxelCellFace.XNeg:
                    return XNegBasisX;
                case VoxelCellFace.YNeg:
                    return YNegBasisX;
                case VoxelCellFace.YPos:
                    return YPosBasisX;
            }
        }

        public static float3 BasisY(this VoxelCellFace face)
        {
            switch (face)
            {
                default:
                case VoxelCellFace.ZNeg:
                    return ZNegBasisY;
                case VoxelCellFace.XPos:
                    return XPosBasisY;
                case VoxelCellFace.ZPos:
                    return ZPosBasisY;
                case VoxelCellFace.XNeg:
                    return XNegBasisY;
                case VoxelCellFace.YNeg:
                    return YNegBasisY;
                case VoxelCellFace.YPos:
                    return YPosBasisY;
            }
        }
    }

    public readonly struct CellInfo
    {
        public readonly float Width;
        public readonly float Height;
        public readonly float3 Position;

        public CellInfo(float3 position, float width, float height)
        {
            this.Width = width;
            this.Height = height;
            this.Position = position;
        }
    }

    public readonly struct CellMaterials
    {
        public readonly int m1, m2, m3, m4;

        public CellMaterials(int m1, int m2, int m3, int m4)
        {
            this.m1 = m1;
            this.m2 = m2;
            this.m3 = m3;
            this.m4 = m4;
        }

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return m1;
                    case 1:
                        return m2;
                    case 2:
                        return m3;
                    case 3:
                        return m4;
                    default:
                        throw new System.IndexOutOfRangeException("Index was not between 0 to 4 (exclusive)");
                }
            }
        }
    }

    public readonly struct CellEdges
    {
        public readonly int e1, e2, e3, e4;

        public CellEdges(int e1, int e2, int e3, int e4)
        {
            this.e1 = e1;
            this.e2 = e2;
            this.e3 = e3;
            this.e4 = e4;
        }

        public int this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0:
                        return e1;
                    case 1:
                        return e2;
                    case 2:
                        return e3;
                    case 3:
                        return e4;
                    default:
                        throw new System.IndexOutOfRangeException("Index was not between 0 to 4 (exclusive)");
                }
            }
        }
    }

    public interface IVoxelCell
    {
        float GetWidth();

        float GetHeight();

        float GetDepth();

        int GetCellFaceCount(VoxelCellFace face);

        int GetCellFace(VoxelCellFace face, int index);

        CellInfo GetInfo(int cellFace);

        CellEdges GetEdges(int cellFace);

        CellMaterials GetMaterials(int cellFace);

        int GetNeighboringEdge(int cellFace, int edge);

        float GetIntersection(int cellFace, int edge);

        bool HasIntersection(int cellFace, int edge);

        float3 GetNormal(int cellFace, int edge);
    } 
}