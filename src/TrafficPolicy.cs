namespace SuperTrafficSpawnBlock
{
    internal static class TrafficPolicy
    {
        internal static bool Reject(bool superSource, bool superDestination, bool heavy, bool allowHeavy)
        {
            return superDestination || (superSource && heavy && !allowHeavy);
        }
    }
}
