using System;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Static SDF shape renderers are intended to be used for simple SDF types that do not change,
    /// such as for example boxes, spheres, etc.
    /// </summary>
    public class StaticSdfShapeRenderer : ScriptableObject, SdfShapeRenderHandler.ISdfRenderer
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
