using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    public struct BlockIdentity : IComponentData, ISerializable
    {
        public int Number;
        public void Serialize<T>(T writer) where T : IWriter { writer.Write(Number); }
        public void Deserialize<T>(T reader) where T : IReader { reader.Read(out Number); }
    }
    public partial class BlockIdentitySystem : GameSystemBase
    {
        private EntityQuery query;
        private BlockPrefabSystem blocks;
        private Game.UI.NameSystem names;
        private readonly HashSet<int> used = new HashSet<int>();
        private float nextRefresh;
        private int nextNumber;
        protected override void OnCreate()
        {
            base.OnCreate();
            blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            names = World.GetOrCreateSystemManaged<Game.UI.NameSystem>();
            query = GetEntityQuery(ComponentType.ReadOnly<Game.Buildings.TrafficSpawner>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Destroyed>());
        }
        private bool IsBlock(Entity e)
        {
            var prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
            return prefab == blocks.SinkPrefab || blocks.FactorFor(prefab) > 0;
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode) { nextRefresh = 0; nextNumber = 0; }
        protected override void OnUpdate()
        {
            if (!Mod.Active || !blocks.Ready || UnityEngine.Time.realtimeSinceStartup < nextRefresh) return;
            nextRefresh = UnityEngine.Time.realtimeSinceStartup + 0.5f;
            using (var entities = query.ToEntityArray(Allocator.Temp))
            {
                used.Clear();
                foreach (var e in entities)
                    if (IsBlock(e) && EntityManager.HasComponent<BlockIdentity>(e))
                        nextNumber = System.Math.Max(nextNumber, EntityManager.GetComponentData<BlockIdentity>(e).Number);
                foreach (var e in entities)
                {
                    if (!IsBlock(e)) continue;
                    int old = EntityManager.HasComponent<BlockIdentity>(e) ? EntityManager.GetComponentData<BlockIdentity>(e).Number : 0;
                    int number = old;
                    if (number <= 0 || !used.Add(number))
                    {
                        number = ++nextNumber; used.Add(number);
                        var data = new BlockIdentity { Number = number };
                        if (EntityManager.HasComponent<BlockIdentity>(e)) EntityManager.SetComponentData(e, data);
                        else EntityManager.AddComponentData(e, data);
                    }
                    string label = names.GetRenderedLabelName(e), suffix = " #" + number;
                    if (label.EndsWith(suffix, System.StringComparison.Ordinal)) continue;
                    // Preserve user labels; replace only the previous identifier when a copied block gets a new one.
                    string previous = " #" + old;
                    if (old > 0 && label.EndsWith(previous, System.StringComparison.Ordinal)) label = label.Substring(0, label.Length - previous.Length);
                    names.SetCustomName(e, label + suffix);
                }
            }
        }
    }
}
