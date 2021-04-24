using Voxel;
using Voxel.Voxelizer;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VoxelPolygonizer;
using VoxelPolygonizer.CMS;

public struct MaterialColors : VoxelMeshTessellation.IMaterialColorMap
{
    public Color32 GetColor(int material)
    {
        return FromInteger(material);
    }

    public static Color32 FromInteger(int i)
    {
        int alpha = (i >> 24) & 0xFF;
        int red = (i >> 16) & 0xFF;
        int green = (i >> 8) & 0xFF;
        int blue = (i) & 0xFF;
        return new Color32((byte)red, (byte)green, (byte)blue, (byte)alpha);
    }

    public static int ToInteger(int red, int green, int blue, int alpha)
    {
        int code = 0;
        code |= (alpha & 0xFF) << 24;
        code |= (red & 0xFF) << 16;
        code |= (green & 0xFF) << 8;
        code |= (blue & 0xFF);
        return code;
    }
}

[BurstCompile]
public struct PolygonizeJob : IJob
{
    public NativeMemoryCache MemoryCache;

    [ReadOnly] public NativeArray<float3> Cells;

    [ReadOnly] public NativeArray<int> Materials;

    [ReadOnly] public NativeArray<float> Intersections;

    [ReadOnly] public NativeArray<float3> Normals;

    public NativeList<VoxelMeshComponent> Components;

    public NativeList<PackedIndex> Indices;

    public NativeList<VoxelMeshComponentVertex> Vertices;

    public VoxelMeshTessellation.NativeDeduplicationCache DedupeCache;
    public NativeList<float3> MeshVertices;
    public NativeList<float3> MeshNormals;
    public NativeList<int> MeshTriangles;
    public NativeList<Color32> MeshColors;
    public NativeList<int> MeshMaterials;

    public void Execute()
    {
        var solver = new SvdQefSolver<RawArrayVoxelCell>();
        solver.Clamp = false;
        var polygonizer = new CMSVoxelPolygonizer<RawArrayVoxelCell, CMSStandardProperties, SvdQefSolver<RawArrayVoxelCell>, IntersectionSharpFeatureSolver<RawArrayVoxelCell>>(new CMSStandardProperties(), solver, new IntersectionSharpFeatureSolver<RawArrayVoxelCell>(), MemoryCache);

        for (int i = 0; i < Cells.Length; i++)
        {
            RawArrayVoxelCell cell = new RawArrayVoxelCell(i, Cells[i], Materials, Intersections, Normals);

            polygonizer.Polygonize(cell, Components, Indices, Vertices);
        }

        VoxelMeshTessellation.Tessellate(Components, Indices, Vertices, Matrix4x4.identity, MeshVertices, MeshTriangles, MeshNormals, MeshMaterials, new MaterialColors(), MeshColors, DedupeCache);
    }
}

public readonly struct JobCell
{
    public readonly RawArrayVoxelCell cell;
    public readonly Vector3 pos;

    public JobCell(RawArrayVoxelCell cell, Vector3 pos)
    {
        this.cell = cell;
        this.pos = pos;
    }
}


[RequireComponent(typeof(MeshFilter))]
public partial class CreateVoxelTerrain : MonoBehaviour
{
    [SerializeField] private MeshCollider meshCollider = null;

    private Mesh voxelMesh = null;

    [SerializeField] private float scale = 1.0f;

    [SerializeField] private bool regenerate = false;

    [SerializeField] private bool placeSdf = false;

    [SerializeField] private Vector3 sdfRotation = Vector3.zero;
    [SerializeField] private BrushType brushType = BrushType.Sphere;

    [SerializeField] private bool replaceSdfMaterial = false;

    [SerializeField] [Range(0, 255)] private byte materialRed = 255;
    [SerializeField] [Range(0, 255)] private byte materialGreen = 255;
    [SerializeField] [Range(0, 255)] private byte materialBlue = 255;
    [SerializeField] [Range(0, 255)] private byte materialTexture = 0;

    [SerializeField] private bool generateEachFrame = false;
    private int run = 0;

    [SerializeField] private bool lockSelection = false;
    [SerializeField] Vector3Int lockedSelection = Vector3Int.zero;

    [SerializeField] private bool renderLockedSelectionOnly = false;

    [SerializeField] [Range(0.0f, 30.0f)] private float fixedTime = 0.0f;

    [SerializeField] private MeshFilter voxelizeMesh = null;

    [SerializeField] private CustomBrushContainer customBrush = null;

    [SerializeField] private bool smoothVoxelizerNormals = true;

    private Vector3 gizmoPosition = Vector3.zero;
    private Matrix4x4 gizmoTransform = Matrix4x4.identity;
    private RawArrayVoxelCell gizmoCell = new RawArrayVoxelCell();
    private List<VoxelMeshComponent> gizmoComponents = null;
    private List<PackedIndex> gizmoComponentIndices = null;
    private List<VoxelMeshComponentVertex> gizmoComponentVertices = null;

    private NativeArray<int> gizmoCellMaterials;
    private NativeArray<float3> gizmoCellNormals;
    private NativeArray<float> gizmoCellIntersections;

    private Vector3Int? prevSelectedCell = null;
    private Vector3Int? selectedCell = null;

    private int selectedPrimitive = -1;

    const int fieldSize = 16;

    private class PlacedSdf
    {
        internal readonly ISdf sdf;
        internal readonly int material;
        internal readonly bool replace;

        internal PlacedSdf(ISdf sdf, int material, bool replace)
        {
            this.sdf = sdf;
            this.material = material;
            this.replace = replace;
        }
    }

    private List<PlacedSdf> placedSdfs = new List<PlacedSdf>();

    [SerializeField] private bool undo = false;
    [SerializeField] private bool redo = false;


    private void GenerateMesh()
    {
        int size = (int)Mathf.Ceil(fieldSize * scale);

        /*field = new TestVoxelField(size, size, size);
        GenerateScene(field);*/

        var dedupedTable = new Dictionary<int, List<VoxelMeshTessellation.DedupedVertex>>();

        var components = new NativeList<VoxelMeshComponent>(Allocator.Persistent);
        var componentIndices = new NativeList<PackedIndex>(Allocator.Persistent);
        var componentVertices = new NativeList<VoxelMeshComponentVertex>(Allocator.Persistent);

        int voxels = /*1;//*/ (size - 1) * (size - 1) * (size - 1);

        var cellMaterials = new NativeArray<int>(voxels * 8, Allocator.Persistent);
        var cellIntersections = new NativeArray<float>(voxels * 12, Allocator.Persistent);
        var cellNormals = new NativeArray<float3>(voxels * 12, Allocator.Persistent);

        var cells = new NativeArray<float3>(voxels, Allocator.Persistent);

        int voxelIndex = 0;
        for (int z = 0; z < size - 1; z++)
        {
            for (int y = 0; y < size - 1; y++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    if (renderLockedSelectionOnly && !(x == lockedSelection.x && y == lockedSelection.y && z == lockedSelection.z))
                    {
                        continue;
                    }

                    //field.FillCell(x, y, z, voxelIndex, cellMaterials, cellIntersections, cellNormals);

                    cells[voxelIndex] = new float3(x, y, z);

                    voxelIndex++;
                }
            }
        }

        NativeMemoryCache memoryCache = new NativeMemoryCache(Allocator.Persistent);

        VoxelMeshTessellation.NativeDeduplicationCache dedupeCache = new VoxelMeshTessellation.NativeDeduplicationCache(Allocator.Persistent);

        var meshVertices = new NativeList<float3>(Allocator.Persistent);
        var meshNormals = new NativeList<float3>(Allocator.Persistent);
        var meshTriangles = new NativeList<int>(Allocator.Persistent);
        var meshColors = new NativeList<Color32>(Allocator.Persistent);
        var meshMaterials = new NativeList<int>(Allocator.Persistent);

        var polygonizerJob = new PolygonizeJob
        {
            Cells = cells,
            MemoryCache = memoryCache,
            Materials = cellMaterials,
            Intersections = cellIntersections,
            Normals = cellNormals,
            Components = components,
            Indices = componentIndices,
            Vertices = componentVertices,
            MeshVertices = meshVertices,
            MeshNormals = meshNormals,
            MeshTriangles = meshTriangles,
            MeshColors = meshColors,
            MeshMaterials = meshMaterials,
            DedupeCache = dedupeCache
        };

        var watch = System.Diagnostics.Stopwatch.StartNew();

        polygonizerJob.Schedule().Complete();

        watch.Stop();

        string text = "Polygonized voxel field in " + watch.ElapsedMilliseconds + "ms. Vertices: " + meshVertices.Length + ". Run: " + run;
        Debug.Log(text);

        var cam = FindObjectOfType<Camera>();
        if (cam != null)
        {
            var display = cam.GetComponent<FPSDisplay>();
            if (display != null)
            {
                display.SetInfo(text);
            }
        }

        var vertices = new List<Vector3>(meshVertices.Length);
        var indices = new List<int>(meshTriangles.Length);
        var materials = new List<int>(meshMaterials.Length);
        var colors = new List<Color32>(meshColors.Length);
        var normals = new List<Vector3>(meshNormals.Length);

        for (int i = 0; i < meshVertices.Length; i++)
        {
            vertices.Add(meshVertices[i]);
        }
        for (int i = 0; i < meshTriangles.Length; i++)
        {
            indices.Add(meshTriangles[i]);
        }
        for (int i = 0; i < meshMaterials.Length; i++)
        {
            materials.Add(meshMaterials[i]);
        }
        for (int i = 0; i < meshColors.Length; i++)
        {
            colors.Add(meshColors[i]);
        }
        for (int i = 0; i < meshNormals.Length; i++)
        {
            normals.Add(meshNormals[i]);
        }

        dedupeCache.Dispose();

        meshVertices.Dispose();
        meshNormals.Dispose();
        meshTriangles.Dispose();
        meshColors.Dispose();
        meshMaterials.Dispose();

        memoryCache.Dispose();

        cells.Dispose();

        cellMaterials.Dispose();
        cellIntersections.Dispose();
        cellNormals.Dispose();

        components.Dispose();
        componentIndices.Dispose();
        componentVertices.Dispose();


        run++;

        voxelMesh.Clear(false);
        voxelMesh.SetVertices(vertices);
        voxelMesh.SetNormals(normals);
        voxelMesh.SetTriangles(indices, 0);
        if (colors.Count > 0)
        {
            voxelMesh.SetColors(colors);
        }

        if (meshCollider != null)
        {
            meshCollider.sharedMesh = voxelMesh;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        gizmoCellMaterials = new NativeArray<int>(8, Allocator.Persistent);
        gizmoCellNormals = new NativeArray<float3>(12, Allocator.Persistent);
        gizmoCellIntersections = new NativeArray<float>(12, Allocator.Persistent);

        voxelMesh = new Mesh();
        GetComponent<MeshFilter>().sharedMesh = voxelMesh;

        MortonIndexer indexer = new MortonIndexer(32, 32, 32);
        for (int x = 0; x < 32; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                for (int z = 0; z < 32; z++)
                {
                    int ind = indexer.ToIndex(x, y, z);
                    if (ind >= 32 * 32 * 32)
                    {
                        Debug.Log(ind);
                    }
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        gizmoCellMaterials.Dispose();
        gizmoCellNormals.Dispose();
        gizmoCellIntersections.Dispose();
    }

    private void Update()
    {
        if (undo)
        {
            undo = false;

            var editManager = GetComponent<VoxelEditManagerContainer>().Instance;
            if (editManager != null)
            {
                editManager.Undo();
            }
        }

        if (redo)
        {
            redo = false;

            var editManager = GetComponent<VoxelEditManagerContainer>().Instance;
            if (editManager != null)
            {
                editManager.Redo();
            }
        }

        if (!lockSelection)
        {
            Camera camera = Camera.current;
            if (camera != null)
            {
                Vector3 relPos = camera.transform.position - transform.position;
                Vector3 relDir = Quaternion.Inverse(transform.rotation) * camera.transform.forward.normalized;

                //if (field.RayCast(relPos, relDir, 16, out TestVoxelField.RayCastResult result))
                if (gameObject.GetComponent<VoxelWorldContainer>().Instance.RayCast(relPos, relDir, 64, out VoxelWorld<MortonIndexer>.RayCastResult result))
                {
                    selectedCell = new Vector3Int(Mathf.FloorToInt(result.pos.x), Mathf.FloorToInt(result.pos.y), Mathf.FloorToInt(result.pos.z));
                }
                else
                {
                    selectedCell = null;
                }

                if (selectedCell != null)
                {
                    lockedSelection = selectedCell.Value;
                }
                else
                {
                    lockedSelection = Vector3Int.zero;
                }
            }
            else
            {
                selectedCell = null;
            }
        }
        else
        {
            selectedCell = lockedSelection;
        }

        if (selectedCell != null && prevSelectedCell != selectedCell)
        {
            //field.FillCell(selectedCell.Value.x, selectedCell.Value.y, selectedCell.Value.z, 0, gizmoCellMaterials, gizmoCellIntersections, gizmoCellNormals);
            var sculpture = gameObject.GetComponent<VoxelWorldContainer>().Instance;
            VoxelChunk<MortonIndexer> chunk = sculpture.GetChunk(ChunkPos.FromVoxel(selectedCell.Value, sculpture.ChunkSize));
            if (chunk != null)
            {
                chunk.FillCell(
                    ((selectedCell.Value.x % sculpture.ChunkSize) + sculpture.ChunkSize) % sculpture.ChunkSize,
                    ((selectedCell.Value.y % sculpture.ChunkSize) + sculpture.ChunkSize) % sculpture.ChunkSize,
                    ((selectedCell.Value.z % sculpture.ChunkSize) + sculpture.ChunkSize) % sculpture.ChunkSize,
                    0, gizmoCellMaterials, gizmoCellIntersections, gizmoCellNormals);

                gizmoCell = new RawArrayVoxelCell(0, (Vector3)selectedCell.Value, gizmoCellMaterials, gizmoCellIntersections, gizmoCellNormals);

                NativeMemoryCache memoryCache = new NativeMemoryCache(Allocator.Persistent);

                var polygonizer = new CMSVoxelPolygonizer<RawArrayVoxelCell, CMSStandardProperties, SvdQefSolver<RawArrayVoxelCell>, IntersectionSharpFeatureSolver<RawArrayVoxelCell>>(new CMSStandardProperties(), new SvdQefSolver<RawArrayVoxelCell>(), new IntersectionSharpFeatureSolver<RawArrayVoxelCell>(), memoryCache);

                var components = new NativeList<VoxelMeshComponent>(Allocator.Persistent);
                var componentIndices = new NativeList<PackedIndex>(Allocator.Persistent);
                var componentVertices = new NativeList<VoxelMeshComponentVertex>(Allocator.Persistent);

                polygonizer.Polygonize(gizmoCell, components, componentIndices, componentVertices);

                gizmoComponents = new List<VoxelMeshComponent>(components.Length);
                for (int i = 0; i < components.Length; i++)
                {
                    gizmoComponents.Add(components[i]);
                }

                gizmoComponentIndices = new List<PackedIndex>(componentIndices.Length);
                for (int i = 0; i < componentIndices.Length; i++)
                {
                    gizmoComponentIndices.Add(componentIndices[i]);
                }

                gizmoComponentVertices = new List<VoxelMeshComponentVertex>(componentVertices.Length);
                for (int i = 0; i < componentVertices.Length; i++)
                {
                    gizmoComponentVertices.Add(componentVertices[i]);
                }

                memoryCache.Dispose();
                components.Dispose();
                componentIndices.Dispose();
                componentVertices.Dispose();

                gizmoPosition = selectedCell.Value;
                gizmoTransform = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
            }
        }

        prevSelectedCell = selectedCell;

        float brushSize = 2.1f + 8;

        if (placeSdf)
        {
            placeSdf = false;
            regenerate = true;

            var editManager = GetComponent<VoxelEditManagerContainer>().Instance;

            switch (brushType)
            {
                case BrushType.Sphere:
                    gameObject.GetComponent<VoxelWorldContainer>().Instance.ApplySdf(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new SphereSDF(brushSize), MaterialColors.ToInteger(materialRed, materialGreen, materialBlue, materialTexture), replaceSdfMaterial, editManager.Consumer());
                    break;
                case BrushType.Box:
                    gameObject.GetComponent<VoxelWorldContainer>().Instance.ApplySdf(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new BoxSDF(brushSize), MaterialColors.ToInteger(materialRed, materialGreen, materialBlue, materialTexture), replaceSdfMaterial, editManager.Consumer());
                    break;
                case BrushType.Cylinder:
                    gameObject.GetComponent<VoxelWorldContainer>().Instance.ApplySdf(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new CylinderSDF(brushSize, brushSize), MaterialColors.ToInteger(materialRed, materialGreen, materialBlue, materialTexture), replaceSdfMaterial, editManager.Consumer());
                    break;
                case BrushType.Pyramid:
                    gameObject.GetComponent<VoxelWorldContainer>().Instance.ApplySdf(new Vector3(gizmoPosition.x, gizmoPosition.y - brushSize / 2, gizmoPosition.z), Quaternion.Euler(sdfRotation), new PyramidSDF(brushSize * 2, brushSize * 2), MaterialColors.ToInteger(materialRed, materialGreen, materialBlue, materialTexture), replaceSdfMaterial, editManager.Consumer());
                    break;
                case BrushType.Mesh:
                    var mesh = voxelizeMesh.mesh;
                    var triangles = mesh.triangles;
                    var vertices = mesh.vertices;
                    var normals = mesh.normals;

                    var inVertices = new NativeArray<float3>(triangles.Length, Allocator.TempJob);
                    var inNormals = new NativeArray<float3>(triangles.Length, Allocator.TempJob);

                    for (int l = triangles.Length, i = 0; i < l; i += 3)
                    {
                        inVertices[i] = vertices[triangles[i]];
                        inVertices[i + 1] = vertices[triangles[i + 1]];
                        inVertices[i + 2] = vertices[triangles[i + 2]];

                        inNormals[i] = normals[triangles[i]];
                        inNormals[i + 1] = normals[triangles[i + 1]];
                        inNormals[i + 2] = normals[triangles[i + 2]];
                    }

                    int voxelizerSize = 64;
                    var outVoxels = new NativeArray3D<Voxel.Voxel, MortonIndexer>(new MortonIndexer(voxelizerSize, voxelizerSize, voxelizerSize), voxelizerSize, voxelizerSize, voxelizerSize, Allocator.TempJob);

                    var voxelizationProperties = smoothVoxelizerNormals ? Voxelizer.VoxelizationProperties.SMOOTH : Voxelizer.VoxelizationProperties.FLAT;

                    var watch = new System.Diagnostics.Stopwatch();
                    watch.Start();

                    using (var job = Voxelizer.Voxelize(inVertices, inNormals, outVoxels, MaterialColors.ToInteger(materialRed, materialGreen, materialBlue, materialTexture), voxelizationProperties))
                    {
                        job.Handle.Complete();
                    }

                    watch.Stop();
                    Debug.Log("Voxelized mesh: " + watch.ElapsedMilliseconds + "ms");
                    watch.Reset();
                    watch.Start();

                    //TODO Make voxelizer also undoable?
                    gameObject.GetComponent<VoxelWorldContainer>().Instance.ApplyGrid((int)gizmoPosition.x, (int)gizmoPosition.y, (int)gizmoPosition.z, outVoxels, true, false, null);

                    watch.Stop();
                    Debug.Log("Applied to grid: " + watch.ElapsedMilliseconds + "ms");

                    inVertices.Dispose();
                    inNormals.Dispose();
                    outVoxels.Dispose();

                    break;
                case BrushType.Custom:
                    using (var sdf = customBrush.Instance.CreateSdf(Allocator.TempJob))
                    {
                        gameObject.GetComponent<VoxelWorldContainer>().Instance.ApplySdf(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), sdf, MaterialColors.ToInteger(materialRed, materialGreen, materialBlue, materialTexture), replaceSdfMaterial, editManager.Consumer());
                    }
                    break;
            }
        }

        if (gizmoPosition != null)
        {
            switch (brushType)
            {
                case BrushType.Sphere:
                    gameObject.GetComponent<SdfShapeRenderHandler>().Render(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new SphereSDF(brushSize));
                    break;
                case BrushType.Box:
                    gameObject.GetComponent<SdfShapeRenderHandler>().Render(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new BoxSDF(brushSize));
                    break;
                case BrushType.Cylinder:
                    gameObject.GetComponent<SdfShapeRenderHandler>().Render(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new CylinderSDF(brushSize, brushSize));
                    break;
                case BrushType.Pyramid:
                    gameObject.GetComponent<SdfShapeRenderHandler>().Render(new Vector3(gizmoPosition.x, gizmoPosition.y - brushSize / 2, gizmoPosition.z), Quaternion.Euler(sdfRotation), new PyramidSDF(brushSize * 2, brushSize * 2));
                    break;
                case BrushType.Custom:
                    Matrix4x4 brushTransform = Matrix4x4.TRS(new Vector3(gizmoPosition.x, gizmoPosition.y, gizmoPosition.z), Quaternion.Euler(sdfRotation), new Vector3(1, 1, 1));
                    using (var sdf = customBrush.Instance.CreateSdf(Allocator.TempJob))
                    {
                        gameObject.GetComponent<SdfShapeRenderHandler>().Render(brushTransform, sdf);
                    }

                    Camera camera = Camera.current;
                    if (camera != null)
                    {
                        selectedPrimitive = -1;

                        var ray = camera.transform.forward.normalized;
                        float maxDst = 60.0f;
                        int steps = Mathf.CeilToInt(maxDst * 2);
                        for (int i = 0; i < steps && selectedPrimitive < 0; i++)
                        {
                            var pos = camera.transform.position + ray * maxDst / steps * i;

                            int j = 0;
                            foreach (var primitive in customBrush.Instance.Primitives)
                            {
                                var renderSdf = customBrush.Instance.Evaluator.GetRenderSdf(primitive);
                                if (renderSdf != null && renderSdf.Eval(math.mul(math.mul(brushTransform, primitive.invTransform), new float4(pos, 1.0f)).xyz) < 0)
                                {
                                    selectedPrimitive = j;
                                    break;
                                }
                                j++;
                            }
                        }
                    }
                    if (selectedPrimitive >= 0 && selectedPrimitive < customBrush.Instance.Primitives.Count)
                    {
                        var primitive = customBrush.Instance.Primitives[selectedPrimitive];
                        var renderSdf = customBrush.Instance.Evaluator.GetRenderSdf(primitive);
                        if (renderSdf != null)
                        {
                            gameObject.GetComponent<SdfShapeRenderHandler>().Render(brushTransform * (Matrix4x4)primitive.transform, renderSdf);
                        }
                    }
                    break;
            }
        }

        if (generateEachFrame || regenerate)
        {
            regenerate = false;
            //GenerateMesh();
        }
    }

    private void OnDrawGizmos()
    {
        if (gizmoComponents != null && gizmoComponentIndices != null && gizmoComponentVertices != null)
        {
            VoxelMeshTessellation.DrawDebugGizmos(gizmoPosition, gizmoTransform, gizmoComponents, gizmoComponentIndices, gizmoComponentVertices, ref gizmoCell, new MaterialColors());
        }
    }
}
