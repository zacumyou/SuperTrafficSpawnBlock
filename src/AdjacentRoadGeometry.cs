using Colossal.Mathematics;
using Unity.Mathematics;

namespace SuperTrafficSpawnBlock
{
    internal static class AdjacentRoadGeometry
    {
        internal static bool FacesRoad(float3 block, float3 center, quaternion rotation) =>
            math.dot(math.normalizesafe(center.xz - block.xz), math.normalizesafe(math.forward(rotation).xz)) >= 0.8660254f;

        internal static void CheckBoundary(Bezier4x3 boundary, float3 block, float3 front, float3 center, ref float best)
        {
            float gap = MathUtils.Distance(boundary.xz, front.xz, out float t);
            float3 point = MathUtils.Position(boundary, t);
            float2 outward = math.normalizesafe(point.xz - center.xz);
            // Do not reach across the carriageway, or attach a ground block to a bridge/tunnel.
            if (gap > 8f || math.abs(point.y - block.y) > 3f ||
                math.dot(block.xz - point.xz, outward) < -1f || math.dot(block.xz - center.xz, outward) <= 0f) return;
            best = math.min(best, gap);
        }
    }
}
