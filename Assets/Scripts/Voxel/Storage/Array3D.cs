using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Voxel
{
    public struct Array3D<T> : IEnumerable<T>, IEquatable<NativeArray<T>>, IEnumerable where T : struct
    {
        private T[] data;

        private readonly int xSize, ySize, zSize, stride;

        public Array3D(int xSize, int ySize, int zSize)
        {
            data = new T[xSize * ySize * zSize];
            this.xSize = xSize;
            this.ySize = ySize;
            this.zSize = zSize;
            stride = xSize * ySize;
        }

        /// <summary>
        /// For linear access and best cache behaviour this should be iterated in following order:
        /// for(z) { for(y) { for(x) { ... } } }
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns></returns>
        public T this[int x, int y, int z]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return data[x + y * xSize + z * stride];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                data[x + y * xSize + z * stride] = value;
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
            data.CopyTo(array, 0);
        }

        public void CopyTo(NativeArray<T> array)
        {
            array.CopyFrom(data);
        }

        public void CopyTo(Array3D<T> array)
        {
            data.CopyTo(array.data, 0);
        }

        public void CopyFrom(T[] array)
        {
            array.CopyTo(data, 0);
        }

        public void CopyFrom(NativeArray<T> array)
        {
            array.CopyTo(data);
        }

        public void CopyFrom(Array3D<T> array)
        {
            array.data.CopyTo(data, 0);
        }

        public bool Equals(NativeArray<T> other)
        {
            return data.Equals(other);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return data.GetEnumerator() as IEnumerator<T>;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return data.GetEnumerator();
        }
    }
}