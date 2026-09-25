using System.Collections.Generic;

namespace SuperTrafficSpawnBlock
{
    // Rebuild from distinct ready requests, so a repeated update cannot multiply its own output.
    internal sealed class DispatchPlan<T>
    {
        private readonly HashSet<T> seen = new HashSet<T>();

        internal static int NormalizeMultiplier(int value)
        {
            switch (value)
            {
                case 1: case 2: case 5: case 10: case 20: case 50: return value;
                default: return 20;
            }
        }

        internal void Build(IReadOnlyList<T> input, ISet<T> ready, int multiplier, List<T> output)
        {
            int count = NormalizeMultiplier(multiplier);
            output.Clear();
            seen.Clear();
            foreach (T request in input)
            {
                if (!ready.Contains(request)) { output.Add(request); continue; }
                if (!seen.Add(request)) continue;
                for (int i = 0; i < count; i++) output.Add(request);
            }
        }
    }
}
