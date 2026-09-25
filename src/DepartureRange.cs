namespace SuperTrafficSpawnBlock
{
    internal static class DepartureRange
    {
        internal static bool IsFullForwardExit(float start, float end, float nextStart, float nextEnd)
        {
            return start >= 0f && start < end && end >= 0.9999f && end <= 1f &&
                nextStart >= 0f && nextStart <= 0.0001f && nextEnd > nextStart && nextEnd <= 1f;
        }
    }
}
