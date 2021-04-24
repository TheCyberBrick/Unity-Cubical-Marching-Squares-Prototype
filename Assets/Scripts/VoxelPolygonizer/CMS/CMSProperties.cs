using System;
using Unity.Collections;
using UnityEngine;
using VoxelPolygonizer.CMS;

[CreateAssetMenu(fileName = "CMSProperties", menuName = "ScriptableObjects/CMSProperties", order = 1)]
public class CMSProperties : ScriptableObject
{
    [SerializeField] private DataStruct data = DataStruct.Default();
    public DataStruct Data 
    {
        get
        {
            return data;
        }
    }

    [Serializable]
    public struct DataStruct : ICMSProperties
    {
        public static DataStruct Default()
        {
            return new DataStruct
            {
                airMaterial = 0,
                _2DSharpFeatureProperties = new SharpFeatureProperties2D
                {
                    maxFeatureTheta = 0.6f,
                    maxFeaturePhi = 0.6f,
                    maxTransitionTheta = 0.99f
                },
                _3DSharpFeatureProperties = new SharpFeatureProperties3D
                {
                    maxFeatureTheta = 0.6f,
                    minCornerPhi = 0.7f,
                    minTransitionTheta = -0.999f
                },

            };
        }

        public int airMaterial;

        [Serializable]
        public struct SharpFeatureProperties2D
        {
            public float maxFeatureTheta;
            public float maxFeaturePhi;
            public float maxTransitionTheta;
        }
        public SharpFeatureProperties2D _2DSharpFeatureProperties;

        [Serializable]
        public struct SharpFeatureProperties3D
        {
            public float maxFeatureTheta;
            public float minCornerPhi;
            public float minTransitionTheta;
        }
        public SharpFeatureProperties3D _3DSharpFeatureProperties;

        public bool IsSolid(int material)
        {
            return material != airMaterial;
        }

        public bool IsSharp2DFeature(float theta, float phi, NativeArray<int> materials)
        {
            return theta < _2DSharpFeatureProperties.maxFeatureTheta && phi < _2DSharpFeatureProperties.maxFeaturePhi;
        }

        public bool IsSharp3DFeature(float theta, ComponentMaterials materials)
        {
            return theta < _3DSharpFeatureProperties.maxFeatureTheta;
        }

        public bool IsSharp3DCornerFeature(float phi, ComponentMaterials materials)
        {
            return phi > _3DSharpFeatureProperties.minCornerPhi;
        }

        public bool IsValid2DTransitionFeature(float minTheta, float maxTheta, NativeArray<int> materials)
        {
            //min theta, max theta??
            return /*maxTheta < 0.9999f*/ minTheta < _2DSharpFeatureProperties.maxTransitionTheta;
        }

        public bool IsValid3DTransitionFeature(float theta, ComponentMaterials materials)
        {
            return theta > _3DSharpFeatureProperties.minTransitionTheta;
        }
    }
}