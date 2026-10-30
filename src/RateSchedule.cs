using System.Collections.Generic;
namespace SuperTrafficSpawnBlock
{
    internal sealed class RateSchedule<T>
    {
        // Keep the serialized int contract. -2 is the newly introduced half-rate code.
        internal const int Half = -2;
        internal static readonly int[] Choices = { Half, 1, 2, 3, 5, 10, 15, 20, 50 };
        private readonly HashSet<T> halfPending = new HashSet<T>();
        internal static bool Allowed(int value) => System.Array.IndexOf(Choices, value) >= 0;
        internal int Next(T source, int rate)
        {
            if (rate != Half) { halfPending.Remove(source); return rate; }
            if (halfPending.Remove(source)) return 1;
            halfPending.Add(source); return 0;
        }
        internal void Reset(T source) => halfPending.Remove(source);
        internal void Clear() => halfPending.Clear();
        internal void Retain(HashSet<T> live) => halfPending.RemoveWhere(source => !live.Contains(source));
    }
}
