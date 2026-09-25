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
    // Empty tags must use the native empty-component contract. Zero-byte ISerializable fails saving.
    public struct RouteProbe : IComponentData, IEmptySerializable { }
    public struct GeneratedTraffic : IComponentData, IEmptySerializable { }
}

