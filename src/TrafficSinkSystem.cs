using Game;
using Game.Common;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    // Identify generated cars once, so later source demolition does not change their identity.
    public partial class GeneratedTrafficSystem : GameSystemBase
    {
        private EntityQuery fresh;
        private BlockPrefabSystem blocks;
        protected override void OnCreate()
        {
            base.OnCreate(); blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            fresh = GetEntityQuery(ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Car>(), ComponentType.ReadOnly<TripSource>(), ComponentType.Exclude<GeneratedTraffic>());
        }
        protected override void OnUpdate()
        {
            if (!Mod.Active || !blocks.Ready) return;
            using (var entities = fresh.ToEntityArray(Allocator.Temp))
                foreach (Entity e in entities)
                {
                    Entity source = EntityManager.GetComponentData<TripSource>(e).m_Source;
                    if (EntityManager.HasComponent<PrefabRef>(source) && blocks.FactorFor(EntityManager.GetComponentData<PrefabRef>(source).m_Prefab) > 0) EntityManager.AddComponent<GeneratedTraffic>(e);
                }
        }
    }
    public partial class TrafficSinkSystem : GameSystemBase
    {
        private EntityQuery cars;
        private DestinationRoutingSystem routing;
        private EndFrameBarrier barrier;
        protected override void OnCreate()
        {
            base.OnCreate(); routing = World.GetOrCreateSystemManaged<DestinationRoutingSystem>(); barrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            cars = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<GeneratedTraffic>(), ComponentType.ReadOnly<CarCurrentLane>(), ComponentType.ReadOnly<Target>(), ComponentType.ReadOnly<PathOwner>() }, None = new[] { ComponentType.ReadOnly<Unspawned>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>() } });
        }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;
        protected override void OnUpdate()
        {
            if (!Mod.Active) return;
            var commands = barrier.CreateCommandBuffer();
            using (var entities = cars.ToEntityArray(Allocator.Temp))
                foreach (Entity e in entities)
                {
                    Entity target = EntityManager.GetComponentData<Target>(e).m_Target;
                    if (!routing.IsSink(target)) continue;
                    var path = EntityManager.GetComponentData<PathOwner>(e);
                    if ((path.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Obsolete)) != 0) continue;
                    if (!VehicleUtils.PathEndReached(EntityManager.GetComponentData<CarCurrentLane>(e))) continue;
                    VehicleUtils.DeleteVehicle(commands, e, EntityManager.HasBuffer<LayoutElement>(e) ? EntityManager.GetBuffer<LayoutElement>(e, true) : default);
                }
        }
    }
}
