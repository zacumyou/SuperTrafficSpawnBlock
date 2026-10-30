using Colossal.Mathematics;
using Unity.Mathematics;
using SuperTrafficSpawnBlock;

internal static class SpawnFootprintTests
{
    internal static void Run()
    {
        var car = new Bounds3(new float3(-1, 0, -2.5f), new float3(1, 1.5f, 2.5f));
        SpawnFootprint Body(float x, float y, float z, bool clearance = false, float turn = 0) =>
            SpawnFootprint.Create(new float3(x,y,z), quaternion.RotateY(turn), car, clearance);
        var departure = Body(0,0,0,true);
        Check(departure.Overlaps(Body(0,0,0)), "occupied spawn point waits");
        Check(departure.Overlaps(Body(0,0,6)), "queue ahead needs vehicle length and clearance");
        Check(departure.Overlaps(Body(0,0,-6)), "vehicle immediately behind also blocks");
        Check(!departure.Overlaps(Body(0,0,8)), "adequate gap resumes");
        Check(!departure.Overlaps(Body(3.5f,0,0)), "adjacent lane remains independent");
        Check(!departure.Overlaps(Body(0,6,0)), "bridge above is ignored");
        Check(!departure.Overlaps(Body(0,-6,0)), "tunnel below is ignored");
        Check(departure.Overlaps(Body(0,0,0,false,math.PI/2)), "crossing vehicle blocks physical overlap");
        var busBounds = new Bounds3(new float3(-1.25f,0,-8),new float3(1.25f,3,4));
        var bus = SpawnFootprint.Create(new float3(0,0,10),quaternion.identity,busBounds,false);
        Check(departure.Overlaps(bus), "long vehicle rear blocks despite distant pivot");
        var reserved = new System.Collections.Generic.List<SpawnFootprint>();
        int admitted = 0;
        for (int i=0;i<50;i++)
        {
            bool occupied = reserved.Exists(space => departure.Overlaps(space));
            if (!occupied) { reserved.Add(departure); admitted++; }
        }
        Check(admitted == 1, "50 simultaneously due cars cannot reserve the same position");
        var nextLane = Body(3.5f,0,0,true);
        Check(!reserved.Exists(space=>nextLane.Overlaps(space)), "independent lane can depart in same tick");
        Check(!Body(8,0,0,true,math.PI/4).Overlaps(Body(0,0,0,false,math.PI/4)), "rotated separated bodies do not trigger broad-phase false positive");
        System.Console.WriteLine("PASS: occupied spawn, front/rear clearance, bus bounds, adjacent lanes, heights, crossing traffic and simultaneous reservation.");
    }
    private static void Check(bool value,string message) { if (!value) throw new System.Exception("Spawn footprint: "+message); }
}
