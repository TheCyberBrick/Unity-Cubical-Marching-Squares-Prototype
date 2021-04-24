using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Enables the rendering of SDF brushes.
    /// Renderable SDF brushes need to be registered to <see cref="staticRenderers"/> or <see cref="dynamicRenderers"/>.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class SdfShapeRenderHandler : MonoBehaviour
    {
        /// <summary>
        /// Renderer of an <see cref="ISdf"/> type
        /// </summary>
        public interface ISdfRenderer
        {
            /// <summary>
            /// The <see cref="ISdf"/> type that this renderer renders
            /// </summary>
            /// <returns></returns>
            Type SdfType();

            /// <summary>
            /// Renders the SDF with the given transform and material
            /// </summary>
            /// <param name="transform">Transform to be applied before rendering</param>
            /// <param name="material">Material to render the SDF with. If null the default material is chosen by this renderer</param>
            void Render(Matrix4x4 transform, Material material = null);
        }

        private Dictionary<Type, ISdfRenderer> registry;

        [SerializeField]
        private StaticSdfShapeRenderer[] staticRenderers = new StaticSdfShapeRenderer[0];

        [SerializeField]
        private DynamicSdfShapeRenderer[] dynamicRenderers = new DynamicSdfShapeRenderer[0];

        [SerializeField]
        private Material material;

        private Dictionary<Type, ISdfRenderer> RegisterRenderers()
        {
            var registry = new Dictionary<Type, ISdfRenderer>();
            foreach (ISdfRenderer renderer in staticRenderers)
            {
                registry[renderer.SdfType()] = renderer;
            }
            foreach (ISdfRenderer renderer in dynamicRenderers)
            {
                registry[renderer.SdfType()] = renderer;
            }
            return registry;
        }

        /// <summary>
        /// Renders the SDF with the given position and rotation
        /// </summary>
        /// <param name="position">Position where to render the SDF</param>
        /// <param name="rotation">Rotation to be applied before rendering</param>
        /// <param name="sdf">SDF to render</param>
        /// <param name="material">Material to render the SDF with. If null the material is chosen by the SDF renderer itself</param>
        public void Render(Vector3 position, Quaternion rotation, ISdf sdf, Material material = null)
        {
            var transform = Matrix4x4.TRS(position, rotation, Vector3.one);
            Render(transform, sdf, material);
        }

        /// <summary>
        /// Renders the SDF with the given transform
        /// </summary>
        /// <param name="transform">Transform to be applied before rendering</param>
        /// <param name="sdf">SDF to render</param>
        /// <param name="material">Material to render the SDF with. If null the material is chosen by the SDF renderer itself</param>
        public void Render(Matrix4x4 transform, ISdf sdf, Material material = null)
        {
            if (registry == null)
            {
                registry = RegisterRenderers();
            }

            var renderingTransform = Matrix4x4.identity;

            var transforms = new List<Matrix4x4>();

            int depth = 0;

            var rendering = sdf;
            var cur = sdf;
            while (cur != null)
            {
                rendering = cur;

                var curTransform = cur.RenderingTransform();
                if (curTransform.HasValue)
                {
                    transforms.Add(curTransform.Value);
                }

                cur = cur.RenderChild();

                if (++depth > 30)
                {
                    Debug.Log("Max SDF rendering depth reached");
                    break;
                }
            }

            if (registry.TryGetValue(rendering.GetType(), out ISdfRenderer renderer))
            {
                transforms.Reverse();

                foreach (var matrix in transforms)
                {
                    renderingTransform = matrix * renderingTransform;
                }

                renderingTransform = transform * renderingTransform;

                renderer.Render(renderingTransform, material);
            }
        }
    }
}
