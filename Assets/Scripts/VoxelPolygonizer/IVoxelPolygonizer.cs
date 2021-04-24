using Unity.Collections;

namespace VoxelPolygonizer
{
    public interface IVoxelPolygonizer<TCell> where TCell : IVoxelCell
    {
        void Polygonize(TCell cell, NativeList<VoxelMeshComponent> components, NativeList<PackedIndex> componentIndices, NativeList<VoxelMeshComponentVertex> componentVertices);
    } 
}