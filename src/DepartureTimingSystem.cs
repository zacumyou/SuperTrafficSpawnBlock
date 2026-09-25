using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    public partial class DepartureTimingSystem : GameSystemBase
    {
        private EntityQuery fresh;
        private BlockPrefabSystem registration;
        private readonly Dictionary<Entity, List<Entity>> groups = new Dictionary<Entity, List<Entity>>();
        protected override void OnCreate()
        {
            base.OnCreate();
            registration = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            fresh = GetEntityQuery(ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Unspawned>(), ComponentType.ReadOnly<Car>(), ComponentType.ReadWrite<TripSource>());
            RequireForUpdate(fresh);
        }
        protected override void OnUpdate()
        {
            if (!Mod.Active || !registration.Ready) return;
            groups.Clear();
            using (var cars = fresh.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity car in cars)
                {
                    Entity source = EntityManager.GetComponentData<TripSource>(car).m_Source;
                    if (!EntityManager.HasComponent<PrefabRef>(source) || registration.FactorFor(EntityManager.GetComponentData<PrefabRef>(source).m_Prefab) == 0) continue;
                    if (!groups.TryGetValue(source, out var group)) groups[source] = group = new List<Entity>();
                    group.Add(car);
                }
                foreach (var group in groups.Values)
                    for (int i = 0; i < group.Count; i++)
                    {
                        var trip = EntityManager.GetComponentData<TripSource>(group[i]);
                        trip.m_Timer = SpawnPacing.Delay(i, group.Count);
                        EntityManager.SetComponentData(group[i], trip);
                    }
            }
        }
    }
}

