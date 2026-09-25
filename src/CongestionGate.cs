using System;
using System.Collections.Generic;
namespace SuperTrafficSpawnBlock
{
    // Block-local hysteresis. Overdue means a full spawn window past the scheduled departure.
    internal sealed class CongestionGate<T>
    {
        private readonly HashSet<T> paused = new HashSet<T>();
        internal bool ShouldPause(T source, int factor, int pending, int overdue)
        {
            if (paused.Contains(source))
            {
                if (overdue == 0 && pending <= Math.Max(1, factor / 4)) paused.Remove(source);
            }
            else if (overdue >= Math.Max(2, (factor + 3) / 4)) paused.Add(source);
            return paused.Contains(source);
        }
        internal void Retain(HashSet<T> live) => paused.RemoveWhere(source => !live.Contains(source));
        internal void Clear() => paused.Clear();
    }
}
