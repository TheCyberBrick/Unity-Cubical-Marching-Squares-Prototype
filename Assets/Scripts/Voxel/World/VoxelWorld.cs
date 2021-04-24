using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    public class VoxelWorld<TIndexer> : IDisposable
        where TIndexer : struct, IIndexer
    {
        private readonly Dictionary<ChunkPos, VoxelChunk<TIndexer>> chunks = new Dictionary<ChunkPos, VoxelChunk<TIndexer>>();

        public Transform Transform
        {
            get;
            set;
        }

        public int ChunkSize
        {
            private set;
            get;
        }

        public CMSProperties CMSProperties
        {
            private set;
            get;
        }

        public IndexerFactory<TIndexer> IndexerFactory
        {
            private set;
            get;
        }


        public VoxelWorld(int chunkSize, CMSProperties cmsProperties, Transform transform, IndexerFactory<TIndexer> indexerFactory)
        {
            ChunkSize = chunkSize;
            CMSProperties = cmsProperties;
            Transform = transform;
            IndexerFactory = indexerFactory;
        }

        private Vector3 TransformPointToLocalSpace(Vector3 vec)
        {
            return Transform.InverseTransformPoint(vec);
        }

        private Vector3 TransformDirToLocalSpace(Vector3 vec)
        {
            return Transform.InverseTransformDirection(vec);
        }

        private Quaternion TransformQuatToLocalSpace(Quaternion rot)
        {
            return Quaternion.Inverse(Transform.rotation) * rot;
        }

        public VoxelChunk<TIndexer> GetChunk(ChunkPos pos)
        {
            chunks.TryGetValue(pos, out VoxelChunk<TIndexer> chunk);
            return chunk;
        }

        public delegate void VoxelEditConsumer<TEditIndexer>(VoxelEdit<TEditIndexer> edit) where TEditIndexer : struct, IIndexer;

        public void ApplyGrid<TGridIndexer>(int x, int y, int z, NativeArray3D<Voxel, TGridIndexer> grid, bool propagatePadding, bool includePadding, VoxelEditConsumer<TIndexer> edits, bool writeToChunks = true, bool writeUnsetVoxels = false)
            where TGridIndexer : struct, IIndexer
        {
            int minX = Mathf.FloorToInt((float)x / ChunkSize);
            int minY = Mathf.FloorToInt((float)y / ChunkSize);
            int minZ = Mathf.FloorToInt((float)z / ChunkSize);

            int maxX = Mathf.FloorToInt((float)(x + grid.Length(0)) / ChunkSize);
            int maxY = Mathf.FloorToInt((float)(y + grid.Length(1)) / ChunkSize);
            int maxZ = Mathf.FloorToInt((float)(z + grid.Length(2)) / ChunkSize);

            var handles = new List<VoxelChunk<TIndexer>.FinalizeChange>();

            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (edits != null)
            {
                var snapshots = new List<VoxelChunk<TIndexer>.Snapshot>();

                //Include padding
                int minX2 = Mathf.FloorToInt((float)(x - 1) / ChunkSize);
                int minY2 = Mathf.FloorToInt((float)(y - 1) / ChunkSize);
                int minZ2 = Mathf.FloorToInt((float)(z - 1) / ChunkSize);

                int maxX2 = Mathf.FloorToInt((float)(x + grid.Length(0) + 1) / ChunkSize);
                int maxY2 = Mathf.FloorToInt((float)(y + grid.Length(1) + 1) / ChunkSize);
                int maxZ2 = Mathf.FloorToInt((float)(z + grid.Length(2) + 1) / ChunkSize);

                var snapshotChunks = new List<VoxelChunk<TIndexer>>();

                for (int cx = minX2; cx <= maxX2; cx++)
                {
                    for (int cy = minY2; cy <= maxY2; cy++)
                    {
                        for (int cz = minZ2; cz <= maxZ2; cz++)
                        {
                            ChunkPos chunkPos = ChunkPos.FromChunk(cx, cy, cz);

                            chunks.TryGetValue(chunkPos, out VoxelChunk<TIndexer> chunk);
                            if (chunk != null)
                            {
                                snapshots.Add(chunk.ScheduleSnapshot());
                            }
                            else
                            {
                                snapshotChunks.Add(new VoxelChunk<TIndexer>(this, chunkPos, ChunkSize, IndexerFactory));
                            }
                        }
                    }
                }

                //Finalize clone jobs
                foreach (VoxelChunk<TIndexer>.Snapshot snapshot in snapshots)
                {
                    snapshot.handle.Complete();
                    snapshotChunks.Add(snapshot.chunk);
                }

                //Produce edit
                edits(new VoxelEdit<TIndexer>(this, snapshotChunks));
            }

            if (writeToChunks)
            {
                var changes = new List<VoxelChunk<TIndexer>.Change>();

                //Schedule all jobs
                for (int cx = minX; cx <= maxX; cx++)
                {
                    for (int cy = minY; cy <= maxY; cy++)
                    {
                        for (int cz = minZ; cz <= maxZ; cz++)
                        {
                            ChunkPos chunkPos = ChunkPos.FromChunk(cx, cy, cz);

                            chunks.TryGetValue(chunkPos, out VoxelChunk<TIndexer> chunk);
                            if (chunk == null)
                            {
                                chunks[chunkPos] = chunk = new VoxelChunk<TIndexer>(this, chunkPos, ChunkSize, IndexerFactory);
                            }

                            var gx = (cx - minX) * ChunkSize;
                            var gy = (cy - minY) * ChunkSize;
                            var gz = (cz - minZ) * ChunkSize;

                            changes.Add(chunk.ScheduleGrid(0, 0, 0, gx, gy, gz, grid, propagatePadding, includePadding, writeUnsetVoxels));
                        }
                    }
                }

                //Wait and finalize jobs
                foreach (var change in changes)
                {
                    change.handle.Complete();
                }

                //Finalize
                foreach (var change in changes)
                {
                    change.finalize();
                }
            }
        }

        /// <summary>
        /// Applies the specified signed distance field function to the world.
        /// The SDF is applied in local space, i.e. 1 voxel = 1 unit on the signed distance field!
        /// </summary>
        /// <param name="pos">World position</param>
        /// <param name="rot">World rotation</param>
        /// <param name="sdf">Signed distance field function</param>
        /// <param name="material">Material to be added</param>
        /// <param name="replace">Whether only solid material should be replaced</param>
        /// <param name="edits">Consumes the voxel edit. Can be null if no voxel edits should be stored</param>
        public void ApplySdf<TSdf>(Vector3 pos, Quaternion rot, TSdf sdf, int material, bool replace, VoxelEditConsumer<TIndexer> edits)
            where TSdf : struct, ISdf
        {
            pos = TransformPointToLocalSpace(pos);
            rot = TransformQuatToLocalSpace(rot);

            Debug.Log("Apply sdf at: " + pos);

            var transformedSdf = new TransformSDF<TSdf>(Matrix4x4.TRS(pos, rot, Vector3.one), sdf);

            Vector3 minBound = transformedSdf.Min();
            Vector3 maxBound = transformedSdf.Max();

            int minX = Mathf.FloorToInt((float)minBound.x / ChunkSize);
            int minY = Mathf.FloorToInt((float)minBound.y / ChunkSize);
            int minZ = Mathf.FloorToInt((float)minBound.z / ChunkSize);

            int maxX = Mathf.FloorToInt((float)maxBound.x / ChunkSize);
            int maxY = Mathf.FloorToInt((float)maxBound.y / ChunkSize);
            int maxZ = Mathf.FloorToInt((float)maxBound.z / ChunkSize);

            var changes = new List<VoxelChunk<TIndexer>.Change>();

            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (edits != null)
            {
                var snapshots = new List<VoxelChunk<TIndexer>.Snapshot>();

                //Include padding
                int minX2 = Mathf.FloorToInt((float)(minBound.x - 1) / ChunkSize);
                int minY2 = Mathf.FloorToInt((float)(minBound.y - 1) / ChunkSize);
                int minZ2 = Mathf.FloorToInt((float)(minBound.z - 1) / ChunkSize);

                int maxX2 = Mathf.FloorToInt((float)(maxBound.x + 1) / ChunkSize);
                int maxY2 = Mathf.FloorToInt((float)(maxBound.y + 1) / ChunkSize);
                int maxZ2 = Mathf.FloorToInt((float)(maxBound.z + 1) / ChunkSize);

                var snapshotChunks = new List<VoxelChunk<TIndexer>>();

                for (int cx = minX2; cx <= maxX2; cx++)
                {
                    for (int cy = minY2; cy <= maxY2; cy++)
                    {
                        for (int cz = minZ2; cz <= maxZ2; cz++)
                        {
                            ChunkPos chunkPos = ChunkPos.FromChunk(cx, cy, cz);

                            chunks.TryGetValue(chunkPos, out VoxelChunk<TIndexer> chunk);
                            if (chunk != null)
                            {
                                snapshots.Add(chunk.ScheduleSnapshot());
                            }
                            else
                            {
                                snapshotChunks.Add(new VoxelChunk<TIndexer>(this, chunkPos, ChunkSize, IndexerFactory));
                            }
                        }
                    }
                }

                //Finalize clone jobs
                foreach (VoxelChunk<TIndexer>.Snapshot snapshot in snapshots)
                {
                    snapshot.handle.Complete();
                    snapshotChunks.Add(snapshot.chunk);
                }

                //Produce edit
                edits(new VoxelEdit<TIndexer>(this, snapshotChunks));
            }

            //Schedule all jobs
            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cy = minY; cy <= maxY; cy++)
                {
                    for (int cz = minZ; cz <= maxZ; cz++)
                    {
                        ChunkPos chunkPos = ChunkPos.FromChunk(cx, cy, cz);

                        chunks.TryGetValue(chunkPos, out VoxelChunk<TIndexer> chunk);
                        if (chunk == null)
                        {
                            chunks[chunkPos] = chunk = new VoxelChunk<TIndexer>(this, chunkPos, ChunkSize, IndexerFactory);
                        }

                        changes.Add(chunk.ScheduleSdf(-cx * ChunkSize, -cy * ChunkSize, -cz * ChunkSize, transformedSdf, material, replace));
                    }
                }
            }

            //Wait and finalize jobs
            foreach (var change in changes)
            {
                change.handle.Complete();
            }

            //Finalize
            foreach (var change in changes)
            {
                change.finalize();
            }

            watch.Stop();

            string text = "Applied SDF to " + changes.Count + " voxel chunks in " + watch.ElapsedMilliseconds + "ms. Avg: " + (watch.ElapsedMilliseconds / (float)changes.Count) + "ms.";
            Debug.Log(text);
        }

        public readonly struct RayCastResult
        {
            public readonly Vector3 pos;
            public readonly Vector3 sidePos;
            public readonly VoxelChunk<TIndexer> chunk;

            public RayCastResult(Vector3 pos, Vector3 sidePos, VoxelChunk<TIndexer> chunk)
            {
                this.pos = pos;
                this.sidePos = sidePos;
                this.chunk = chunk;
            }
        }

        public bool RayCast(Vector3 pos, Vector3 dir, float dst, out RayCastResult result)
        {
            pos = TransformPointToLocalSpace(pos);
            dir = TransformDirToLocalSpace(dir);

            const float step = 0.1f;

            int prevX = int.MaxValue;
            int prevY = int.MaxValue;
            int prevZ = int.MaxValue;

            Vector3 stepOffset = dir.normalized * step;

            for (int i = 0; i < dst / step; i++)
            {
                int x = (int)Mathf.Floor(pos.x);
                int y = (int)Mathf.Floor(pos.y);
                int z = (int)Mathf.Floor(pos.z);

                if (x != prevX || y != prevY || z != prevZ)
                {
                    for (int zo = 0; zo < 2; zo++)
                    {
                        for (int yo = 0; yo < 2; yo++)
                        {
                            for (int xo = 0; xo < 2; xo++)
                            {
                                int bx = x + xo;
                                int by = y + yo;
                                int bz = z + zo;

                                var chunk = GetChunk(ChunkPos.FromVoxel(bx, by, bz, ChunkSize));

                                if (chunk != null)
                                {
                                    int material = chunk.GetMaterial(((bx % ChunkSize) + ChunkSize) % ChunkSize, ((by % ChunkSize) + ChunkSize) % ChunkSize, ((bz % ChunkSize) + ChunkSize) % ChunkSize);
                                    if (material != 0)
                                    {
                                        result = new RayCastResult(new Vector3(x, y, z), new Vector3(prevX, prevY, prevZ), chunk);
                                        return true;
                                    }
                                }
                            }
                        }
                    }

                    prevX = x;
                    prevY = y;
                    prevZ = z;
                }

                pos += stepOffset;
            }

            result = new RayCastResult(Vector3.zero, Vector3.zero, null);
            return false;
        }

        public void Clear()
        {
            foreach (var chunk in chunks.Values)
            {
                chunk.Dispose();
            }
            chunks.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        public void Update()
        {
            var handles = new List<VoxelChunk<TIndexer>.FinalizeBuild>();

            System.Diagnostics.Stopwatch watch = null;

            //Schedule all rebuild jobs
            foreach (ChunkPos pos in chunks.Keys)
            {
                VoxelChunk<TIndexer> chunk = chunks[pos];

                if (chunk.mesh == null || chunk.NeedsRebuild)
                {
                    if (watch == null)
                    {
                        watch = System.Diagnostics.Stopwatch.StartNew();
                    }
                    handles.Add(chunk.ScheduleBuild());
                }
            }

            //Wait and finalize jobs
            foreach (var handle in handles)
            {
                handle();
            }

            if (watch != null)
            {
                watch.Stop();

                string text = "Polygonized " + handles.Count + " voxel chunks in " + watch.ElapsedMilliseconds + "ms. Avg: " + (watch.ElapsedMilliseconds / (float)handles.Count) + "ms.";
                Debug.Log(text);
            }
        }

        public void Render(Matrix4x4 renderTransform, Material material)
        {
            foreach (ChunkPos pos in chunks.Keys)
            {
                VoxelChunk<TIndexer> chunk = chunks[pos];
                if (chunk.mesh != null)
                {
                    Graphics.DrawMesh(chunk.mesh, renderTransform * Matrix4x4.TRS(Transform.position, Transform.rotation, Transform.lossyScale) * Matrix4x4.Translate(new Vector3(pos.x * ChunkSize, pos.y * ChunkSize, pos.z * ChunkSize)), material, 0);
                }
            }
        }
    }
}