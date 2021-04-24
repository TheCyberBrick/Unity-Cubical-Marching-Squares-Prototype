using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Voxel
{
    [RequireComponent(typeof(CustomBrushContainer))]
    public class CustomBrushSdfRenderer : DynamicSdfShapeRenderer
    {
        [SerializeField] private VoxelWorldContainer parentWorld;
        [SerializeField] private SdfShapeRenderHandler sdfRenderer;

        [SerializeField] private bool renderSurface = true;
        public bool RenderSurface
        {
            get
            {
                return renderSurface;
            }
            set
            {
                renderSurface = value;
            }
        }

        [SerializeField] private bool renderPrimitives = true;
        public bool RenderPrimitives
        {
            get
            {
                return renderPrimitives;
            }
            set
            {
                renderPrimitives = value;
            }
        }

        [SerializeField] private Material surfaceMaterial;
        [SerializeField] private Material primitiveUnionMaterial;
        [SerializeField] private Material primitiveDifferenceMaterial;

        private CustomBrushContainer brush;

        private VoxelWorld<MortonIndexer> world;

        public bool NeedsRebuild
        {
            get;
            set;
        } = true;

        public void Start()
        {
            brush = GetComponent<CustomBrushContainer>();
            world = new VoxelWorld<MortonIndexer>(parentWorld.ChunkSize, parentWorld.CMSProperties, transform, parentWorld.IndexerFactory);
        }

        void Update()
        {
            if (NeedsRebuild)
            {
                NeedsRebuild = false;
                world.Clear();
                using (var sdf = brush.Instance.CreateSdf(Allocator.TempJob))
                {
                    world.ApplySdf(new Vector3(0, 0, 0), Quaternion.identity, sdf, 1, false, null);
                }
            }

            world.Update();
        }

        public override void Render(Matrix4x4 transform, Material material = null)
        {
            Material surfaceMaterial;
            Material primitiveUnionMaterial;
            Material primitiveDifferenceMaterial;

            if (material != null)
            {
                surfaceMaterial = primitiveUnionMaterial = primitiveDifferenceMaterial = material;
            }
            else
            {
                surfaceMaterial = this.surfaceMaterial;
                primitiveUnionMaterial = this.primitiveUnionMaterial;
                primitiveDifferenceMaterial = this.primitiveDifferenceMaterial;
            }

            if (RenderSurface)
            {
                world.Render(transform, surfaceMaterial);
            }

            if (RenderPrimitives && sdfRenderer != null)
            {
                foreach (var primitive in brush.Instance.Primitives)
                {
                    ISdf renderSdf = brush.Instance.Evaluator.GetRenderSdf(primitive);
                    if (renderSdf != null && !brush.Instance.GetSdfType().IsAssignableFrom(renderSdf.GetType()))
                    {
                        sdfRenderer.Render(transform * (Matrix4x4)primitive.transform, renderSdf, primitive.operation == BrushOperation.Union ? primitiveUnionMaterial : primitiveDifferenceMaterial);
                    }
                }
            }
        }

        public override Type SdfType()
        {
            return brush.Instance.GetSdfType();
        }

        public void OnApplicationQuit()
        {
            world.Dispose();
        }
    }
}
