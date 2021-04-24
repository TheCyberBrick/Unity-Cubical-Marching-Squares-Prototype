using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Voxel.Voxelizer
{
    [BurstCompile]
    public struct VoxelizerCollectBinsJob : IJob
    {
        [ReadOnly] public NativeStream.Reader stream;
        [ReadOnly] public int streams;
        [ReadOnly] public int width, height, depth;

        [WriteOnly] public NativeList<Column> binColumnsX;
        [WriteOnly] public NativeList<int> binsX;
        [WriteOnly] public NativeList<Column> binColumnsY;
        [WriteOnly] public NativeList<int> binsY;
        [WriteOnly] public NativeList<Column> binColumnsZ;
        [WriteOnly] public NativeList<int> binsZ;

        public readonly struct Column
        {
            public readonly int colIndex;
            public readonly int start;
            public readonly ushort length;

            public Column(int colIndex, int start, ushort length)
            {
                this.colIndex = colIndex;
                this.start = start;
                this.length = length;
            }
        }

        public void Execute()
        {
            var binMapX = new NativeMultiHashMap<int, int>(0, Allocator.Temp);
            var binMapY = new NativeMultiHashMap<int, int>(0, Allocator.Temp);
            var binMapZ = new NativeMultiHashMap<int, int>(0, Allocator.Temp);

            for (int t = 0; t < streams; t++)
            {
                var v = t * 3;

                stream.BeginForEachIndex(t);

                var binSizeX = stream.Read<int>();
                for (int i = 0; i < binSizeX; i++)
                {
                    binMapX.Add(stream.Read<int>(), v);
                }

                var binSizeY = stream.Read<int>();
                for (int i = 0; i < binSizeY; i++)
                {
                    binMapY.Add(stream.Read<int>(), v);
                }

                var binSizeZ = stream.Read<int>();
                for (int i = 0; i < binSizeZ; i++)
                {
                    binMapZ.Add(stream.Read<int>(), v);
                }

                stream.EndForEachIndex();
            }

            EmitBins(height * depth, binMapX, binColumnsX, binsX);
            binMapX.Dispose();

            EmitBins(width * depth, binMapY, binColumnsY, binsY);
            binMapY.Dispose();

            EmitBins(width * height, binMapZ, binColumnsZ, binsZ);
            binMapZ.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EmitBins(int numCols, NativeMultiHashMap<int, int> binMap, NativeList<Column> binColumns, NativeList<int> bins)
        {
            int binsLength = 0;

            for (int colIndex = 0; colIndex < numCols; colIndex++)
            {
                int startIndex = binsLength;

                if (binMap.TryGetFirstValue(colIndex, out int v, out NativeMultiHashMapIterator<int> it))
                {
                    do
                    {
                        bins.Add(v);
                        binsLength++;
                    } while (binMap.TryGetNextValue(out v, ref it));
                }

                if (startIndex != binsLength)
                {
                    binColumns.Add(new VoxelizerCollectBinsJob.Column(colIndex, startIndex, (ushort)(binsLength - startIndex)));
                }
            }
        }
    }
}
