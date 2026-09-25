namespace SuperTrafficSpawnBlock
{
    internal static class BlockVariants
    {
        internal static readonly int[] Factors = { 1, 2, 5, 10, 20, 50 };
        // Preserve the original prefab identity for existing saves; only its displayed name changes.
        internal static string PrefabName(int factor) => factor == 20 ? "Super Traffic Spawn Block" : "Super Traffic Spawn Block " + factor + "x";
        internal static string DisplayName(int factor) => "Super Traffic Spawn Block " + factor + "x";
    }
}
