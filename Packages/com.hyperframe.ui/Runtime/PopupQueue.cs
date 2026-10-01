using System.Collections.Generic;

namespace HyperFrame.UI
{
    /// <summary>Priority queue for pending popups: higher priority first, FIFO within a priority.</summary>
    public sealed class PopupQueue<T>
    {
        struct Item { public T Value; public int Priority; public long Order; }

        readonly List<Item> _items = new List<Item>();
        long _order;

        public int Count => _items.Count;

        public void Enqueue(T value, int priority)
        {
            var item = new Item { Value = value, Priority = priority, Order = _order++ };
            int index = _items.Count;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Priority < priority) { index = i; break; }
            }
            _items.Insert(index, item);
        }

        public bool TryDequeue(out T value)
        {
            if (_items.Count == 0) { value = default; return false; }
            value = _items[0].Value;
            _items.RemoveAt(0);
            return true;
        }

        public bool TryPeek(out T value)
        {
            value = _items.Count > 0 ? _items[0].Value : default;
            return _items.Count > 0;
        }

        public bool Remove(T value)
        {
            int i = _items.FindIndex(x => EqualityComparer<T>.Default.Equals(x.Value, value));
            if (i < 0) return false;
            _items.RemoveAt(i);
            return true;
        }

        public IEnumerable<T> Items
        {
            get { foreach (var i in _items) yield return i.Value; }
        }

        public void Clear() => _items.Clear();
    }
}
