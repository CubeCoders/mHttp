using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Collections;

namespace m.Utils
{
    class CountingDictionary<K> : IEnumerable<KeyValuePair<K, CountingDictionary<K>.Counter>>
    {
        internal class Counter
        {
            int count;

            public int Count => count;

            public Counter(int count)
            {
                this.count = count;
            }

            public void Increment() => Interlocked.Increment(ref count);
        }

        readonly IDictionary<K, Counter> counters;

        public CountingDictionary()
        {
            counters = new ConcurrentDictionary<K, Counter>();
        }

        public IEnumerator<KeyValuePair<K, Counter>> GetEnumerator() => counters.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Count(K t)
        {
            if (!counters.TryGetValue(t, out Counter counter))
            {
                lock (counters)
                {
                    if (!counters.TryGetValue(t, out counter))
                    {
                        counter = new Counter(0);
                        counters.Add(t, counter);
                    }
                }
            }

            counter.Increment();
        }
    }
}
