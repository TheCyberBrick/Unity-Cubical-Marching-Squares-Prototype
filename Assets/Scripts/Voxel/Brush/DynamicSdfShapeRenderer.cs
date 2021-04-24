using System;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Dynamic SDF renderers are used by SDFs that change their properties during runtime. An example
    /// for this are <see cref="CustomBrush{TBrushType, TEvaluator}"/>'s.
    /// </summary>
    public class DynamicSdfShapeRenderer : MonoBehaviour, SdfShapeRenderHandler.ISdfRenderer
    {
        public virtual void Render(Matrix4x4 transform, Material material = null)
        {

        }

        public virtual Type SdfType()
        {
            return null;
        }
    }
}
