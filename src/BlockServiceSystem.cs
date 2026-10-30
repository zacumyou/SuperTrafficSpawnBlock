using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Policies;
using Game.Tools;
using Game.Objects;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    public partial class BlockServiceSystem : GameSystemBase
    {
        private EntityQuery blocks;
        private EntityQuery waiting;
        private EndFrameBarrier barrier;
        private BlockPrefabSystem registration;
        internal static bool IsOn(EntityManager manager, Entity e) =>
            manager.Exists(e) && (!manager.HasComponent<Building>(e) ||
                !BuildingUtils.CheckOption(manager.GetComponentData<Building>(e), BuildingOption.Inactive));
        protected override void OnCreate()
        {
            base.OnCreate();
            registration = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            barrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            waiting = GetEntityQuery(ComponentType.ReadOnly<GeneratedTraffic>(), ComponentType.ReadOnly<Game.Objects.Unspawned>(),
                ComponentType.ReadOnly<Car>(), ComponentType.ReadOnly<TripSource>(), ComponentType.Exclude<Deleted>());
            blocks = GetEntityQuery(ComponentType.ReadOnly<Game.Buildings.TrafficSpawner>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Destroyed>(), ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Game.City.CityServiceUpkeep>());
        }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;
        protected override void OnUpdate()
        {
            if (!Mod.Active || !registration.Ready) return;
            using (var entities = blocks.ToEntityArray(Allocator.Temp))
                foreach (var e in entities)
                {
                    var prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
                    if (prefab != registration.SinkPrefab && registration.FactorFor(prefab) == 0) continue;
                    // Native ActionsSection requires these, and handles its own policy/save state.
                    // Existing buildings receive the same support without replacing their prefab.
                    if (!EntityManager.HasBuffer<Policy>(e)) EntityManager.AddBuffer<Policy>(e);
                    if (!EntityManager.HasBuffer<Efficiency>(e)) EntityManager.AddBuffer<Efficiency>(e);
                    if (!EntityManager.HasComponent<Game.City.CityServiceUpkeep>(e)) EntityManager.AddComponent<Game.City.CityServiceUpkeep>(e);
                }
            var commands = barrier.CreateCommandBuffer();
            using (var cars = waiting.ToEntityArray(Allocator.Temp))
                foreach (var car in cars)
                {
                    var source = EntityManager.GetComponentData<TripSource>(car).m_Source;
                    if (!EntityManager.Exists(source) || IsOn(EntityManager, source)) continue;
                    VehicleUtils.DeleteVehicle(commands, car, EntityManager.HasBuffer<LayoutElement>(car) ? EntityManager.GetBuffer<LayoutElement>(car, true) : default);
                }
        }
    }
}
