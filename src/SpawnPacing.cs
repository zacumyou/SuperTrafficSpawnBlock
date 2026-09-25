using System;
namespace SuperTrafficSpawnBlock
{
    internal static class SpawnPacing
    {
        internal const int Window = 256;
        internal static int Budget(int factor, int pending) => Math.Max(0, Math.Min(factor, Math.Max(8, factor * 2) - pending));
        internal static int Delay(int index, int count) => count <= 0 ? 0 : index * Window / count;
    }
}
