using System;
using System.Threading;

namespace m.Utils
{
    sealed class LeakyBucket
    {
        public int Capacity { get; }
        public int LeakRate { get; }

        int currentSize;

        readonly object leakLock = new object();
        DateTime lastLeakOn;

        public LeakyBucket(int capacity, int leaksPerSecond)
        {
            Capacity = capacity;
            LeakRate = leaksPerSecond;

            currentSize = 0;
            lastLeakOn = DateTime.UtcNow;
        }

        public bool Fill(int amount)
        {
            int current = 0;
#if NET451
            current = Thread.VolatileRead(ref currentSize);
#endif
#if NETSTANDARD
            current = Volatile.Read(ref currentSize);
#endif
            var fillTo = current + amount;
            while (fillTo <= Capacity)
            {
                if (Interlocked.CompareExchange(ref currentSize, fillTo, current) == current)
                {
                    return true;
                }

                current = currentSize;
            }

            return false;
        }

        public void Leak()
        {
            lock (leakLock)
            {
                var current = currentSize;
                if (current == 0)
                {
                    lastLeakOn = DateTime.UtcNow;
                    return;
                }
                else
                {
                    var elapsed = (DateTime.UtcNow - lastLeakOn).TotalMilliseconds;
                    var leak = (elapsed / 1000) * LeakRate;
                    if (leak >= 1)
                    {
                        while (Interlocked.CompareExchange(ref currentSize, Math.Max(0, current - (int)leak), current) != current)
                        {
                            current = currentSize;
                            elapsed = (DateTime.UtcNow - lastLeakOn).TotalMilliseconds;
                            leak = (elapsed / 1000) * LeakRate;
                        }

                        lastLeakOn = DateTime.UtcNow;
                    }
                }
            }
        }
    }
}
