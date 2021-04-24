using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace VoxelPolygonizer
{
    public static class VoxelMeshTessellation
    {
        public readonly struct DedupedVertex
        {
            internal readonly float3 position;
            internal readonly float3 normal;
            internal readonly int material;
            internal readonly int triIndex;

            internal DedupedVertex(float3 position, float3 normal, int material, int triIndex)
            {
                this.position = position;
                this.normal = normal;
                this.material = material;
                this.triIndex = triIndex;
            }
        }

        private readonly struct SharpSection
        {
            internal readonly int startIndex, endIndex;

            internal SharpSection(int startIndex, int endIndex)
            {
                this.startIndex = startIndex;
                this.endIndex = endIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HashVertex(float3 position, float3 normal, int material, float quantization)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (int)(position.x * quantization);
                hash = hash * 23 + (int)(position.y * quantization);
                hash = hash * 23 + (int)(position.z * quantization);
                hash = hash * 23 + (int)(normal.x * quantization);
                hash = hash * 23 + (int)(normal.y * quantization);
                hash = hash * 23 + (int)(normal.z * quantization);
                hash = hash * 23 + material;
                return hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVertexEqual(
            float3 position1, float3 normal1, int material1,
            float3 position2, float3 normal2, int material2,
            float quantization)
        {
            if (material1 != material2 ||
                math.any((int3)(position1 * quantization) - (int3)(position2 * quantization)) ||
                math.any((int3)(normal1 * quantization) - (int3)(normal2 * quantization)))
            {
                return false;
            }
            return true;
        }

        internal struct DedupedVertexNode
        {
            internal readonly DedupedVertex vertex;
            internal readonly int next;

            internal DedupedVertexNode(DedupedVertex vertex, int next)
            {
                this.vertex = vertex;
                this.next = next;
            }

            internal DedupedVertexNode(DedupedVertex vertex) : this(vertex, -1)
            {

            }
        }

        public struct NativeDeduplicationCache : IDisposable
        {
            public static NativeDeduplicationCache NONE = new NativeDeduplicationCache();

            internal bool isInstantiated;
            internal NativeHashMap<int, int> table;
            internal NativeList<DedupedVertexNode> nodes;

            public NativeDeduplicationCache(Allocator allocator)
            {
                this.isInstantiated = true;
                this.table = new NativeHashMap<int, int>(0, allocator);
                this.nodes = new NativeList<DedupedVertexNode>(allocator);
            }

            public void Dispose()
            {
                this.table.Dispose();
                this.nodes.Dispose();
            }
        }

        private static bool Deduplicate(float3 position, float3 normal, int material, int freeTriIndex, NativeDeduplicationCache dedupeCache, out int dedupedTriIndex)
        {
            //TODO Find sensible value
            const float quantization = 1024f;

            int hash = HashVertex(position, normal, material, quantization);

            if (!dedupeCache.table.TryGetValue(hash, out int index))
            {
                index = -1;
            }

            if (index >= 0)
            {
                int nodeIndex = index;
                DedupedVertexNode node = dedupeCache.nodes[nodeIndex];

                while (true)
                {
                    DedupedVertex deduped = node.vertex;

                    if (IsVertexEqual(position, normal, material, deduped.position, deduped.normal, deduped.material, quantization))
                    {
                        //Equal vertex found, return index
                        dedupedTriIndex = deduped.triIndex;
                        return true;
                    }

                    if (node.next >= 0)
                    {
                        nodeIndex = node.next;
                        node = dedupeCache.nodes[nodeIndex];
                    }
                    else
                    {
                        //Hash contained but no equal vertex, append new node to linked list
                        dedupeCache.nodes[nodeIndex] = new DedupedVertexNode(deduped, dedupeCache.nodes.Length);
                        dedupeCache.nodes.Add(new DedupedVertexNode(new DedupedVertex(position, normal, material, freeTriIndex)));
                        break;
                    }
                }
            }
            else
            {
                //Hash not yet contained, add a new initial node
                Assert.IsTrue(dedupeCache.table.TryAdd(hash, dedupeCache.nodes.Length));
                dedupeCache.nodes.Add(new DedupedVertexNode(new DedupedVertex(position, normal, material, freeTriIndex)));
            }

            dedupedTriIndex = freeTriIndex;
            return false;
        }

        private static void FindSharpComponentSections(VoxelMeshComponent component, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices,
            NativeList<SharpSection> sharpSections, NativeList<float3> sharpSectionNormals)
        {
            if (component.RegularFeature)
            {
                int sharpSectionStart = -1;
                float normalSumX = 0.0f;
                float normalSumY = 0.0f;
                float normalSumZ = 0.0f;

                for (int i = 0; i < component.Size * 2; i++)
                {
                    int j = i % component.Size;

                    PackedIndex index = componentIndices[component.Index + j];

                    float3 n = componentVertices[index].Normal;

                    if (index.Is2DSharpFeature)
                    {
                        if (sharpSectionStart != -1)
                        {
                            float3 sharpSectionNormal = math.normalize(new float3(normalSumX, normalSumY, normalSumZ));

                            if (j < sharpSectionStart)
                            {
                                sharpSections.Add(new SharpSection(j, component.Size));
                                sharpSectionNormals.Add(sharpSectionNormal);

                                sharpSections.Add(new SharpSection(1, sharpSectionStart));
                                sharpSectionNormals.Add(sharpSectionNormal);
                            }
                            else
                            {
                                sharpSections.Add(new SharpSection(sharpSectionStart, j));
                                sharpSectionNormals.Add(sharpSectionNormal);
                            }
                        }

                        sharpSectionStart = j;
                        normalSumX = normalSumY = normalSumZ = 0.0f;
                    }
                    else if (sharpSectionStart != -1 && index.IsNormalSet)
                    {
                        normalSumX += n.x;
                        normalSumY += n.y;
                        normalSumZ += n.z;
                    }
                }
            }
        } 

        [BurstDiscard]
        private static void DisposeManaged<TType>(TType disposable) where TType : IDisposable
        {
            disposable.Dispose();
        }

        public static void Tessellate(VoxelMeshComponent component, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices,
            Matrix4x4 transform, NativeList<float3> vertices, int freeTriIndex, NativeList<int> triIndices,
            NativeList<float3> normals, NativeList<int> materials, NativeDeduplicationCache dedupeCache)
        {
            float3 triangleFanCenter;
            if (component.Feature)
            {
                triangleFanCenter = component.FeatureVertex;
            }
            else
            {
                triangleFanCenter = float3.zero;
                int vertexCount = 0;
                for (int i = component.Index; i < component.Index + component.Size; i++)
                {
                    triangleFanCenter += (float3)componentVertices[componentIndices[i]].Position;
                    vertexCount++;
                }
                triangleFanCenter *= 1f / vertexCount;
            }

            if (component.RegularFeature)
            {
                var sharpSections = new NativeList<SharpSection>(Allocator.Temp);
                var sharpSectionNormals = new NativeList<float3>(Allocator.Temp);

                FindSharpComponentSections(component, componentIndices, componentVertices, sharpSections, sharpSectionNormals);

                for (int i = component.Index; i < component.Index + component.Size; i++)
                {
                    PackedIndex packedIndex1 = componentIndices[i];
                    VoxelMeshComponentVertex meshVertex1 = componentVertices[packedIndex1];
                    float3 v1 = meshVertex1.Position;
                    float3 n1 = meshVertex1.Normal;
                    int m1 = meshVertex1.Material;

                    PackedIndex packedIndex2 = componentIndices[component.Index + ((i + 1 - component.Index) % component.Size)];
                    VoxelMeshComponentVertex meshVertex2 = componentVertices[packedIndex2];
                    float3 v2 = meshVertex2.Position;
                    float3 n2 = meshVertex2.Normal;
                    int m2 = meshVertex2.Material;

                    float3 faceAverageNormal = float3.zero;
                    if (packedIndex1.IsNormalSet)
                    {
                        faceAverageNormal += n1 / math.length(v1 - triangleFanCenter);
                    }
                    if (packedIndex2.IsNormalSet)
                    {
                        faceAverageNormal += n2 / math.length(v2 - triangleFanCenter);
                    }
                    faceAverageNormal = math.normalize(faceAverageNormal);

                    float3 triangleFanCenterNormal = float3.zero;
                    bool triangleFanCenterNormalSet = false;

                    //Find the normal of the section we're in
                    for (int count = sharpSections.Length, j = 0; j < count; j++)
                    {
                        SharpSection section = sharpSections[j];

                        if (i >= section.startIndex && i < section.endIndex)
                        {
                            triangleFanCenterNormal = sharpSectionNormals[j];
                            triangleFanCenterNormalSet = true;
                        }
                    }

                    if (!triangleFanCenterNormalSet)
                    {
                        triangleFanCenterNormal = faceAverageNormal;
                    }

                    var outV2 = transform.MultiplyPoint(triangleFanCenter);
                    var outN2 = transform.MultiplyVector(triangleFanCenterNormal);

                    var outV1 = transform.MultiplyPoint(v1);
                    float3 outN1;
                    if (packedIndex1.IsNormalSet)
                    {
                        outN1 = transform.MultiplyVector(n1);
                    }
                    else
                    {
                        outN1 = outN2;
                    }
                    if (Deduplicate(outV1, outN1, m1, freeTriIndex, dedupeCache, out int outI1))
                    {
                        triIndices.Add(outI1);
                    }
                    else
                    {
                        vertices.Add(outV1);
                        normals.Add(outN1);
                        materials.Add(m1);
                        triIndices.Add(freeTriIndex);
                        freeTriIndex++;
                    }

                    if (Deduplicate(outV2, outN2, m1, freeTriIndex, dedupeCache, out int outI2))
                    {
                        triIndices.Add(outI2);
                    }
                    else
                    {
                        vertices.Add(outV2);
                        normals.Add(outN2);
                        materials.Add(m1);
                        triIndices.Add(freeTriIndex);
                        freeTriIndex++;
                    }

                    var outV3 = transform.MultiplyPoint(v2);
                    float3 outN3;
                    if (packedIndex2.IsNormalSet)
                    {
                        outN3 = transform.MultiplyVector(n2);
                    }
                    else
                    {
                        outN3 = outN2;
                    }
                    if (Deduplicate(outV3, outN3, m2, freeTriIndex, dedupeCache, out int outI3))
                    {
                        triIndices.Add(outI3);
                    }
                    else
                    {
                        vertices.Add(outV3);
                        normals.Add(outN3);
                        materials.Add(m2);
                        triIndices.Add(freeTriIndex);
                        freeTriIndex++;
                    }
                }

                DisposeManaged(sharpSections);
                DisposeManaged(sharpSectionNormals);
            }
            else
            {
                float3 fullAverageNormal = float3.zero;

                for (int i = component.Index; i < component.Index + component.Size; i++)
                {
                    PackedIndex packedIndex = componentIndices[i];
                    if (packedIndex.IsNormalSet)
                    {
                        VoxelMeshComponentVertex meshVertex = componentVertices[packedIndex];
                        fullAverageNormal += meshVertex.Normal / math.length(meshVertex.Position - triangleFanCenter);
                    }
                }

                fullAverageNormal = math.normalize(fullAverageNormal);

                for (int i = component.Index; i < component.Index + component.Size; i++)
                {
                    PackedIndex packedIndex1 = componentIndices[i];
                    VoxelMeshComponentVertex meshVertex1 = componentVertices[packedIndex1];
                    float3 v1 = meshVertex1.Position;
                    float3 n1 = meshVertex1.Normal;
                    int m1 = meshVertex1.Material;

                    PackedIndex packedIndex2 = componentIndices[component.Index + ((i + 1 - component.Index) % component.Size)];
                    VoxelMeshComponentVertex meshVertex2 = componentVertices[packedIndex2];
                    float3 v2 = meshVertex2.Position;
                    float3 n2 = meshVertex2.Normal;
                    int m2 = meshVertex2.Material;

                    float3 faceAverageNormal = float3.zero;
                    if (packedIndex1.IsNormalSet)
                    {
                        faceAverageNormal += n1 / math.length(v1 - triangleFanCenter);
                    }
                    if (packedIndex2.IsNormalSet)
                    {
                        faceAverageNormal += n2 / math.length(v2 - triangleFanCenter);
                    }
                    faceAverageNormal = math.normalize(faceAverageNormal);

                    var outV1 = transform.MultiplyPoint(v1);
                    float3 outN1;
                    if (packedIndex1.IsNormalSet)
                    {
                        outN1 = transform.MultiplyVector(n1);
                    }
                    else
                    {
                        outN1 = transform.MultiplyVector(faceAverageNormal);
                    }
                    if (Deduplicate(outV1, outN1, m1, freeTriIndex, dedupeCache, out int outI1))
                    {
                        triIndices.Add(outI1);
                    }
                    else
                    {
                        vertices.Add(outV1);
                        normals.Add(outN1);
                        materials.Add(m1);
                        triIndices.Add(freeTriIndex);
                        freeTriIndex++;
                    }

                    var outV2 = transform.MultiplyPoint(triangleFanCenter);
                    var outN2 = transform.MultiplyVector(fullAverageNormal);
                    if (Deduplicate(outV2, outN2, m1, freeTriIndex, dedupeCache, out int outI2))
                    {
                        triIndices.Add(outI2);
                    }
                    else
                    {
                        vertices.Add(outV2);
                        normals.Add(outN2);
                        materials.Add(m1);
                        triIndices.Add(freeTriIndex);
                        freeTriIndex++;
                    }

                    var outV3 = transform.MultiplyPoint(v2);
                    float3 outN3;
                    if (packedIndex2.IsNormalSet)
                    {
                        outN3 = transform.MultiplyVector(n2);
                    }
                    else
                    {
                        outN3 = transform.MultiplyVector(faceAverageNormal);
                    }
                    if (Deduplicate(outV3, outN3, m2, freeTriIndex, dedupeCache, out int outI3))
                    {
                        triIndices.Add(outI3);
                    }
                    else
                    {
                        vertices.Add(outV3);
                        normals.Add(outN3);
                        materials.Add(m2);
                        triIndices.Add(freeTriIndex);
                        freeTriIndex++;
                    }
                }
            }
        }

        public interface IMaterialColorMap
        {
            Color32 GetColor(int material);
        }

        public static void Tessellate<TColorMap>(NativeList<VoxelMeshComponent> components, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices,
            Matrix4x4 transform, NativeList<float3> vertices, NativeList<int> triIndices,
            NativeList<float3> normals, NativeList<int> materials, TColorMap materialColors, NativeList<Color32> colors, NativeDeduplicationCache dedupeCache)
            where TColorMap : struct, IMaterialColorMap
        {
            var newMaterials = new NativeList<int>(Allocator.Temp);

            for (int count = components.Length, i = 0; i < count; i++)
            {
                Tessellate(components[i], componentIndices, componentVertices, transform, vertices, vertices.Length, triIndices, normals, newMaterials, dedupeCache);
            }

            materials.AddRange(newMaterials);

            for (int count = newMaterials.Length, i = 0; i < count; i++)
            {
                colors.Add(materialColors.GetColor(newMaterials[i]));
            }

            DisposeManaged(newMaterials);
        }

        /*[BurstDiscard]
        public static void Tessellate(NativeList<VoxelMeshComponent> components, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices,
            Matrix4x4 transform, Mesh voxelMesh, Func<int, Color32> materialColors = null, Dictionary<int, List<DedupedVertex>> dedupeTable = null)
        {
            var vertices = new List<float3>();
            var indices = new List<int>();
            var materials = new List<int>();
            var colors = new List<Color32>();
            var normals = new List<float3>();

            Tessellate(components, componentIndices, componentVertices, transform, vertices, indices, normals, materials, colors, materialColors, dedupeTable);

            voxelMesh.SetVertices(vertices);
            voxelMesh.SetNormals(normals);
            voxelMesh.SetTriangles(indices, 0);
            if (colors.Count > 0)
            {
                voxelMesh.SetColors(colors);
            }
        }*/

        [BurstDiscard]
        public static void DrawDebugGizmos<TCell, TColorMap>(
            Vector3 position, Matrix4x4 transform, List<VoxelMeshComponent> components, List<PackedIndex> componentIndices,
            List<VoxelMeshComponentVertex> componentVertices, ref TCell voxelCell, TColorMap materialColors, Mesh voxelMesh = null)
            where TCell : struct, IVoxelCell
            where TColorMap : struct, IMaterialColorMap
        {
            Matrix4x4 translatedTransform = Matrix4x4.Translate(position) * transform;

            Gizmos.matrix = translatedTransform;

            if (voxelMesh != null)
            {
                Gizmos.color = new Color(1, 0, 0);
                Gizmos.DrawWireMesh(voxelMesh);
            }

            Gizmos.matrix = transform;

            for (int i = 0; i < components.Count; i++)
            {
                var component = components[i];

                if (component.RegularFeature)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawSphere(component.FeatureVertex, 0.015f);

                    Gizmos.color = new Color(1, 1, 0);
                    Gizmos.DrawSphere(component.FeatureVertex, 0.01f);
                }
                else if (component.MaterialTransitionFeature)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawSphere(component.FeatureVertex, 0.015f);

                    Gizmos.color = new Color(0.2f, 0.8f, 1);
                    Gizmos.DrawSphere(component.FeatureVertex, 0.01f);
                }

                //Centroid/Feature point normal
                if (!component.RegularFeature)
                {
                    float3 triangleFanCenter = float3.zero;
                    int vertexCount = 0;
                    for (int j = component.Index; j < component.Index + component.Size; j++)
                    {
                        triangleFanCenter += (float3)componentVertices[componentIndices[j]].Position;
                        vertexCount++;
                    }
                    triangleFanCenter *= 1f / vertexCount;

                    float3 fullAverageNormal = float3.zero;

                    for (int j = component.Index; j < component.Index + component.Size; j++)
                    {
                        PackedIndex packedIndex = componentIndices[j];
                        if (packedIndex.IsNormalSet)
                        {
                            VoxelMeshComponentVertex meshVertex = componentVertices[packedIndex];
                            fullAverageNormal += meshVertex.Normal / math.length(meshVertex.Position - triangleFanCenter);
                        }
                    }

                    fullAverageNormal = math.normalize(fullAverageNormal);

                    Gizmos.color = new Color(1, 0, 1);
                    Gizmos.DrawRay(component.FeatureVertex, fullAverageNormal * 0.25f);
                }

                //TODO Render individual normals for RegularFeatures

                for (int j = component.Index; j < component.Index + component.Size; j++)
                {
                    var packedIndex = componentIndices[j];

                    if (packedIndex.IsMaterialTransition)
                    {
                        var meshVertex = componentVertices[packedIndex];

                        Gizmos.color = Color.black;
                        Gizmos.DrawSphere(meshVertex.Position, 0.015f);

                        Gizmos.color = new Color(0, 1, 1);
                        Gizmos.DrawSphere(meshVertex.Position, 0.01f);

                        Gizmos.DrawRay(meshVertex.Position, meshVertex.Normal * 0.25f);
                    }
                }
            }

            float width = voxelCell.GetWidth();
            float height = voxelCell.GetHeight();
            float depth = voxelCell.GetDepth();

            Gizmos.matrix = translatedTransform;

            Gizmos.color = new Color(1, 0, 0);
            Gizmos.DrawWireCube(new Vector3(width / 2f, height / 2f, depth / 2f), new Vector3(width, height, depth));

            Gizmos.matrix = transform;

            foreach (VoxelCellFace face in Enum.GetValues(typeof(VoxelCellFace)))
            {
                var cellCount = voxelCell.GetCellFaceCount(face);

                for (int k = 0; k < cellCount; k++)
                {
                    int cell = voxelCell.GetCellFace(face, k);
                    CellInfo info = voxelCell.GetInfo(cell);
                    var edges = voxelCell.GetEdges(cell);
                    var materials = voxelCell.GetMaterials(cell);

                    for (int i = 0; i < 4; i++)
                    {
                        var edge = edges[i];

                        float cx, cy;
                        switch (i)
                        {
                            default:
                            case 0:
                                cx = 0;
                                cy = 0;
                                break;
                            case 1:
                                cx = info.Width;
                                cy = 0;
                                break;
                            case 2:
                                cx = info.Width;
                                cy = info.Height;
                                break;
                            case 3:
                                cx = 0;
                                cy = info.Height;
                                break;
                        }

                        Vector3 cornerPos = info.Position + face.BasisX() * cx + face.BasisY() * cy;

                        Gizmos.color = Color.black;
                        Gizmos.DrawCube(cornerPos, 0.06f * Vector3.one);

                        Gizmos.color = materialColors.GetColor(materials[i]);
                        Gizmos.DrawCube(cornerPos, 0.05f * Vector3.one);

                        if (voxelCell.HasIntersection(cell, edge))
                        {
                            var intersection = voxelCell.GetIntersection(cell, edge);

                            float ix, iy;
                            switch (i)
                            {
                                default:
                                case 0:
                                    ix = intersection;
                                    iy = 0;
                                    break;
                                case 1:
                                    ix = info.Width;
                                    iy = intersection;
                                    break;
                                case 2:
                                    ix = info.Width - intersection;
                                    iy = info.Height;
                                    break;
                                case 3:
                                    ix = 0;
                                    iy = info.Height - intersection;
                                    break;
                            }

                            Vector3 intersectionPos = info.Position + face.BasisX() * ix + face.BasisY() * iy;

                            float3 edgeStart, edgeEnd;
                            switch (i)
                            {
                                default:
                                case 0:
                                    edgeStart = info.Position;
                                    edgeEnd = info.Position + face.BasisX() * 1;
                                    break;
                                case 1:
                                    edgeStart = info.Position + face.BasisX() * 1;
                                    edgeEnd = info.Position + face.BasisX() * 1 + face.BasisY() * 1;
                                    break;
                                case 2:
                                    edgeStart = info.Position + face.BasisX() * 1 + face.BasisY() * 1;
                                    edgeEnd = info.Position + face.BasisY() * 1;
                                    break;
                                case 3:
                                    edgeStart = info.Position + face.BasisY() * 1;
                                    edgeEnd = info.Position;
                                    break;
                            }
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawRay(edgeStart, edgeEnd - edgeStart);

                            Gizmos.color = Color.black;
                            Gizmos.DrawSphere(intersectionPos, 0.015f);

                            Gizmos.color = new Color(1, 0, 1);
                            Gizmos.DrawSphere(intersectionPos, 0.01f);

                            Vector3 normal = voxelCell.GetNormal(cell, edge);

                            Gizmos.DrawRay(intersectionPos, normal * 0.25f);

                            Camera cam = Camera.current;
                            if (cam != null)
                            {
                                int facing = GetFacing(cam.transform.forward);

                                Vector3 planeX;
                                Vector3 planeY;

                                Gizmos.color = new Color(0, 0, 0);

                                switch (facing)
                                {
                                    default:
                                    case 0:
                                        planeX = new Vector3(0, 0, 1);
                                        planeY = new Vector3(0, 1, 0);
                                        Gizmos.DrawCube(intersectionPos, new Vector3(0.001f, 0.085f, 0.085f));
                                        Gizmos.color = new Color(1, 0, 0);
                                        Gizmos.DrawCube(intersectionPos, new Vector3(0.001f, 0.075f, 0.075f));
                                        break;
                                    case 1:
                                        planeX = new Vector3(0, 0, 1);
                                        planeY = new Vector3(1, 0, 0);
                                        Gizmos.DrawCube(intersectionPos, new Vector3(0.085f, 0.001f, 0.085f));
                                        Gizmos.color = new Color(0, 1, 0);
                                        Gizmos.DrawCube(intersectionPos, new Vector3(0.075f, 0.001f, 0.075f));
                                        break;
                                    case 2:
                                        planeX = new Vector3(1, 0, 0);
                                        planeY = new Vector3(0, 1, 0);
                                        Gizmos.DrawCube(intersectionPos, new Vector3(0.085f, 0.085f, 0.001f));
                                        Gizmos.color = new Color(0, 0, 1);
                                        Gizmos.DrawCube(intersectionPos, new Vector3(0.075f, 0.075f, 0.001f));
                                        break;
                                }

                                Vector3 projectedNormal = planeX * Vector3.Dot(planeX, normal) + planeY * Vector3.Dot(planeY, normal);
                                projectedNormal.Normalize();

                                Gizmos.DrawRay(intersectionPos, projectedNormal * 0.15f);
                            }
                        }
                    }
                }
            }

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(1, 1, 1);
        }

        private static int GetFacing(Vector3 vec)
        {
            int facing = 0;
            float maxValue = 0;
            for (int i = 0; i < 3; i++)
            {
                float abs = Mathf.Abs(vec[i]);
                if (abs > maxValue)
                {
                    maxValue = abs;
                    facing = i;
                }
            }
            return facing;
        }
    }
}
