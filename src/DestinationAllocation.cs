using System;
namespace SuperTrafficSpawnBlock
{
    internal static class DestinationAllocation
    {
        internal static int[] Allocate(int vehicles, int destinations, Func<int, int> draw)
        {
            if (vehicles < 0 || destinations <= 0) throw new ArgumentOutOfRangeException();
            var counts = new int[destinations];
            for (int i = 0; i < vehicles; i++) counts[draw(destinations)]++;
            return counts;
        }
    }
}
