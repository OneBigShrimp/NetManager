using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyNetManager
{

    internal sealed class ConcurrentQueue<T>
    {
        private readonly Queue<T> _inner = new Queue<T>();
        private readonly object _obj = new object();

        public int Count
        {
            get
            {
                lock (_obj)
                {
                    return _inner.Count;
                }
            }
        }

        public bool TryDequeue(out T item)
        {
            lock (_obj)
            {
                if (_inner.Count == 0)
                {
                    item = default(T);
                    return false;
                }
                item = _inner.Dequeue();
                return true;
            }
        }

        public void Enqueue(T item)
        {
            lock (_obj)
            {
                _inner.Enqueue(item);
            }
        }

        public void Clear()
        {
            lock (_obj)
            {
                _inner.Clear();
            }
        }
    }
}
