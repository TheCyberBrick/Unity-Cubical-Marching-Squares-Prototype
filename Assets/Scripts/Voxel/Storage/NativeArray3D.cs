using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Voxel
{
    public delegate TIndexer IndexerFactory<TIndexer>(int xSize, int ySize, int zSize) where TIndexer : struct, IIndexer;

    public interface IIndexer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int ToIndex(int x, int y, int z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void FromIndex(int index, ref int x, ref int y, ref int z);
    }

    public struct LinearIndexer : IIndexer
    {
        private readonly int xSize, ySize, zSize, stride;

        public LinearIndexer(int xSize, int ySize, int zSize)
        {
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
            stride = xSize * ySize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToIndex(int x, int y, int z)
        {
            return x + y * xSize + z * stride;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FromIndex(int index, ref int x, ref int y, ref int z)
        {
            x = index % xSize;
            y = (index / xSize) % ySize;
            z = (index / stride) % zSize;
        }
    }

    /// <summary>
    /// The m3D_e_sLUT/m3D_d_sLUT/morton3D_DecodeCoord_LUT256 methods were separated, slightly adjusted and ported to C#.
    /// The original work is licensed under following license:
    /// 
    /// MIT License
    /// 
    /// Copyright(c) 2016 Jeroen Baert
    /// 
    /// Permission is hereby granted, free of charge, to any person obtaining a copy
    /// of this software and associated documentation files (the "Software"), to deal
    /// in the Software without restriction, including without limitation the rights
    /// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    /// copies of the Software, and to permit persons to whom the Software is
    /// furnished to do so, subject to the following conditions:
    /// 
    /// The above copyright notice and this permission notice shall be included in all
    /// copies or substantial portions of the Software.
    /// 
    /// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    /// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    /// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    /// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    /// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    /// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    /// SOFTWARE.
    /// </summary>
    public struct MortonIndexer : IIndexer
    {
        private readonly int xSize, ySize, zSize;

        public MortonIndexer(int xSize, int ySize, int zSize)
        {
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ToIndex(int x, int y, int z)
        {
            int answer = 0;
            for (int i = 8; i > 0; --i)
            {
                int shift = (i - 1) * 8;
                answer =
                answer << 24 |
                (MortonLUTs.Morton3D_encode_z_256[(z >> shift) & 0x000000FF] |
                    MortonLUTs.Morton3D_encode_y_256[(y >> shift) & 0x000000FF] |
                    MortonLUTs.Morton3D_encode_x_256[(x >> shift) & 0x000000FF]);
            }
            return answer;
        }

        public void FromIndex(int m, ref int x, ref int y, ref int z)
        {
            /*x = morton3D_DecodeCoord_LUT256<morton, coord>(m, Morton3D_decode_x_512, 0);
            y = morton3D_DecodeCoord_LUT256<morton, coord>(m, Morton3D_decode_y_512, 0);
            z = morton3D_DecodeCoord_LUT256<morton, coord>(m, Morton3D_decode_z_512, 0);*/

            x = 0;
            for (int i = 0; i < 4; ++i)
            {
                x |= (MortonLUTs.Morton3D_decode_x_512[(m >> (i * 9)) & 0x000001FF] << (3 * i));
            }

            y = 0;
            for (int i = 0; i < 4; ++i)
            {
                y |= (MortonLUTs.Morton3D_decode_y_512[(m >> (i * 9)) & 0x000001FF] << (3 * i));
            }

            z = 0;
            for (int i = 0; i < 4; ++i)
            {
                z |= (MortonLUTs.Morton3D_decode_z_512[(m >> (i * 9)) & 0x000001FF] << (3 * i));
            }
        }
    }


    public struct NativeArray3D<T, TIndexer> : IDisposable, IEnumerable<T>, IEquatable<NativeArray<T>>, IEnumerable
        where T : struct
        where TIndexer : struct, IIndexer
    {
        private NativeArray<T> data;

        public TIndexer Indexer {
            get;
            private set;
        }
        private readonly int xSize, ySize, zSize;

        public NativeArray3D(TIndexer indexer, int xSize, int ySize, int zSize, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
        {
            Indexer = indexer;
            data = new NativeArray<T>(xSize * ySize * zSize, allocator, options);
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
        }

        public T this[int x, int y, int z]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return data[Indexer.ToIndex(x, y, z)];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                data[Indexer.ToIndex(x, y, z)] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Length(int dim)
        {
            switch (dim)
            {
                case 0: return xSize;
                case 1: return ySize;
                case 2: return zSize;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        public void CopyTo(T[] array)
        {
            data.CopyTo(array);
        }

        public void CopyTo(NativeArray<T> array)
        {
            data.CopyTo(array);
        }

        public void CopyTo(NativeArray3D<T, TIndexer> array)
        {
            data.CopyTo(array.data);
        }

        public void CopyFrom(T[] array)
        {
            data.CopyFrom(array);
        }

        public void CopyFrom(NativeArray<T> array)
        {
            data.CopyFrom(array);
        }

        public void CopyFrom(NativeArray3D<T, TIndexer> array)
        {
            data.CopyFrom(array.data);
        }

        public void Dispose()
        {
            data.Dispose();
        }

        public bool Equals(NativeArray<T> other)
        {
            return data.Equals(other);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return data.GetEnumerator();
        }
    }
}