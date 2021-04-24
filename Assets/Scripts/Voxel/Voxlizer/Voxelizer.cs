using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxel.Voxelizer
{
    public class Voxelizer
    {
        public class VoxelizationJob : IDisposable
        {
            public JobHandle Handle
            {
                get;
                private set;
            }

            private readonly IDisposable[] buffers;

            internal VoxelizationJob(JobHandle handle, params IDisposable[] buffers)
            {
                Handle = handle;
                this.buffers = buffers;
            }

            public void Dispose()
            {
                foreach (var buffer in buffers)
                {
                    buffer.Dispose();
                }
            }
        }

        public struct VoxelizationProperties
        {
            /// <summary>
            /// Inner loop batch size for job scheduler.
            /// </summary>
            public int parallelForBatchCount;

            /// <summary>
            /// Padding between object and grid boundaries. Must be > 0.
            /// </summary>
            public float padding;

            /// <summary>
            /// Minimum value of a normal projected onto its voxel edge in order to be considered valid.
            /// Increasing this value will cause more holes that need to be patched in a second pass, slowing down the process.
            /// </summary>
            public float angleThreshold;

            /// <summary>
            /// Maximum distance between voxel edge center to intersected triangle point. For a voxel size of 1 unit this value should
            /// be at least 0.5, otherwise intersected triangle points that lie even on the voxel edge may be discarded.
            /// Values larger than 1.5 * voxel size should be avoided as that may result in wrong triangles being used for intersections.
            /// Decreasing this value will cause more holes that need to be patched in a second pass, slowing down the process.
            /// </summary>
            public float snapThreshold;

            /// <summary>
            /// Whether vertex normals should be interpolated.
            /// This should only be used when the vertex normals differ slightly from the face normals, otherwise
            /// artifacts will happen due to the flat vs interpolated surface mismatch!
            /// </summary>
            public bool smoothNormals;

            public static readonly VoxelizationProperties FLAT = new VoxelizationProperties
            {
                parallelForBatchCount = 32,
                padding = 1,
                angleThreshold = 0.05f,
                snapThreshold = 0.8f,
                smoothNormals = false
            };

            public static readonly VoxelizationProperties SMOOTH = new VoxelizationProperties
            {
                parallelForBatchCount = 32,
                padding = 1,
                angleThreshold = 0.05f,
                snapThreshold = 0.8f,
                smoothNormals = true
            };
        }

        /// <summary>
        /// Voxelizes the mesh into the specified grid. The mesh is scaled to fit the grid (minus padding).
        /// Returns the voxelization job containing its job handle. The voxelization job must be disposed once the job has completed.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="normals"></param>
        /// <param name="grid"></param>
        /// <param name="material"></param>
        /// <param name="properties"></param>
        /// <returns>The voxelization job containing its job handle. The voxelization job must be disposed once the job has completed.</returns>
        public static VoxelizationJob Voxelize<TIndexer>(NativeArray<float3> vertices, NativeArray<float3> normals, NativeArray3D<Voxel, TIndexer> grid, int material, VoxelizationProperties properties)
            where TIndexer : struct, IIndexer
        {
            var triangles = vertices.Length / 3;

            var width = grid.Length(0);
            var height = grid.Length(1);
            var depth = grid.Length(2);

            var scaledVertices = new NativeArray<float3>(vertices.Length, Allocator.TempJob);

            var scaleJobHandle = new VoxelizerMeshScaleJob
            {
                inVertices = vertices,
                outVertices = scaledVertices,
                width = grid.Length(0),
                height = grid.Length(1),
                depth = grid.Length(2),
                padding = properties.padding
            }.Schedule();

            var binsStream = new NativeStream(triangles, Allocator.TempJob);

            //Bin triangles
            var binJobHandle = new VoxelizerBinJob
            {
                vertices = scaledVertices,
                width = width,
                height = height,
                depth = depth,
                stream = binsStream.AsWriter()
            }.Schedule(triangles, properties.parallelForBatchCount, scaleJobHandle);

            var binColsX = new NativeList<VoxelizerCollectBinsJob.Column>(Allocator.TempJob);
            var binColsY = new NativeList<VoxelizerCollectBinsJob.Column>(Allocator.TempJob);
            var binColsZ = new NativeList<VoxelizerCollectBinsJob.Column>(Allocator.TempJob);

            var binsX = new NativeList<int>(Allocator.TempJob);
            var binsY = new NativeList<int>(Allocator.TempJob);
            var binsZ = new NativeList<int>(Allocator.TempJob);

            //Collect bins and reorder
            var collectBinsJobHandle = new VoxelizerCollectBinsJob
            {
                stream = binsStream.AsReader(),
                streams = triangles,
                width = width,
                height = height,
                depth = depth,
                binColumnsX = binColsX,
                binsX = binsX,
                binColumnsY = binColsY,
                binsY = binsY,
                binColumnsZ = binColsZ,
                binsZ = binsZ
            }.Schedule(binJobHandle);

            var intersectionsStreamX = new NativeStream(height * depth, Allocator.TempJob);
            var intersectionsStreamY = new NativeStream(width * depth, Allocator.TempJob);
            var intersectionsStreamZ = new NativeStream(width * height, Allocator.TempJob);

            //Intersect X axis
            var intersectXJobHandle = new VoxelizerMeshIntersectionJob
            {
                vertices = scaledVertices,
                normals = normals,
                columns = binColsX.AsDeferredJobArray(),
                bins = binsX,
                width = width,
                height = height,
                depth = depth,
                axis = 0,
                stream = intersectionsStreamX.AsWriter(),
                smoothNormals = properties.smoothNormals
            }.Schedule(binColsX, properties.parallelForBatchCount, collectBinsJobHandle);

            //Intersect Y axis
            var intersectYJobHandle = new VoxelizerMeshIntersectionJob
            {
                vertices = scaledVertices,
                normals = normals,
                columns = binColsY.AsDeferredJobArray(),
                bins = binsY,
                width = width,
                height = height,
                depth = depth,
                axis = 1,
                stream = intersectionsStreamY.AsWriter(),
                smoothNormals = properties.smoothNormals
            }.Schedule(binColsY, properties.parallelForBatchCount, collectBinsJobHandle);

            //Intersect Z axis
            var intersectZJobHandle = new VoxelizerMeshIntersectionJob
            {
                vertices = scaledVertices,
                normals = normals,
                columns = binColsZ.AsDeferredJobArray(),
                bins = binsZ,
                width = width,
                height = height,
                depth = depth,
                axis = 2,
                stream = intersectionsStreamZ.AsWriter(),
                smoothNormals = properties.smoothNormals
            }.Schedule(binColsZ, properties.parallelForBatchCount, collectBinsJobHandle);

            var intersectionColsX = new NativeList<VoxelizerCollectBinsJob.Column>(height * depth, Allocator.TempJob);
            var intersectionColsY = new NativeList<VoxelizerCollectBinsJob.Column>(width * depth, Allocator.TempJob);
            var intersectionColsZ = new NativeList<VoxelizerCollectBinsJob.Column>(width * height, Allocator.TempJob);

            var intersectionsX = new NativeList<float4>(Allocator.TempJob);
            var intersectionsY = new NativeList<float4>(Allocator.TempJob);
            var intersectionsZ = new NativeList<float4>(Allocator.TempJob);

            //Collect X axis
            var collectXJobHandle = new VoxelizerCollectIntersectionsJob
            {
                stream = intersectionsStreamX.AsReader(),
                binColumns = binColsX,
                columns = intersectionColsX,
                intersections = intersectionsX
            }.Schedule(intersectXJobHandle);

            //Collect Y axis
            var collectYJobHandle = new VoxelizerCollectIntersectionsJob
            {
                stream = intersectionsStreamY.AsReader(),
                binColumns = binColsY,
                columns = intersectionColsY,
                intersections = intersectionsY
            }.Schedule(intersectYJobHandle);

            //Collect Z axis
            var collectZJobHandle = new VoxelizerCollectIntersectionsJob
            {
                stream = intersectionsStreamZ.AsReader(),
                binColumns = binColsZ,
                columns = intersectionColsZ,
                intersections = intersectionsZ
            }.Schedule(intersectZJobHandle);

            //Voxelizing using only the axis intersections can result in holes.
            //The voxelizer job will detect those holes and put them in this list
            //so they can be fixed later
            var holes = new NativeList<VoxelizerFillJob<TIndexer>.Hole>(Allocator.TempJob);

            //Fill in materials and normals where possible
            var voxelizerFillJobHandle = new VoxelizerFillJob<TIndexer>
            {
                colsX = intersectionColsX,
                colsY = intersectionColsY,
                colsZ = intersectionColsZ,
                intersectionsX = intersectionsX,
                intersectionsY = intersectionsY,
                intersectionsZ = intersectionsZ,
                material = material,
                grid = grid,
                holes = holes,
                angleThreshold = properties.angleThreshold,
                snapThreshold = properties.snapThreshold
            }.Schedule(JobHandle.CombineDependencies(collectXJobHandle, collectYJobHandle, collectZJobHandle));

            //If there are holes in the voxel data, i.e. missing intersections and normals,
            //then they are patched up in a second pass
            var patchesQueue = new NativeQueue<VoxelizerFindPatchesJob<TIndexer>.PatchedHole>(Allocator.TempJob);

            //Find all hole patches in parallel
            var findPatchesJobHandle = new VoxelizerFindPatchesJob<TIndexer>
            {
                vertices = scaledVertices,
                normals = normals,
                holes = holes.AsDeferredJobArray(),
                angleThreshold = properties.angleThreshold,
                smoothNormals = properties.smoothNormals,
                queue = patchesQueue.AsParallelWriter()
            }.Schedule(holes, properties.parallelForBatchCount, voxelizerFillJobHandle);

            //Apply the hole patches to the grid
            var applyPatchesJobHandle = new VoxelizerApplyPatchesJob<TIndexer>
            {
                queue = patchesQueue,
                grid = grid
            }.Schedule(findPatchesJobHandle);

            return new VoxelizationJob(applyPatchesJobHandle,
                scaledVertices,
                binsStream,
                binColsX, binColsY, binColsZ,
                binsX, binsY, binsZ,
                intersectionsStreamX, intersectionsStreamY, intersectionsStreamZ,
                intersectionColsX, intersectionColsY, intersectionColsZ,
                intersectionsX, intersectionsY, intersectionsZ,
                holes,
                patchesQueue);
        }
    }
}
