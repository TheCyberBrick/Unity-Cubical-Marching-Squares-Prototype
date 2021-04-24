using System.Runtime.CompilerServices;
using UnityEngine;

namespace Voxel
{
    public readonly struct ChunkPos
    {
        public readonly int x, y, z;

        private ChunkPos(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChunkPos FromChunk(int x, int y, int z)
        {
            return new ChunkPos(x, y, z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChunkPos FromVoxel(Vector3Int pos, int chunkSize)
        {
            return FromVoxel(pos.x, pos.y, pos.z, chunkSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChunkPos FromVoxel(int x, int y, int z, int chunkSize)
        {
            return new ChunkPos(x < 0 ? (x / chunkSize - 1) : (x / chunkSize), y < 0 ? (y / chunkSize - 1) : (y / chunkSize), z < 0 ? (z / chunkSize - 1) : (z / chunkSize));
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkPos pos &&
                   x == pos.x &&
                   y == pos.y &&
                   z == pos.z;
        }

        public override int GetHashCode()
        {
            var hashCode = 373119288;
            hashCode = hashCode * -1521134295 + x.GetHashCode();
            hashCode = hashCode * -1521134295 + y.GetHashCode();
            hashCode = hashCode * -1521134295 + z.GetHashCode();
            return hashCode;
        }

        public override string ToString()
        {
            return "SculptureChunk[x=" + x + ", y=" + y + ", z=" + z + "]";
        }
    }
}