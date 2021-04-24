using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    public class VoxelEditManager<TIndexer> : IDisposable
        where TIndexer : struct, IIndexer
    {
        public int QueueSize
        {
            get;
            set;
        }

        private readonly VoxelWorld<TIndexer> world;

        private List<VoxelEdit<TIndexer>> edits;
        private List<VoxelEdit<TIndexer>> undone;

        private bool wasPreviousUndo = false;

        public VoxelEditManager(VoxelWorld<TIndexer> world, int queueSize)
        {
            this.world = world;
            QueueSize = queueSize;
            edits = new List<VoxelEdit<TIndexer>>();
            undone = new List<VoxelEdit<TIndexer>>();
        }

        public bool Undo()
        {
            wasPreviousUndo = true;

            if (edits.Count > 0)
            {
                if (undone.Count == 0)
                {
                    //We need a snapshot of the current state so that the latest edit can be undone as well

                    var latestSnapshots = new List<VoxelEdit<TIndexer>>();

                    var edit = edits[edits.Count - 1];
                    edit.Restore((latest) => latestSnapshots.Add(latest));

                    undone.Add(new VoxelEdit<TIndexer>(world, latestSnapshots));

                    edits.Remove(edit);
                    undone.Add(edit);
                }
                else
                {
                    var edit = edits[edits.Count - 1];
                    edit.Restore(null);

                    edits.Remove(edit);
                    undone.Add(edit);
                }

                return true;
            }

            return false;
        }

        public bool Redo()
        {
            if(wasPreviousUndo && undone.Count > 0)
            {
                //No need to restore to before first undone edit
                var edit = undone[undone.Count - 1];
                undone.Remove(edit);
                edits.Add(edit);
            }

            wasPreviousUndo = false;

            if (undone.Count > 0)
            {
                var edit = undone[undone.Count - 1];
                edit.Restore(null);

                undone.Remove(edit);

                //Don't re-add snapshot of the original state
                if(undone.Count != 0)
                {
                    edits.Add(edit);
                }
                else
                {
                    edit.Dispose();
                }

                return true;
            }

            return false;
        }

        private void RemoveEdit(VoxelEdit<TIndexer> edit)
        {
            edits.Remove(edit);
            edit.Dispose();
        }

        private void QueueEdit(VoxelEdit<TIndexer> edit)
        {
            if (edits.Count >= QueueSize)
            {
                RemoveEdit(edits[0]);
            }

            edits.Add(edit);

            //Remove all undone edits because they cannot be redone anymore
            wasPreviousUndo = true;
            foreach (VoxelEdit<TIndexer> undoneEdit in undone)
            {
                undoneEdit.Dispose();
            }
            undone.Clear();
        }

        public VoxelWorld<TIndexer>.VoxelEditConsumer<TIndexer> Consumer()
        {
            return QueueEdit;
        }

        public void Dispose()
        {
            foreach (VoxelEdit<TIndexer> edit in edits)
            {
                edit.Dispose();
            }
            edits.Clear();

            foreach (VoxelEdit<TIndexer> edit in undone)
            {
                edit.Dispose();
            }
            undone.Clear();
        }
    }
}
