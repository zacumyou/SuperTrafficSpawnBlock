using Colossal.Mathematics;
using Unity.Mathematics;

namespace SuperTrafficSpawnBlock
{
    internal struct SpawnFootprint
    {
        internal float3 Center;
        internal float2 Right, Forward, Half;
        internal float Height;

        internal static SpawnFootprint Create(float3 position, quaternion rotation, Bounds3 bounds, bool clearance)
        {
            float3 half = math.max((bounds.max - bounds.min) * 0.5f, new float3(0.25f));
            return new SpawnFootprint {
                Center = position + math.rotate(rotation, (bounds.max + bounds.min) * 0.5f),
                Right = math.normalizesafe(math.rotate(rotation, new float3(1, 0, 0)).xz, new float2(1, 0)),
                Forward = math.normalizesafe(math.forward(rotation).xz, new float2(0, 1)),
                Half = half.xz + (clearance ? new float2(0.35f, 2f) : float2.zero),
                Height = half.y + (clearance ? 0.25f : 0f)
            };
        }
        internal Bounds3 SearchBounds()
        {
            float2 size = math.abs(Right) * Half.x + math.abs(Forward) * Half.y;
            var extent = new float3(size.x, Height, size.y);
            return new Bounds3(Center - extent, Center + extent);
        }
        internal bool Overlaps(SpawnFootprint other)
        {
            if (math.abs(Center.y - other.Center.y) >= Height + other.Height) return false;
            float2 delta = other.Center.xz - Center.xz;
            return OnAxis(other, delta, Right) && OnAxis(other, delta, Forward) &&
                OnAxis(other, delta, other.Right) && OnAxis(other, delta, other.Forward);
        }
        private bool OnAxis(SpawnFootprint other, float2 delta, float2 axis) => math.abs(math.dot(delta, axis)) <
            math.abs(math.dot(Right, axis)) * Half.x + math.abs(math.dot(Forward, axis)) * Half.y +
            math.abs(math.dot(other.Right, axis)) * other.Half.x + math.abs(math.dot(other.Forward, axis)) * other.Half.y;
    }
}
