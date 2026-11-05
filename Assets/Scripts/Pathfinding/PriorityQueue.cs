using System.Collections.Generic;

public class PriorityQueue<T>
{
    #region Private Types
    private struct Entry
    {
        public T Item;
        public float Priority;

        public Entry(T item, float priority)
        {
            Item = item;
            Priority = priority;
        }
    }
    #endregion

    #region Private Fields
    private readonly List<Entry> _heap = new();
    #endregion

    #region Properties
    public int Count => _heap.Count;
    public bool IsEmpty => _heap.Count == 0;
    #endregion

    #region Public Methods
    public void Enqueue(T item, float priority)
    {
        _heap.Add(new Entry(item, priority));
        HeapifyUp(_heap.Count - 1);
    }

    public T Dequeue()
    {
        T root = _heap[0].Item;

        int lastIndex = _heap.Count - 1;
        _heap[0] = _heap[lastIndex];
        _heap.RemoveAt(lastIndex);

        if (_heap.Count > 0) HeapifyDown(0);

        return root;
    }

    public void Clear() => _heap.Clear();
    #endregion

    #region Private Methods
    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;

            if (_heap[index].Priority >= _heap[parent].Priority) break;

            Swap(index, parent);
            index = parent;
        }
    }

    private void HeapifyDown(int index)
    {
        int lastIndex = _heap.Count - 1;

        while (true)
        {
            int left = index * 2 + 1;
            int right = index * 2 + 2;

            if (left > lastIndex) break;

            int smallest = left;
            if (right <= lastIndex && _heap[right].Priority < _heap[left].Priority) smallest = right;

            if (_heap[index].Priority <= _heap[smallest].Priority) break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b) => (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
    #endregion
}
