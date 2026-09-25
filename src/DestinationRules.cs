using System.Collections.Generic;
namespace SuperTrafficSpawnBlock
{
    internal static class DestinationRules
    {
        internal static bool Matches<T>(T choice, T destination, T none, bool outside, bool alive)
        {
            if (!alive) return false;
            return EqualityComparer<T>.Default.Equals(choice, none) ? outside : EqualityComparer<T>.Default.Equals(choice, destination);
        }
    }
}
