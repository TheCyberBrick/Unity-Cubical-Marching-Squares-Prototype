using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace VoxelPolygonizer.CMS
{
    internal struct Segment
    {
        internal readonly int index;
        internal readonly int size;
        internal readonly int materialTransitionIndex;
        internal readonly int materialTransitionMaterial1;
        internal readonly int materialTransitionMaterial2;
        internal readonly float3 materialTransitionSurfaceNormal;
        internal readonly int startEdge; 
        internal readonly int endEdge;

        internal Segment(int index, int size, int materialTransitionIndex,
            float3 materialTransitionSurfaceNormal, int materialTransitionMaterial1, int materialTransitionMaterial2,
            int startEdge, int endEdge)
        {
            this.index = index;
            this.size = size;
            this.materialTransitionIndex = materialTransitionIndex;
            this.materialTransitionSurfaceNormal = materialTransitionSurfaceNormal;
            this.materialTransitionMaterial1 = materialTransitionMaterial1;
            this.materialTransitionMaterial2 = materialTransitionMaterial2;
            this.startEdge = startEdge;
            this.endEdge = endEdge;
        }
    }

    internal readonly struct SegmentVertex
    {
        internal readonly float2 Pos;
        internal readonly float3 Normal;
        internal readonly int Material;

        internal SegmentVertex(float2 pos, float3 normal, int material)
        {
            this.Pos = pos;
            this.Normal = normal;
            this.Material = material;
        }
    }

    internal readonly struct MaterialTransition
    {
        internal readonly float x, y, dx, dy;
        internal readonly float3 normal;
        internal readonly int material1, material2;

        internal MaterialTransition(float x, float y, float dx, float dy, float3 normal,
            int material1, int material2)
        {
            this.x = x;
            this.y = y;
            this.dx = dx;
            this.dy = dy;
            this.normal = normal;
            this.material1 = material1;
            this.material2 = material2;
        }
    }

    public readonly struct ComponentMaterials
    {
        //TODO Implement this
    }

    public struct NativeMemoryCache : IDisposable
    {
        internal NativeList<Segment> segments;
        internal NativeList<float3> segmentVertices;
        internal NativeList<float3> segmentNormals;
        internal NativeList<int> segmentMaterials;
        internal NativeList<Segment> component;
        internal NativeList<float3> transitionSurfaceNormals;
        internal NativeArray<SegmentVertex> segmentVertexArray;
        internal NativeList<MaterialTransition> materialTransitions;
        internal NativeList<int> cellMaterials;
        internal NativeList<float3> points;
        internal NativeList<float3> normals;

        public NativeMemoryCache(Allocator allocator)
        {
            segments = new NativeList<Segment>(allocator);
            segmentVertices = new NativeList<float3>(allocator);
            segmentNormals = new NativeList<float3>(allocator);
            segmentMaterials = new NativeList<int>(allocator);
            component = new NativeList<Segment>(allocator);
            transitionSurfaceNormals = new NativeList<float3>(allocator);
            segmentVertexArray = new NativeArray<SegmentVertex>(4 /*max num. of vertices per segment*/, allocator);
            materialTransitions = new NativeList<MaterialTransition>(4, allocator);
            cellMaterials = new NativeList<int>(4, allocator);
            points = new NativeList<float3>(32, allocator);
            normals = new NativeList<float3>(32, allocator);
        }

        public void Dispose()
        {
            segments.Dispose();
            segmentVertices.Dispose();
            segmentNormals.Dispose();
            segmentMaterials.Dispose();
            component.Dispose();
            transitionSurfaceNormals.Dispose();
            segmentVertexArray.Dispose();
            materialTransitions.Dispose();
            cellMaterials.Dispose();
            points.Dispose();
            normals.Dispose();
        }
    }

    public struct CMSVoxelPolygonizer<TCell, TProperties, TSolver, TSolver2D> : IVoxelPolygonizer<TCell>
        where TCell : struct, IVoxelCell
        where TProperties : struct, ICMSProperties
        where TSolver : struct, ISharpFeatureSolver<TCell>
        where TSolver2D : struct, ISharpFeatureSolver2D<TCell>
    {
        private readonly struct TaggedEdge
        {
            internal readonly int Edge;
            internal readonly int Material;
            internal readonly float2 Intersection;

            internal TaggedEdge(int edge, int material, float2 intersection)
            {
                this.Edge = edge;
                this.Material = material;
                this.Intersection = intersection;
            }
        }

        private readonly NativeMemoryCache memoryCache;

        public CMSVoxelPolygonizer(TProperties properties, TSolver solver, TSolver2D solver2d, NativeMemoryCache cache)
        {
            Properties = properties;
            SharpFeatureSolver = solver;
            SharpFeatureSolver2D = solver2d;
            memoryCache = cache;
        }

        public TSolver SharpFeatureSolver
        {
            get; set;
        }

        public TSolver2D SharpFeatureSolver2D
        {
            get; set;
        }

        public TProperties Properties
        {
            get; set;
        }

        public void Polygonize(TCell cell, NativeList<VoxelMeshComponent> components, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices)
        {
            var segments = memoryCache.segments;
            segments.Clear();

            var segmentVertices = memoryCache.segmentVertices;
            segmentVertices.Clear();

            var segmentNormals = memoryCache.segmentNormals;
            segmentNormals.Clear();

            var segmentMaterials = memoryCache.segmentMaterials;
            segmentMaterials.Clear();

            for (int k = 0; k < CMSTables.VoxelCellFaces.Length; k++)
            {
                VoxelCellFace face = CMSTables.VoxelCellFaces[k];
                int cellCount = cell.GetCellFaceCount(face);
                for (int i = 0; i < cellCount; i++)
                {
                    GenerateFaceCellSegments(cell, cell.GetCellFace(face, cellCount), face, segments, segmentVertices, segmentNormals, segmentMaterials);
                }
            }

            GenerateComponents(cell, segments, segmentVertices, segmentNormals, segmentMaterials, components, componentIndices, componentVertices);

            //TODO Fix outliers by pulling them towards the component's geometric center until they're inside their cell?

            //TODO 3D disambiguation by checking for overlapping volumes, and if overlapping join the two components as cylinder
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 CellSpaceToWorldSpace(float3 cellPos, float3 basisX, float3 basisY, float x, float y)
        {
            return cellPos + basisX * x + basisY * y;
        }

        private VoxelMeshComponent Reconstruct3DSharpFeature(
            TCell cell, int componentIndex, int componentSize, NativeList<PackedIndex> componentIndices,
            NativeList<VoxelMeshComponentVertex> componentVertices, NativeList<float3> transitionSurfaceNormals)
        {
            float theta = 1.0F;
            float transitionTheta = 1.0F;

            float3 n0 = float3.zero;
            float3 n1 = float3.zero;

            int regularSamples = 0;
            int transitionSamples = 0;

            float4 vertMean = new float4(0.0f);

            for (int i = componentIndex; i < componentIndex + componentSize; i++)
            {
                PackedIndex packedIndex1 = componentIndices[i];
                int index1 = packedIndex1;
                bool is2DFeature1 = packedIndex1.Is2DSharpFeature;

                if (!is2DFeature1)
                {
                    bool isTransition1 = packedIndex1.IsMaterialTransition;

                    VoxelMeshComponentVertex meshVertex1 = componentVertices[index1];

                    float3 vertex1 = meshVertex1.Position;

                    vertMean += new float4(vertex1, 1.0f);

                    if (packedIndex1.IsNormalSet)
                    {
                        var normal1v = meshVertex1.Normal;

                        if (!isTransition1)
                        {
                            regularSamples++;
                        }
                        else
                        {
                            transitionSamples++;
                        }

                        for (int j = componentIndex; j < componentIndex + componentSize; j++)
                        {
                            PackedIndex packedIndex2 = componentIndices[j];
                            int index2 = packedIndex2;
                            bool is2DFeature2 = packedIndex2.Is2DSharpFeature;

                            if (!is2DFeature2 && i != j)
                            {
                                if (packedIndex2.IsNormalSet)
                                {
                                    VoxelMeshComponentVertex meshVertex2 = componentVertices[index2];

                                    float3 vertex2 = meshVertex1.Position;

                                    var normal2v = meshVertex2.Normal;
                                    bool isTransition2 = packedIndex2.IsMaterialTransition;

                                    float ctheta = math.dot(normal1v, normal2v);

                                    if (!isTransition1 && !isTransition2)
                                    {
                                        if (ctheta <= theta)
                                        {
                                            n0 = normal1v;
                                            n1 = normal2v;
                                            theta = ctheta;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (regularSamples + transitionSamples <= 2)
            {
                //Not enough samples for a 3D sharp feature!
                //return new VoxelMeshComponent(packedIndices, vertices, normals, materials, float3.zero, false, false);
                return new VoxelMeshComponent(componentIndex, componentSize, float3.zero, false, false);
            }

            vertMean /= vertMean.w;

            for (int i = componentIndex; i < componentIndex + componentSize; i++)
            {
                PackedIndex packedIndex1 = componentIndices[i];
                int index1 = packedIndex1;
                bool is2DFeature1 = packedIndex1.Is2DSharpFeature;

                if (!is2DFeature1)
                {
                    if (packedIndex1.IsNormalSet)
                    {
                        VoxelMeshComponentVertex meshVertex1 = componentVertices[index1];

                        float3 vertex1 = meshVertex1.Position;

                        var normal1v = meshVertex1.Normal;
                        bool isTransition1 = packedIndex1.IsMaterialTransition;

                        for (int j = componentIndex; j < componentIndex + componentSize; j++)
                        {
                            PackedIndex packedIndex2 = componentIndices[j];
                            int index2 = packedIndex2;
                            bool is2DFeature2 = packedIndex2.Is2DSharpFeature;

                            if (!is2DFeature2 && i != j)
                            {
                                if (packedIndex2.IsNormalSet)
                                {
                                    VoxelMeshComponentVertex meshVertex2 = componentVertices[index2];

                                    float3 vertex2 = meshVertex1.Position;

                                    var normal2v = meshVertex2.Normal;
                                    bool isTransition2 = packedIndex2.IsMaterialTransition;

                                    //This check is slightly relaxed in case there's only 4 transition samples
                                    //because then only vertices with same material are checked against each other.
                                    //If there are more than 4 transition samples then it needs to be more strict such
                                    //that the resulting sharp feature stays stable.
                                    if (isTransition1 && isTransition2 && (meshVertex1.Material == meshVertex2.Material || transitionSamples > 4))
                                    {
                                        float3 diff1 = meshVertex1.Position - vertMean.xyz;
                                        float3 diff2 = meshVertex2.Position - vertMean.xyz;

                                        float3 perp = math.cross(diff1, diff2);

                                        float3 d1 = math.cross(diff1, perp);
                                        float3 d2 = math.cross(diff2, perp);

                                        if ((int)math.sign(math.dot(d1, normal1v)) == (int)math.sign(math.dot(d2, normal2v)))
                                        {
                                            transitionTheta = math.min(transitionTheta, -math.dot(normal1v, normal2v));
                                        }
                                        else
                                        {
                                            transitionTheta = math.min(transitionTheta, math.dot(normal1v, normal2v));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //TODO How to handle these?
            ComponentMaterials componentMaterialsView = new ComponentMaterials();

            bool is3DSharpFeature = math.lengthsq(n0) > 0.01f && math.lengthsq(n1) > 0.01f && Properties.IsSharp3DFeature(theta, componentMaterialsView);
            bool is3DMaterialTransitionFeature = math.lengthsq(n0) > 0.01f && math.lengthsq(n1) > 0.01f && transitionSamples >= 2 && Properties.IsValid3DTransitionFeature(transitionTheta, componentMaterialsView);
            bool is3DSharpCornerFeature = false;

            if (is3DSharpFeature || is3DMaterialTransitionFeature)
            {
                //If there are more than two material transition vertices then
                //the feature induced by the material transitions might be a sharp
                //feature so that needs to be checked similarly to the regular vertices.
                bool isSharpMaterialTransitionFeature = is3DMaterialTransitionFeature && Properties.IsSharp3DFeature(transitionTheta, componentMaterialsView);

                if (isSharpMaterialTransitionFeature)
                {
                    is3DSharpCornerFeature = true;
                }
                else
                {
                    float3 planeNormal = math.cross(n0, n1);

                    float phi = 0.0F;

                    //The material transitions may however also introduce a sharp corner feature
                    //with the planes of the regular vertices so they also need to be checked
                    //like the regular vertices

                    for (int i = componentIndex; i < componentIndex + componentSize; i++)
                    {
                        PackedIndex packedIndex = componentIndices[i];
                        bool isTransition = packedIndex.IsMaterialTransition;
                        bool is2DFeature = packedIndex.Is2DSharpFeature;

                        //Ignore if vertex is a 2D feature or if vertex is a transition feature and there is no material transition feature
                        if (is2DFeature || (isTransition && !is3DMaterialTransitionFeature))
                        {
                            continue;
                        }

                        if (packedIndex.IsNormalSet)
                        {
                            phi = math.max(phi, math.abs(math.dot(componentVertices[packedIndex].Normal, planeNormal)));
                        }
                    }

                    is3DSharpCornerFeature = Properties.IsSharp3DCornerFeature(phi, componentMaterialsView);
                }
            }

            float3 sharpFeature = float3.zero;

            if (is3DSharpFeature || is3DMaterialTransitionFeature)
            {
                bool isOnly3DMaterialTransitionFeature = !is3DSharpFeature && is3DMaterialTransitionFeature;

                float4 lsMean = new float4(0.0f);

                var points = memoryCache.points;
                points.Clear();

                var normals = memoryCache.normals;
                normals.Clear();

                for (int i = 0; i < componentSize; i++)
                {
                    PackedIndex packedIndex = componentIndices[componentIndex + i];
                    bool isTransition = packedIndex.IsMaterialTransition;
                    bool is2DFeature = packedIndex.Is2DSharpFeature;

                    //Ignore if vertex is a 2D feature or if vertex is a transition feature and there is no material transition feature
                    if (is2DFeature || (isTransition && !is3DMaterialTransitionFeature))
                    {
                        continue;
                    }

                    int index = packedIndex;

                    VoxelMeshComponentVertex meshVertex = componentVertices[index];

                    float3 vertex = meshVertex.Position;
                    float3 normal = float3.zero;

                    if (isOnly3DMaterialTransitionFeature && !isTransition)
                    {
                        //When there is no sharp feature but only a material transition feature and this vertex
                        //is not a material transition, then instead of using the vertex' regular normal
                        //the cross product with its neighbor and the mean vertex is used.
                        //Like this the smooth surface the regular vertices would produce is retained much better.

                        float3 vertex2 = componentVertices[componentIndices[componentIndex + (i + 1) % componentSize]].Position;
                        normal = math.normalize(math.cross(vertex - vertMean.xyz, vertex2 - vertMean.xyz));
                    }
                    else if (packedIndex.IsNormalSet)
                    {
                        normal = meshVertex.Normal;
                    }

                    if (math.lengthsq(normal) > 0.01f)
                    {
                        //Centering around only non-transition vertices gives better results
                        if (!isTransition)
                        {
                            lsMean += new float4(vertex, 1.0f);
                        }

                        //Check if normal is finite. Some normals may be infinite or NaN when material transitions
                        //are involved, in which case they need to be ignored in the LS system.
                        if (math.all(math.isfinite(normal)))
                        {
                            normals.Add(normal);
                        }
                        else
                        {
                            normals.Add(float3.zero);
                        }

                        points.Add(meshVertex.Position);
                    }
                }

                lsMean /= lsMean.w;

                sharpFeature = SharpFeatureSolver.Solve(cell, points, normals, !is3DSharpCornerFeature, lsMean.xyz);
            }

            //Replace material transition normals with the proper surface normals
            int transitionIndex = 0;
            for (int i = componentIndex; i < componentIndex + componentSize; i++)
            {
                PackedIndex packedIndex = componentIndices[i];
                if (packedIndex.IsMaterialTransition)
                {
                    VoxelMeshComponentVertex meshVertex = componentVertices[packedIndex];
                    componentVertices[packedIndex] = new VoxelMeshComponentVertex(meshVertex.Position, transitionSurfaceNormals[transitionIndex], meshVertex.Material);
                    transitionIndex++;
                }
            }

            //TODO Fill in missing normals?

            //TODO calculate mean vertex and add it to the first index
            //Or better do this in generateComponents and then in this
            //method just replace the first vertex.

            //return new VoxelMeshComponent(packedIndices, vertices, normals, materials, sharpFeature, is3DSharpFeature, transitionSamples > 0);
            return new VoxelMeshComponent(componentIndex, componentSize, sharpFeature, is3DSharpFeature, is3DMaterialTransitionFeature);
        }

        private void GenerateComponents(TCell cell, NativeList<Segment> segments, NativeList<float3> segmentVertices, NativeList<float3> segmentNormals, NativeList<int> segmentMaterials,
            NativeList<VoxelMeshComponent> components, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices)
        {
            var component = memoryCache.component;
            component.Clear();

            var transitionSurfaceNormals = memoryCache.transitionSurfaceNormals;
            transitionSurfaceNormals.Clear();

            while (segments.Length > 0)
            {
                int numVertices = 0;
                int materialTransitions = 0;

                component.Clear();

                Segment start = segments[segments.Length - 1];
                //segments.Remove(start);
                //TODO Does this work?
                segments.RemoveAtSwapBack(segments.Length - 1);

                component.Add(start);
                numVertices += start.size;
                if (start.materialTransitionIndex >= 0)
                {
                    materialTransitions++;
                }

                float3 currentEndPoint = segmentVertices[start.index + start.size - 1];

                int joinedSegmentIndex;
                do
                {
                    joinedSegmentIndex = -1;

                    for (int count = segments.Length, i = 0; i < count; i++)
                    {
                        var next = segments[i];
                        //TODO Use segment's start and end edges to optimize and make this more robust
                        if (math.lengthsq(segmentVertices[next.index] - currentEndPoint) < 0.00001f)
                        {
                            joinedSegmentIndex = i;
                            component.Add(next);
                            numVertices += next.size;
                            if (next.materialTransitionIndex >= 0)
                            {
                                materialTransitions++;
                            }
                            currentEndPoint = segmentVertices[next.index + next.size - 1];
                            break;
                        }
                    }

                    if (joinedSegmentIndex >= 0)
                    {
                        segments.RemoveAtSwapBack(joinedSegmentIndex);
                    }
                } while (joinedSegmentIndex >= 0);

                int componentIndex = componentIndices.Length;
                int componentSize = numVertices - component.Length + materialTransitions;

                int verticesIndex = componentVertices.Length;

                transitionSurfaceNormals.Clear();

                int vertexIndex = 0;
                for (int j = 0; j < component.Length; j++)
                {
                    Segment segment = component[j];

                    bool isFirstSegment = j == 0;
                    bool isLastSegment = j == component.Length - 1;

                    int startIndex = isFirstSegment ? 0 : 1;
                    int endIndex = isLastSegment ? segment.size - 1 : segment.size;

                    for (int i = startIndex; i < endIndex; i++)
                    {
                        bool isMaterialTransitionVertex = segment.materialTransitionIndex == i;

                        if (isMaterialTransitionVertex)
                        {
                            var normal = segmentNormals[segment.index + i];

                            //Duplicate and insert material transition vertices

                            componentIndices.Add(new PackedIndex(verticesIndex + vertexIndex, true, false, math.lengthsq(normal) > 0.01f));
                            componentVertices.Add(new VoxelMeshComponentVertex(segmentVertices[segment.index + i], normal, segment.materialTransitionMaterial1));
                            transitionSurfaceNormals.Add(segment.materialTransitionSurfaceNormal);
                            vertexIndex++;

                            componentIndices.Add(new PackedIndex(verticesIndex + vertexIndex, true, false, math.lengthsq(normal) > 0.01f));
                            componentVertices.Add(new VoxelMeshComponentVertex(segmentVertices[segment.index + i], normal, segment.materialTransitionMaterial2));
                            transitionSurfaceNormals.Add(segment.materialTransitionSurfaceNormal);
                            vertexIndex++;
                        }
                        else
                        {
                            var normal = segmentNormals[segment.index + i];

                            //Anything that is not a material transition or the start or the end of the segment is
                            //a sharp feature
                            bool is2DFeature = i != 0 && i != segment.size - 1;

                            componentIndices.Add(new PackedIndex(verticesIndex + vertexIndex, false, is2DFeature, math.lengthsq(normal) > 0.01f));
                            componentVertices.Add(new VoxelMeshComponentVertex(segmentVertices[segment.index + i], normal, segmentMaterials[segment.index + i]));
                            vertexIndex++;
                        }
                    }
                }

                //Fill in missing materials at 2D sharp features
                if (componentSize > 0)
                {
                    int currentMaterial = componentVertices[componentIndices[componentIndex]].Material; //The first vertex is always an intersection vertex, so it definitely has a material

                    for (int i = componentIndex + 1; i < componentIndex + componentSize; i++)
                    {
                        PackedIndex index = componentIndices[i];
                        VoxelMeshComponentVertex vertex = componentVertices[index];

                        if (vertex.Material >= 0)
                        {
                            currentMaterial = vertex.Material;
                        }
                        else
                        {
                            componentVertices[index] = new VoxelMeshComponentVertex(vertex.Position, vertex.Normal, currentMaterial);
                        }
                    }
                }

                components.Add(Reconstruct3DSharpFeature(cell, componentIndex, componentSize, componentIndices, componentVertices, transitionSurfaceNormals));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TaggedEdge ComputeTaggedEdge(TCell cell, int edgeNum, int faceCellIndex, int caseIndex, CellInfo info, CellEdges edges, CellMaterials materials)
        {
            var edge = CMSTables.IntersectionEdgeTable[caseIndex * 5 + 1 + edgeNum];
            var edgeId = edges[edge];

            var edgeMaterial = materials[CMSTables.EdgeMaterialTable[caseIndex * 5 + 1 + edgeNum]];

            float2 intersection;
            switch (edge)
            {
                default:
                case 0:
                    intersection = new float2(cell.GetIntersection(faceCellIndex, edgeId), 0);
                    break;
                case 1:
                    intersection = new float2(info.Width, cell.GetIntersection(faceCellIndex, edgeId));
                    break;
                case 2:
                    intersection = new float2(info.Width - cell.GetIntersection(faceCellIndex, edgeId), info.Height);
                    break;
                case 3:
                    intersection = new float2(0, info.Height - cell.GetIntersection(faceCellIndex, edgeId));
                    break;
            }

            return new TaggedEdge(edgeId, edgeMaterial, intersection);
        }

        private void GenerateFaceCellSegments(TCell cell, int faceCellIndex, VoxelCellFace face,
            NativeList<Segment> segments, NativeList<float3> segmentVertices, NativeList<float3> segmentNormals, NativeList<int> segmentMaterials)
        {
            var materials = cell.GetMaterials(faceCellIndex);

            var caseIndex = 0;

            var solidMaterials = 0;

            if (Properties.IsSolid(materials.m1))
            {
                caseIndex |= 0b0001;
                solidMaterials++;
            }
            if (Properties.IsSolid(materials.m2))
            {
                caseIndex |= 0b0010;
                solidMaterials++;
            }
            if (Properties.IsSolid(materials.m3))
            {
                caseIndex |= 0b0100;
                solidMaterials++;
            }
            if (Properties.IsSolid(materials.m4))
            {
                caseIndex |= 0b1000;
                solidMaterials++;
            }

            if (caseIndex != 0 && caseIndex != 0b1111)
            {
                var info = cell.GetInfo(faceCellIndex);

                var edges = cell.GetEdges(faceCellIndex);

                var basisX = face.BasisX();
                var basisY = face.BasisY();

                var materialTransitions = memoryCache.materialTransitions;
                materialTransitions.Clear();

                var cellMaterials = memoryCache.cellMaterials;
                cellMaterials.Clear();

                if ((caseIndex & 0b0001) != 0)
                {
                    cellMaterials.Add(materials.m1);
                }
                if ((caseIndex & 0b0010) != 0)
                {
                    cellMaterials.Add(materials.m2);
                }
                if ((caseIndex & 0b0100) != 0)
                {
                    cellMaterials.Add(materials.m3);
                }
                if ((caseIndex & 0b1000) != 0)
                {
                    cellMaterials.Add(materials.m4);
                }

                var numMaterialTransitionEdgeIndices = CMSTables.MaterialTransitionEdgeTable[caseIndex * 3];
                for (int i = 0; i < numMaterialTransitionEdgeIndices; i++)
                {
                    var edge = CMSTables.MaterialTransitionEdgeTable[caseIndex * 3 + 1 + i];
                    var edgeId = edges[edge];

                    if (cell.HasIntersection(faceCellIndex, edgeId))
                    {
                        float3 transitionNormal = cell.GetNormal(faceCellIndex, edgeId);

                        float ix = 0;
                        float iy = 0;

                        switch (edge)
                        {
                            case 0:
                                ix = cell.GetIntersection(faceCellIndex, edgeId);
                                iy = 0;
                                break;
                            case 1:
                                ix = info.Width;
                                iy = cell.GetIntersection(faceCellIndex, edgeId);
                                break;
                            case 2:
                                ix = info.Width - cell.GetIntersection(faceCellIndex, edgeId);
                                iy = info.Height;
                                break;
                            case 3:
                                ix = 0;
                                iy = info.Height - cell.GetIntersection(faceCellIndex, edgeId);
                                break;
                        }

                        float dx = math.dot(transitionNormal, basisX);
                        float dy = math.dot(transitionNormal, basisY);

                        materialTransitions.Add(new MaterialTransition(ix, iy, dx, dy, transitionNormal,
                            materials[CMSTables.MaterialTransitionEdgeMaterialTable[caseIndex * 4 + i * 2]],
                            materials[CMSTables.MaterialTransitionEdgeMaterialTable[caseIndex * 4 + i * 2 + 1]]));
                    }
                }

                switch (CMSTables.IntersectionEdgeTable[caseIndex * 5])
                {
                    case 2:
                        {
                            //Unambiguous case, can create segment immediately

                            var te0 = ComputeTaggedEdge(cell, 0, faceCellIndex, caseIndex, info, edges, materials);
                            float3 te0n = cell.GetNormal(faceCellIndex, te0.Edge);
                            float2 te0np = math.normalize(new float2(math.dot(te0n, basisX), math.dot(te0n, basisY)));

                            var te1 = ComputeTaggedEdge(cell, 1, faceCellIndex, caseIndex, info, edges, materials);
                            float3 te1n = cell.GetNormal(faceCellIndex, te1.Edge);
                            float2 te1np = math.normalize(new float2(math.dot(te1n, basisX), math.dot(te1n, basisY)));

                            bool isFeatureSharp = Find2DFeature(cell, faceCellIndex, cellMaterials, te0.Intersection, te0np, te1.Intersection, te1np, math.dot(te0n, te1n), info.Width, info.Height, out float2 feature).IsSharp;

                            segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te0, te0n, te1, te1n, isFeatureSharp, feature, materialTransitions, cellMaterials));

                            break;
                        }
                    case 4:
                        {
                            //Need to resolve ambiguity first by checking for overlapping sharp features

                            var te0 = ComputeTaggedEdge(cell, 0, faceCellIndex, caseIndex, info, edges, materials);
                            float3 te0n = cell.GetNormal(faceCellIndex, te0.Edge);
                            float2 te0np = math.normalize(new float2(math.dot(te0n, basisX), math.dot(te0n, basisY)));

                            var te1 = ComputeTaggedEdge(cell, 1, faceCellIndex, caseIndex, info, edges, materials);
                            float3 te1n = cell.GetNormal(faceCellIndex, te1.Edge);
                            float2 te1np = math.normalize(new float2(math.dot(te1n, basisX), math.dot(te1n, basisY)));

                            var te2 = ComputeTaggedEdge(cell, 2, faceCellIndex, caseIndex, info, edges, materials);
                            float3 te2n = cell.GetNormal(faceCellIndex, te2.Edge);
                            float2 te2np = math.normalize(new float2(math.dot(te2n, basisX), math.dot(te2n, basisY)));

                            var te3 = ComputeTaggedEdge(cell, 3, faceCellIndex, caseIndex, info, edges, materials);
                            float3 te3n = cell.GetNormal(faceCellIndex, te3.Edge);
                            float2 te3np = math.normalize(new float2(math.dot(te3n, basisX), math.dot(te3n, basisY)));

                            //Test (sharp) feature of te0+te1 and te2+te3 for overlap
                            FeatureType2D featureType1 = Find2DFeature(cell, faceCellIndex, cellMaterials, te0.Intersection, te0np, te1.Intersection, te1np, math.dot(te0n, te1n), info.Width, info.Height, out float2 feature1);
                            FeatureType2D featureType2 = Find2DFeature(cell, faceCellIndex, cellMaterials, te2.Intersection, te2np, te3.Intersection, te3np, math.dot(te2n, te3n), info.Width, info.Height, out float2 feature2);

                            int case1Violations = 0;
                            case1Violations += !featureType1.IsValid ? 2 : 0;
                            case1Violations += !featureType2.IsValid ? 2 : 0;
                            case1Violations += !featureType1.IsInBounds ? 1 : 0;
                            case1Violations += !featureType2.IsInBounds ? 1 : 0;
                            case1Violations += SharpFeatureSolver2D.CheckFeatureIntersection(te0.Intersection, feature1, featureType1, te1.Intersection, te2.Intersection, feature2, featureType2, te3.Intersection) ? 3 : 0;

                            if (case1Violations == 0)
                            {
                                segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te0, te0n, te1, te1n, featureType1.IsSharp, feature1, materialTransitions, cellMaterials));
                                segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te2, te2n, te3, te3n, featureType2.IsSharp, feature2, materialTransitions, cellMaterials));
                            }
                            else
                            {
                                //Test (sharp) feature of te1+te2 and te0+te3 for overlap
                                FeatureType2D featureType4 = Find2DFeature(cell, faceCellIndex, cellMaterials, te0.Intersection, te0np, te3.Intersection, te3np, math.dot(te0n, te3n), info.Width, info.Height, out float2 feature4);
                                FeatureType2D featureType3 = Find2DFeature(cell, faceCellIndex, cellMaterials, te1.Intersection, te1np, te2.Intersection, te2np, math.dot(te1n, te2n), info.Width, info.Height, out float2 feature3);

                                int case2Violations = 0;
                                case2Violations += !featureType3.IsValid ? 2 : 0;
                                case2Violations += !featureType4.IsValid ? 2 : 0;
                                case2Violations += !featureType3.IsInBounds ? 1 : 0;
                                case2Violations += !featureType4.IsInBounds ? 1 : 0;
                                case2Violations += SharpFeatureSolver2D.CheckFeatureIntersection(te1.Intersection, feature3, featureType3, te2.Intersection, te0.Intersection, feature4, featureType4, te3.Intersection) ? 3 : 0;

                                if (case2Violations == 0)
                                {
                                    segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te2, te2n, te1, te1n, featureType3.IsSharp, feature3, materialTransitions, cellMaterials));
                                    segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te0, te0n, te3, te3n, featureType4.IsSharp, feature4, materialTransitions, cellMaterials));
                                }
                                else
                                {
                                    bool useCase1;

                                    if (case1Violations == case2Violations)
                                    {
                                        //Use case with less acute angles, usually more stable
                                        useCase1 = math.abs(math.dot(te0np, te1np)) + math.abs(math.dot(te2np, te3np)) < math.abs(math.dot(te1np, te2np)) + math.abs(math.dot(te0np, te3np));
                                    }
                                    else if (case1Violations < case2Violations)
                                    {
                                        useCase1 = true;
                                    }
                                    else
                                    {
                                        useCase1 = false;
                                    }

                                    if (useCase1)
                                    {
                                        segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te0, te0n, te1, te1n, featureType1.IsSharp, feature1, materialTransitions, cellMaterials));
                                        segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te2, te2n, te3, te3n, featureType2.IsSharp, feature2, materialTransitions, cellMaterials));
                                    }
                                    else
                                    {
                                        segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te2, te2n, te1, te1n, featureType3.IsSharp, feature3, materialTransitions, cellMaterials));
                                        segments.Add(CreateSegment(segmentVertices, segmentNormals, segmentMaterials, info.Position, basisX, basisY, info.Width, info.Height, te0, te0n, te3, te3n, featureType4.IsSharp, feature4, materialTransitions, cellMaterials));
                                    }
                                }
                            }
                            break;
                        }
                    default:
                        //Uhm...
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FeatureType2D Find2DFeature(
            TCell cell, int cellFace, NativeList<int> cellMaterials,
            float2 pos1, float2 normal1, float2 pos2, float2 normal2, float featureAngle3D,
            float width, float height, out float2 feature)
        {
            SharpFeatureSolver2D.Solve(cell, cellFace, pos1, normal1, pos2, normal2, featureAngle3D, width, height, out feature, out FeatureType2D type);

            if(!Properties.IsSharp2DFeature(normal1.x * normal2.x + normal1.y * normal2.y, featureAngle3D, cellMaterials))
            {
                //Return the intersection so 2D ambiguities can be resolved. The returned
                //intersection is however not treated as sharp feature because the the angle is not
                //sufficiently sharp.
                type = FeatureType2D.NONE;
            }

            return type;
        }

        private Segment CreateSegment(NativeList<float3> vertices, NativeList<float3> normals, NativeList<int> materials,
            float3 pos, float3 basisX, float3 basisY, float cellWidth, float cellHeight,
            TaggedEdge e1, float3 e1n, TaggedEdge e2, float3 e2n,
            bool isSharpFeature, float2 sharpFeature, NativeList<MaterialTransition> materialTransitions, NativeArray<int> cellMaterials)
        {
            int index = 0;

            int numVertices = isSharpFeature ? 4 : 2;

            int usedSegmentVertices = numVertices;
            NativeArray<SegmentVertex> segmentVertices = memoryCache.segmentVertexArray;

            segmentVertices[index] = new SegmentVertex(e1.Intersection, e1n, e1.Material);
            index++;

            if (isSharpFeature)
            {
                if (sharpFeature.x < -cellWidth)
                {
                    sharpFeature.x = 0;
                }
                else if (sharpFeature.x > cellWidth * 2)
                {
                    sharpFeature.x = cellWidth;
                }
                if (sharpFeature.y < -cellHeight)
                {
                    sharpFeature.y = 0;
                }
                else if (sharpFeature.y > cellHeight * 2)
                {
                    sharpFeature.y = cellHeight;
                }

                segmentVertices[index] = new SegmentVertex(sharpFeature, e1n, e1.Material);
                index++;

                segmentVertices[index] = new SegmentVertex(sharpFeature, e2n, e2.Material);
                index++;
            }

            segmentVertices[index] = new SegmentVertex(e2.Intersection, e2n, e2.Material);

            int materialTransitionInsertionIndex = -1;
            float3 materialTransitionVertex = float3.zero;
            float3 materialTransitionSurfaceNormal = float3.zero;
            float3 materialTransitionNormal = float3.zero;
            int materialTransitionMaterial1 = -1, materialTransitionMaterial2 = -1;

            float materialTransitionMinError = float.MaxValue;
            bool isValidTransition = false;

            if (materialTransitions.Length > 0 && !isSharpFeature)
            {
                for (int i = 0; i < usedSegmentVertices - 1 && materialTransitionInsertionIndex == -1 /*Only allow one transition, see TODO further below*/; i++)
                {
                    SegmentVertex v1 = segmentVertices[i];
                    SegmentVertex v2 = segmentVertices[i + 1];

                    float2 p1 = v1.Pos;
                    float2 p2 = v2.Pos;

                    float3 n1 = v1.Normal;
                    float3 n2 = v2.Normal;

                    float2 sd = p2 - p1;
                    float segmentLength = math.length(sd);
                    sd /= segmentLength;

                    //TODO Move material transition creation to 2D sharp feature solver

                    for (int j = materialTransitions.Length - 1; j >= 0; j--)
                    {
                        MaterialTransition transition = materialTransitions[j];

                        float theta1 = math.abs(math.dot(transition.normal, n1));
                        float theta2 = math.abs(math.dot(transition.normal, n2));

                        if (Properties.IsValid2DTransitionFeature(math.min(theta1, theta2), math.max(theta1, theta2), cellMaterials) &&
                            IntersectionSharpFeatureSolver<TCell>.FindLineIntersection(transition.x, transition.y, transition.x + transition.dy, transition.y - transition.dx, p1.x, p1.y, p2.x, p2.y, out float2 intersection))
                        {
                            float projection = sd.x * (intersection.x - p1.x) + sd.y * (intersection.y - p1.y);

                            //Check if intersection lies on line segment
                            isValidTransition = projection >= 0 && projection < segmentLength;

                            //Find the projection error, i.e. how far away the projection is from the segment
                            float projectionError = projection < 0 ? -projection : projection > segmentLength ? projection - segmentLength : 0;

                            //Check if intersection lies on line segment
                            if (isValidTransition || projectionError < materialTransitionMinError)
                            {
                                //TODO Currently needs to be checked since only one transition is allowed.
                                //If multiple transitions were allowed this would need to be checked later
                                //such that the multiple transitions start and end always match up with the current
                                //and next segment's materials
                                if (isValidTransition && (transition.material1 != v1.Material || transition.material2 != v2.Material))
                                {
                                    continue;
                                }

                                materialTransitionMinError = projectionError;

                                materialTransitionInsertionIndex = i;
                                if (isValidTransition)
                                {
                                    materialTransitionVertex = CellSpaceToWorldSpace(pos, basisX, basisY, intersection.x, intersection.y);
                                }
                                else
                                {
                                    materialTransitionVertex = CellSpaceToWorldSpace(pos, basisX, basisY, projection <= 0 ? (p1.x + sd.x * segmentLength * 0.0001f) : (p2.x - sd.x * segmentLength * 0.0001f), projection <= 0 ? (p1.y + sd.y * segmentLength * 0.0001f) : (p2.y - sd.y * segmentLength * 0.0001f));
                                }
                                materialTransitionNormal = math.normalize(math.cross(n1, math.cross(transition.normal, n1)));
                                if (isValidTransition)
                                {
                                    materialTransitionMaterial1 = transition.material1;
                                    materialTransitionMaterial2 = transition.material2;
                                }
                                else
                                {
                                    materialTransitionMaterial1 = v1.Material;
                                    materialTransitionMaterial2 = v2.Material;
                                }

                                if (isValidTransition)
                                {
                                    //Fix neighboring vertex materials to match (i.e. sharp features' materials)
                                    segmentVertices[i] = new SegmentVertex(p1, n1, transition.material1);
                                    segmentVertices[i + 1] = new SegmentVertex(p2, n2, transition.material2);

                                    //TODO Currently only allow one material transition.
                                    //Would multiple transitions per segment line work?
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            //Check if there is a trivial material transition, i.e. only a material change but no sharp feature or valid material transition
            bool hasTrivialMaterialTransition = !isSharpFeature && materialTransitionInsertionIndex == -1 && e1.Material != e2.Material;

            if (materialTransitionInsertionIndex >= 0 || hasTrivialMaterialTransition)
            {
                numVertices++;
            }

            int segmentIndex = vertices.Length;
            int segmentSize = numVertices;

            //Insert trivial material transition
            if (hasTrivialMaterialTransition)
            {
                float2 transition = (segmentVertices[0].Pos + segmentVertices[1].Pos) / 2.0f;

                materialTransitionInsertionIndex = 0;
                materialTransitionVertex = CellSpaceToWorldSpace(pos, basisX, basisY, transition.x, transition.y);
                materialTransitionNormal = float3.zero;
                materialTransitionMaterial1 = e1.Material;
                materialTransitionMaterial2 = e2.Material;
            }

            int materialTransitionIndex = -1;
            int insertionIndex = 0;
            for (int i = 0; i < usedSegmentVertices; i++)
            {
                SegmentVertex vertex = segmentVertices[i];

                vertices.Add(CellSpaceToWorldSpace(pos, basisX, basisY, vertex.Pos.x, vertex.Pos.y));
                normals.Add(vertex.Normal);
                materials.Add(vertex.Material);
                insertionIndex++;

                //Insert material transition
                if (materialTransitionInsertionIndex == i)
                {
                    vertices.Add(materialTransitionVertex);
                    normals.Add(materialTransitionNormal);
                    materials.Add(-1);
                    materialTransitionIndex = insertionIndex;
                    insertionIndex++;
                }
            }

            //Set surface normal of material transition to average of neighboring vertices.
            //Material transitions should always be smooth. Sharp features are already handled
            //by different vertices.
            if (materialTransitionIndex != -1)
            {
                materialTransitionSurfaceNormal = (normals[materialTransitionIndex - 1] + normals[materialTransitionIndex + 1]) * 0.5f;
            }

            return new Segment(segmentIndex, segmentSize, materialTransitionIndex, materialTransitionSurfaceNormal,
                materialTransitionMaterial1, materialTransitionMaterial2, e1.Edge, e2.Edge);
        }
    }
}
