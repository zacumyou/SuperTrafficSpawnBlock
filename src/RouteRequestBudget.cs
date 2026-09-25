using System;
namespace SuperTrafficSpawnBlock
{
    internal static class RouteRequestBudget
    {
        internal const int PerWindow = 4;
        internal const int MaxPending = 16;
        internal static int Available(int pending) => Math.Max(0, Math.Min(PerWindow, MaxPending - pending));
    }
}
