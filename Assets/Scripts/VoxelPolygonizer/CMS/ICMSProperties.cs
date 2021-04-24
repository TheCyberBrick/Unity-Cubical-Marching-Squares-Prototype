using Unity.Collections;

namespace VoxelPolygonizer.CMS
{
    public interface ICMSProperties
    {
        /// <summary>
        /// Returns whether the specified material is solid.
        /// </summary>
        /// <param name="material">Material ID</param>
        /// <returns>
        /// Returns whether the specified material is solid.
        /// </returns>
        bool IsSolid(int material); 

        /// <summary>
        /// Returns whether there is a sharp feature in a 2D cell.
        /// </summary>
        /// <param name="theta">
        /// theta := n₁ᵀ n₂, where n₁ and n₂ are the normals at the surface intersections projected to the face cell's plane.
        /// This value is the cosine of the angle between the two projected 2D normals.
        /// </param>
        /// <param name="phi">
        /// featureAngle3D := n₁ᵀ n₂, where n₁ and n₂ are the normals at the surface intersections.
        /// This value is the cosine of the angle between the two 3D normals.
        /// </param>
        /// <param name="materials">All the material IDs in the 2D cell.</param>
        /// <returns>
        /// Returns whether there is a sharp feature in a 2D cell.
        /// </returns>
        bool IsSharp2DFeature(float theta, float phi, NativeArray<int> materials);

        /// <summary>
        /// Returns whether there is a sharp feature in a 3D component.
        /// </summary>
        /// <param name="theta">
        /// theta := minᵢ,ⱼ(nᵢᵀ nⱼ), where nᵢ and nⱼ are normals of the component.
        /// This value is the cosine of the maximum angle between the component's normals.
        /// </param>
        /// <param name="materials">All material IDs in the 3D cell.</param>
        /// <returns>
        /// Returns whether there is a sharp feature in a 3D component.
        /// </returns>
        bool IsSharp3DFeature(float theta, ComponentMaterials materials);

        /// <summary>
        /// Returns whether there is a sharp corner feature in a 3D component.
        /// </summary>
        /// <param name="phi">
        /// phi := maxᵢ|nᵢᵀ n*|, where n* = n₀ x n₁ and n₀/n₁ are the component's normals that span the maximum angle.
        /// This value is the cosine of the minimum angle between the component's normals and the normal of the plane spanned by n₀ and n₁.
        /// </param>
        /// <param name="materials">All material IDs in the 3D cell.</param>
        /// <returns>
        /// Returns whether there is a sharp corner feature in a 3D component.
        /// </returns>
        bool IsSharp3DCornerFeature(float phi, ComponentMaterials materials);

        /// <summary>
        /// Returns whether there is a valid and sensible transition feature in a 2D component. Since the angles may depend on the orientation of the
        /// segment this method requires both a min and a max value. Usually the max value is used to specify a lower bound on angles.
        /// </summary>
        /// <param name="minTheta">
        /// minTheta := minᵢ|n₁ᵀ nᵢ|, where n₁ is the normal of the material transition and nᵢ the surface normals.
        /// This value is the cosine of the maximum angle between the normals.
        /// </param>
        /// <param name="maxTheta">
        /// maxTheta := maxᵢ|n₁ᵀ nᵢ|, where n₁ is the normal of the material transition and nᵢ the surface normals.
        /// This value is the cosine of the minimum angle between the normals.
        /// </param>
        /// <param name="materials">All material IDs in the 2D cell.</param>
        /// <returns>
        /// Returns whether there is a valid and sensible transition feature in a 2D component. Since the angles may depend on the orientation of the
        /// segment this method requires both a min and a max value. Usually the max value is used to specify a lower bound on angles.
        /// </returns>
        bool IsValid2DTransitionFeature(float minTheta, float maxTheta, NativeArray<int> materials);

        /// <summary>
        /// Returns whether there is a valid and sensible transition feature in a 3D component.
        /// For material transitions even in normal cases theta can be negative which means there
        /// are material transitions on opposite corners and that can produce very unstable features.
        /// </summary>
        /// <param name="theta">
        /// theta := minᵢ,ⱼ(nᵢᵀ nⱼ), where nᵢ and nⱼ are the material transition (not surface!) normals of the component.
        /// This value is the cosine of the maximum angle between the component's normals and the normal of the plane spanned by n₀ and n₁.
        /// </param>
        /// <param name="materials">All material IDs in the 3C cell.</param>
        /// <returns>
        /// Returns whether there is a valid and sensible transition feature in a 3D component.
        /// For material transitions even in normal cases theta can be negative which means there
        /// are material transitions on opposite corners and that can produce very unstable features.
        /// </returns>
        bool IsValid3DTransitionFeature(float theta, ComponentMaterials materials);
    }

    public struct CMSStandardProperties : ICMSProperties
    {
        public bool IsSolid(int material)
        {
            return material != 0;
        }

        public bool IsSharp2DFeature(float theta, float phi, NativeArray<int> materials)
        {
            return theta < 0.6f && phi < 0.6f;
        }

        public bool IsSharp3DFeature(float theta, ComponentMaterials materials)
        {
            return theta < 0.6f;
        }

        public bool IsSharp3DCornerFeature(float phi, ComponentMaterials materials)
        {
            return phi > 0.7f;
        }

        public bool IsValid2DTransitionFeature(float minTheta, float maxTheta, NativeArray<int> materials)
        {
            //min theta, max theta??
            return /*maxTheta < 0.9999f*/ minTheta < 0.99f;
        }

        public bool IsValid3DTransitionFeature(float theta, ComponentMaterials materials)
        {
            return theta > -0.999f;
        }
    }
}