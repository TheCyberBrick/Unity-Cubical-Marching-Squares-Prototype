using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Voxel
{
    [RequireComponent(typeof(Transform))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VoxelWorldContainer : MonoBehaviour
    {
        [SerializeField] private int chunkSize = 16;
        public int ChunkSize
        {
            get
            {
                return chunkSize;
            }
        }

        [SerializeField] private CMSProperties cmsProperties = null;
        public CMSProperties CMSProperties
        {
            get
            {
                return cmsProperties;
            }
        }

        public IndexerFactory<MortonIndexer> IndexerFactory
        {
            get
            {
                return (xSize, ySize, zSize) => new MortonIndexer(xSize, ySize, zSize);
            }
        }

        private MeshRenderer meshRenderer;

        private VoxelWorld<MortonIndexer> _instance;
        public VoxelWorld<MortonIndexer> Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VoxelWorld<MortonIndexer>(ChunkSize, CMSProperties, transform, IndexerFactory);
                }
                return _instance;
            }
        }

        void Start()
        {
            meshRenderer = gameObject.GetComponent<MeshRenderer>();
        }

        void Update()
        {
            Instance.Transform = transform;
            Instance.Update();
            Instance.Render(Matrix4x4.identity, meshRenderer.material);
        }

        void OnApplicationQuit()
        {
            Instance.Dispose();
        }
    }
}
