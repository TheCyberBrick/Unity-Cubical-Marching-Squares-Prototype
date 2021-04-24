using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class VoxelTerrainTextures : MonoBehaviour
{
    [SerializeField] private Texture2D[] textures;

    void Start()
    {
        if(textures != null && textures.Length > 0)
        {
            Texture2DArray texArray = new Texture2DArray(textures[0].width, textures[0].height, textures.Length, TextureFormat.RGBA32, true, true);

            for (int i = 0; i < textures.Length; i++)
            {
                texArray.SetPixels(textures[i].GetPixels(), i, 0);
            }
            texArray.filterMode = FilterMode.Bilinear;
            texArray.wrapMode = TextureWrapMode.Repeat;
            texArray.Apply();

            GetComponent<MeshRenderer>().material.SetTexture("_TextureArray", texArray);
            GetComponent<MeshRenderer>().material.SetInt("_TextureArrayLength", textures.Length);
        }
    }
}
