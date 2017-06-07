using System.Threading;

namespace Archivator.GzipArchivator
{
    internal class BoundedBuffer<T>
    {
        private readonly Semaphore _availableItems;
        private readonly Semaphore _availableSpaces;
        private readonly T[] _items;
        private int _putPosition;
        private int _takePosition;

        public BoundedBuffer(int capacity)
        {
            _availableSpaces = new Semaphore(capacity, capacity);
            _availableItems = new Semaphore(0, capacity);
            _items = new T[capacity];
        }

        public void Add(T item)
        {
            _availableSpaces.WaitOne();
            lock (_items)
            {
                var i = _putPosition;
                _items[i] = item;
                _putPosition = (++i == _items.Length) ? 0 : i;
            }
            _availableItems.Release();
        }

        public T Take()
        {
            _availableItems.WaitOne();
            T item;
            lock (_items)
            {
                var i = _takePosition;
                item = _items[i];
                _items[i] = default(T);
                _takePosition = (++i == _items.Length) ? 0 : i;
            }
            _availableSpaces.Release();
            return item;
        }
    }
}
