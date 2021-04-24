using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CreateVoxelTerrain;

namespace Voxel
{
    public readonly struct DefaultCustomBrushType : IEquatable<DefaultCustomBrushType>
    {
        public static readonly DefaultCustomBrushType BOX = new DefaultCustomBrushType(BrushType.Box);
        public static readonly DefaultCustomBrushType SPHERE = new DefaultCustomBrushType(BrushType.Sphere);

        public readonly BrushType type;

        public DefaultCustomBrushType(BrushType type)
        {
            this.type = type;
        }

        public bool Equals(DefaultCustomBrushType other)
        {
            return type == other.type;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is DefaultCustomBrushType))
            {
                return false;
            }

            var type = (DefaultCustomBrushType)obj;
            return this.type == type.type;
        }

        public override int GetHashCode()
        {
            return 34944597 + type.GetHashCode();
        }

        public static bool operator ==(DefaultCustomBrushType type1, DefaultCustomBrushType type2)
        {
            return type1.Equals(type2);
        }

        public static bool operator !=(DefaultCustomBrushType type1, DefaultCustomBrushType type2)
        {
            return !type1.Equals(type2);
        }
    }
}
