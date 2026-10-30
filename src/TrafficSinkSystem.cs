using Game;
using Game.Common;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
                    if (!routing.IsSink(target) || !BlockServiceSystem.IsOn(EntityManager, target)) continue;
                    var path = EntityManager.GetComponentData<PathOwner>(e);
                    if ((path.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Obsolete | PathFlags.Stuck)) != 0) continue;
                    var lane = EntityManager.GetComponentData<CarCurrentLane>(e);
                    if (!VehicleUtils.PathEndReached(lane) && !InAbsorptionRange(e, target, lane, path)) continue;
                    VehicleUtils.DeleteVehicle(commands, e, EntityManager.HasBuffer<LayoutElement>(e) ? EntityManager.GetBuffer<LayoutElement>(e, true) : default);
                }
        }
        private bool BelongsTo(Entity entity, Entity road, Entity sink)
        {
            // Lane -> edge/building; also support one nested lane owner.
            for (int i = 0; i < 3 && entity != Entity.Null; i++)
            {
                if (entity == road || entity == sink) return true;
                if (!EntityManager.HasComponent<Owner>(entity)) return false;
                entity = EntityManager.GetComponentData<Owner>(entity).m_Owner;
            }
            return false;
        }
        private bool InAbsorptionRange(Entity car, Entity sink, CarCurrentLane lane, PathOwner owner)
        {
            int radius = SinkRange.Get(EntityManager, sink);
            if (radius == 0 || !EntityManager.HasComponent<Transform>(car) ||
                !EntityManager.HasComponent<Transform>(sink) || !EntityManager.HasComponent<Game.Buildings.Building>(sink)) return false;
            float3 offset = EntityManager.GetComponentData<Transform>(car).m_Position - EntityManager.GetComponentData<Transform>(sink).m_Position;
            if (math.lengthsq(offset) > radius * radius || math.abs(offset.y) > 4f) return false;
            Entity road = EntityManager.GetComponentData<Game.Buildings.Building>(sink).m_RoadEdge;
            if (road == Entity.Null || !BelongsTo(lane.m_Lane, road, sink) || !EntityManager.HasBuffer<PathElement>(car)) return false;
            // PathOwner advances as lanes are copied into navigation. Check that look-ahead too.
            if (EntityManager.HasBuffer<CarNavigationLane>(car))
            {
                var navigation = EntityManager.GetBuffer<CarNavigationLane>(car, true);
                if (navigation.Length > 16) return false;
                for (int i = 0; i < navigation.Length; i++)
                    if (!BelongsTo(navigation[i].m_Lane, road, sink)) return false;
            }
            var path = EntityManager.GetBuffer<PathElement>(car, true);
            // Only the final approach: reject paths that still leave this road, including U-turn detours.
            // Bound work even on malformed/long paths. Native arrival remains available as fallback.
            if (owner.m_ElementIndex < 0 || owner.m_ElementIndex > path.Length || path.Length - owner.m_ElementIndex > 16) return false;
            for (int i = owner.m_ElementIndex; i < path.Length; i++)
                if (!BelongsTo(path[i].m_Target, road, sink)) return false;
            return true;
        }
    }
}
