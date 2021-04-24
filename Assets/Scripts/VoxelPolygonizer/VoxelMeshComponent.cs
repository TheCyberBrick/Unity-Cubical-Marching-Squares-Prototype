using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace VoxelPolygonizer
{
    public readonly struct PackedIndex
    {
        private readonly int packedIndex;

        public PackedIndex(int index, bool isMaterialTransition, bool is2DSharpFeature, bool isNormalSet)
        {
            this.packedIndex = (index << 3) | (isMaterialTransition ? 0b001 : 0) | (is2DSharpFeature ? 0b010 : 0) | (isNormalSet ? 0b100 : 0);
        }

        public int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return packedIndex >> 3;
            }
        }

        public bool IsMaterialTransition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (packedIndex & 0b001) != 0;
            }
        }

        public bool Is2DSharpFeature
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (packedIndex & 0b010) != 0;
            }
        }

        public bool IsNormalSet
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (packedIndex & 0b100) != 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(PackedIndex i) => i.Index;
    }

    public readonly struct VoxelMeshComponentVertex
    {
        public readonly float3 Position;
        public readonly float3 Normal;
        public readonly int Material;

        public VoxelMeshComponentVertex(float3 position, float3 normal, int material)
        {
            this.Position = position;
            this.Normal = normal;
            this.Material = material;
        }
    }

    public readonly struct VoxelMeshComponent
    {
        public readonly float3 FeatureVertex;
        public readonly int Index;
        public readonly int Size;
        public readonly bool RegularFeature;
        public readonly bool MaterialTransitionFeature;

        public bool Feature
        {
            get
            {
                return RegularFeature || MaterialTransitionFeature;
            }
        }

        public VoxelMeshComponent(int index, int size, float3 sharpVertex, bool regularFeature, bool materialTransitionFeature)
        {
            this.Index = index;
            this.Size = size;
            this.FeatureVertex = sharpVertex;
            this.RegularFeature = regularFeature;
            this.MaterialTransitionFeature = materialTransitionFeature;
        } 
    }
}