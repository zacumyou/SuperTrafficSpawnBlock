using Colossal.Serialization.Entities;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    public struct DestinationChoice : IComponentData, ISerializable
    {
        public Entity Sink;
        public void Serialize<T>(T writer) where T : IWriter { writer.Write(Sink); }
        public void Deserialize<T>(T reader) where T : IReader { reader.Read(out Sink); }
    }
    public struct DestinationTarget : IBufferElementData, ISerializable
    {
        public Entity Sink;
        public void Serialize<T>(T writer) where T : IWriter { writer.Write(Sink); }
        public void Deserialize<T>(T reader) where T : IReader { reader.Read(out Sink); }
    }
    public struct SpawnRate : IComponentData, ISerializable
    {
        public int Factor;
        public void Serialize<T>(T writer) where T : IWriter { writer.Write(Factor); }
        public void Deserialize<T>(T reader) where T : IReader { reader.Read(out Factor); }
    }
    public struct SinkRange : IComponentData, ISerializable
    {
        public int Radius;
        public static bool Allowed(int radius) => radius == 0 || radius == 16 || radius == 32 || radius == 64;
        public static int Get(EntityManager manager, Entity sink)
        {
            int value = manager.HasComponent<SinkRange>(sink) ? manager.GetComponentData<SinkRange>(sink).Radius : 0;
            return value == 8 ? 16 : Allowed(value) ? value : 0;
        }
        public void Serialize<T>(T writer) where T : IWriter { writer.Write(Radius); }
        public void Deserialize<T>(T reader) where T : IReader { reader.Read(out Radius); }
    }
    // Empty tags must use the native empty-component contract. Zero-byte ISerializable fails saving.
    public struct RouteProbe : IComponentData, IEmptySerializable { }
    public struct GeneratedTraffic : IComponentData, IEmptySerializable { }
}

