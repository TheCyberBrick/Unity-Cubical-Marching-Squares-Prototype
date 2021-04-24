using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Voxel
{
    [RequireComponent(typeof(VoxelWorldContainer))]
    public class VoxelEditManagerContainer : MonoBehaviour
    {
        [SerializeField] private int queueSize = 5;

        private VoxelEditManager<MortonIndexer> _instance;
        public VoxelEditManager<MortonIndexer> Instance
        {
            get
            {
                if(_instance == null)
                {
                    _instance = new VoxelEditManager<MortonIndexer>(GetComponent<VoxelWorldContainer>().Instance, queueSize);
                }
                return _instance;
            }
        }

        public void OnApplicationQuit()
        {
            Instance.Dispose();
        }
    }
}
