using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoxelPolygonizer.CMS
{
    class CMSTables 
    {
        public static readonly int[] IntersectionEdgeTable = new int[]
        {
            0, -1, -1, -1, -1,
            2,  3,  0, -1, -1,
            2,  0,  1, -1, -1,
            2,  3,  1, -1, -1,
            2,  1,  2, -1, -1,
            4,  3,  2,  1,  0,
            2,  0,  2, -1, -1,
            2,  3,  2, -1, -1,
            2,  2,  3, -1, -1,
            2,  2,  0, -1, -1,
            4,  2,  3,  0,  1,
            2,  2,  1, -1, -1,
            2,  1,  3, -1, -1,
            2,  1,  0, -1, -1,
            2,  0,  3, -1, -1,
            0, -1, -1, -1, -1
        };

        public static readonly int[] EdgeMaterialTable = new int[]
        {
            0, -1, -1, -1, -1,
            2,  0,  0, -1, -1,
            2,  1,  1, -1, -1,
            2,  0,  1, -1, -1,
            2,  2,  2, -1, -1,
            4,  0,  2,  2,  0,
            2,  1,  2, -1, -1,
            2,  0,  2, -1, -1,
            2,  3,  3, -1, -1,
            2,  3,  0, -1, -1,
            4,  3,  3,  1,  1,
            2,  3,  1, -1, -1,
            2,  2,  3, -1, -1,
            2,  2,  0, -1, -1,
            2,  1,  3, -1, -1,
            0, -1, -1, -1, -1
        };

        public static readonly int[] MaterialTransitionEdgeTable = new int[]
        {
            0, -1, -1,
            0, -1, -1,
            0, -1, -1,
            1,  0, -1,
            0, -1, -1,
            0, -1, -1,
            1,  1, -1,
            2,  0,  1,
            0, -1, -1,
            1,  3, -1,
            0, -1, -1,
            2,  3,  0,
            1,  2, -1,
            2,  2,  3,
            2,  1,  2,
            0, -1, -1
        };

        public static readonly int[] MaterialTransitionEdgeMaterialTable = new int[]
        {
            -1, -1, -1, -1,
            -1, -1, -1, -1,
            -1, -1, -1, -1,
             0,  1, -1, -1,
            -1, -1, -1, -1,
            -1, -1, -1, -1,
             1,  2, -1, -1,
             0,  1,  1,  2,
            -1, -1, -1, -1,
             3,  0, -1, -1,
            -1, -1, -1, -1,
             3,  0,  0,  1,
             2,  3, -1, -1,
             2,  3,  3,  0,
             1,  2,  2,  3,
            -1, -1, -1, -1
        };

        public static readonly VoxelCellFace[] VoxelCellFaces = new VoxelCellFace[]
        {
            VoxelCellFace.ZNeg,
            VoxelCellFace.XPos,
            VoxelCellFace.ZPos,
            VoxelCellFace.XNeg,
            VoxelCellFace.YNeg,
            VoxelCellFace.YPos
        };
    }
}
