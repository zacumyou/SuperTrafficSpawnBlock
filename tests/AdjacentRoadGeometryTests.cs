using Colossal.Mathematics;
using Unity.Mathematics;
using SuperTrafficSpawnBlock;

internal static class AdjacentRoadGeometryTests
{
    internal static void Run()
    {
        var block = new float3(0, 0, 14);
        var front = new float3(0, 0, 10);
        var center = float3.zero;
        var facing = quaternion.RotateY(math.PI);
        Check(AdjacentRoadGeometry.FacesRoad(block, center, facing), "front toward road");
        Check(!AdjacentRoadGeometry.FacesRoad(block, center, quaternion.identity), "back toward road rejected");
        Check(!AdjacentRoadGeometry.FacesRoad(block, center, quaternion.RotateY(math.PI * 0.5f)), "parallel block rejected");
        Check(AdjacentRoadGeometry.FacesRoad(block, center, quaternion.RotateY(math.PI + math.radians(25))), "25 degree tolerance");
        Check(!AdjacentRoadGeometry.FacesRoad(block, center, quaternion.RotateY(math.PI + math.radians(35))), "35 degree rejection");
        float Gap(float z, float height, float3 at, float3 frontage)
        {
            var line = new Bezier4x3(new float3(-30,height,z), new float3(-10,height,z), new float3(10,height,z), new float3(30,height,z));
            float result = float.MaxValue;
            AdjacentRoadGeometry.CheckBoundary(line, at, frontage, center, ref result);
            return result;
        }
        Check(math.abs(Gap(8, 0, block, front) - 2f) < 0.01f, "adjacent shoulder accepted");
        Check(Gap(-8, 0, block, front) == float.MaxValue, "far side of road rejected");
        Check(Gap(8, 6, block, front) == float.MaxValue, "bridge above ground rejected");
        Check(Gap(8, -6, block, front) == float.MaxValue, "tunnel below ground rejected");
        Check(Gap(8, 1, block, front) < float.MaxValue, "small terrain height difference accepted");
        Check(Gap(8, 0, new float3(0,0,25), new float3(0,0,21)) == float.MaxValue, "distant block rejected");
        Check(Gap(8, 0, new float3(0,0,3), new float3(0,0,-1)) == float.MaxValue, "block inside carriageway rejected");
        System.Console.WriteLine("PASS: adjacent road frontage, rotation, same-side shoulder, distance and bridge/tunnel separation.");
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new System.Exception("Adjacent road: " + message);
    }
}
