using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using NetCarFlags = Game.Net.CarLaneFlags;
using Edge = Game.Net.Edge;
using SubLane = Game.Net.SubLane;

namespace SuperTrafficSpawnBlock
{
    // Operates once on newly created vehicles, before native initialization chooses a spawn transform.
    // Only each vehicle's private path is changed; road lanes and the shared request path stay intact.
    public partial class DepartureLaneSystem : GameSystemBase
    {
        private EntityQuery freshCars;
        private BlockPrefabSystem registration;
        private bool failed;
        private bool logged;
        private readonly ConnectedLaneSelector<(Entity, Entity), Entity, PathNode> selector =
            new ConnectedLaneSelector<(Entity, Entity), Entity, PathNode>();
        private readonly List<LaneExit<Entity, PathNode>> candidates = new List<LaneExit<Entity, PathNode>>();
        private readonly HashSet<PathNode> nextStarts = new HashSet<PathNode>();

        protected override void OnCreate()
        {
            base.OnCreate();
            registration = World.GetOrCreateSystemManaged<BlockPrefabSystem>();

            freshCars = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Created>(), ComponentType.ReadOnly<Unspawned>(),
                    ComponentType.ReadOnly<Car>(), ComponentType.ReadOnly<TripSource>(), ComponentType.ReadOnly<Owner>(),
                    ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<PathOwner>(), ComponentType.ReadWrite<PathElement>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Destroyed>() }
            });
            RequireForUpdate(freshCars);
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            selector.Clear();
            logged = false;

            failed = false;
        }

        protected override void OnUpdate()
        {
            if (!Mod.Active || failed || !registration.Ready) return;
            try
            {
                using (var cars = freshCars.ToEntityArray(Allocator.Temp))
                {
                    foreach (Entity car in cars)
                    {
                        Entity source = EntityManager.GetComponentData<TripSource>(car).m_Source;
                        if (source != EntityManager.GetComponentData<Owner>(car).m_Owner ||
                            !EntityManager.HasComponent<PrefabRef>(source) ||
                            registration.FactorFor(EntityManager.GetComponentData<PrefabRef>(source).m_Prefab) == 0) continue;
                        Entity model = EntityManager.GetComponentData<PrefabRef>(car).m_Prefab;
                        if (!EntityManager.HasComponent<Game.Prefabs.CarData>(model)) continue;
                        SizeClass size = EntityManager.GetComponentData<Game.Prefabs.CarData>(model).m_SizeClass;
                        var owner = EntityManager.GetComponentData<PathOwner>(car);
                        if ((owner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Obsolete)) != 0) continue;
                        var path = EntityManager.GetBuffer<PathElement>(car);
                        int index = owner.m_ElementIndex;
                        if (index < 0) continue;
                        // A direct building-to-road start is supported. Never skip a driveway/connection lane.
                        while (index < path.Length && path[index].m_Target == source) index++;

                        if (index + 1 >= path.Length) continue;
                        var first = path[index];
                        var next = path[index + 1];
                        if (!DepartureRange.IsFullForwardExit(first.m_TargetDelta.x, first.m_TargetDelta.y,
                            next.m_TargetDelta.x, next.m_TargetDelta.y)) continue;
                        if (!TryGroup(first.m_Target, out Entity edge, out Entity group, out int min, out int max) ||
                            !EntityManager.HasComponent<Edge>(edge)) continue;
                        candidates.Clear();
                        nextStarts.Clear();
                        CollectNextStarts(next.m_Target, size);
                        if (nextStarts.Count == 0) continue;
                        var lanes = EntityManager.GetBuffer<SubLane>(edge, true);
                        for (int i = min; i <= max; i++)
                        {
                            Entity lane = lanes[i].m_SubLane;
                            if (!CanDrive(lane, size) || !EntityManager.HasComponent<SlaveLane>(lane)) continue;
                            var slave = EntityManager.GetComponentData<SlaveLane>(lane);
                            if ((slave.m_Flags & (SlaveLaneFlags.StartingLane | SlaveLaneFlags.EndingLane | SlaveLaneFlags.MergingLane)) != 0) continue;
                            var master = EntityManager.GetComponentData<MasterLane>(group);
                            if (slave.m_Group != master.m_Group || !EntityManager.HasComponent<Owner>(lane) ||
                                EntityManager.GetComponentData<Owner>(lane).m_Owner != edge) continue;
                            // The native master/slave group defines interchangeable lanes in one direction.
                            candidates.Add(new LaneExit<Entity, PathNode>(lane, EntityManager.GetComponentData<Lane>(lane).m_EndNode));
                        }
                        if (!selector.TryChoose((source, group), candidates, nextStarts, out Entity chosen)) continue;
                        first.m_Target = chosen;
                        path[index] = first;
                        if (!logged)
                        {
                            logged = true;
                            Mod.Log.Info("Departure lane distribution active: connected same-direction road lanes; private vehicle path before native initialization.");
                        }
                    }
                }
            }
            catch (Exception error)
            {
                failed = true;
                Mod.Log.Error(error, "Departure lane distribution stopped; native spawning remains active.");
            }
        }

        private bool TryGroup(Entity lane, out Entity edge, out Entity masterEntity, out int min, out int max)
        {
            edge = masterEntity = Entity.Null;
            min = max = 0;
            if (!EntityManager.HasComponent<Owner>(lane) || !EntityManager.HasComponent<Game.Net.CarLane>(lane)) return false;
            edge = EntityManager.GetComponentData<Owner>(lane).m_Owner;
            if (!EntityManager.HasBuffer<SubLane>(edge)) return false;
            var lanes = EntityManager.GetBuffer<SubLane>(edge, true);
            masterEntity = lane;
            if (!EntityManager.HasComponent<MasterLane>(masterEntity))
            {
                if (!EntityManager.HasComponent<SlaveLane>(lane)) return false;
                int masterIndex = EntityManager.GetComponentData<SlaveLane>(lane).m_MasterIndex;
                if (masterIndex < 0 || masterIndex >= lanes.Length) return false;
                masterEntity = lanes[masterIndex].m_SubLane;
            }
            if (!EntityManager.HasComponent<MasterLane>(masterEntity)) return false;
            var master = EntityManager.GetComponentData<MasterLane>(masterEntity);
            min = master.m_MinIndex;
            max = master.m_MaxIndex;
            return min >= 0 && max >= min && max < lanes.Length;
        }

        private void CollectNextStarts(Entity next, SizeClass size)
        {
            if (EntityManager.HasComponent<MasterLane>(next) && TryGroup(next, out Entity owner, out _, out int min, out int max))
            {
                var lanes = EntityManager.GetBuffer<SubLane>(owner, true);
                for (int i = min; i <= max; i++)
                {
                    Entity lane = lanes[i].m_SubLane;
                    if (CanDrive(lane, size)) nextStarts.Add(EntityManager.GetComponentData<Lane>(lane).m_StartNode);
                }
            }
            else if (CanDrive(next, size)) nextStarts.Add(EntityManager.GetComponentData<Lane>(next).m_StartNode);
        }

        private bool CanDrive(Entity lane, SizeClass size)
        {
            if (!EntityManager.HasComponent<Lane>(lane) || !EntityManager.HasComponent<Game.Net.CarLane>(lane) ||
                !EntityManager.HasComponent<PrefabRef>(lane) || EntityManager.HasComponent<Deleted>(lane) ||
                EntityManager.HasComponent<Temp>(lane) || EntityManager.HasComponent<MasterLane>(lane)) return false;
            var data = EntityManager.GetComponentData<Game.Net.CarLane>(lane);
            if ((data.m_Flags & (NetCarFlags.Forbidden | NetCarFlags.PublicOnly | NetCarFlags.Twoway | NetCarFlags.Runway)) != 0 ||
                data.m_AccessRestriction != Entity.Null) return false;
            Entity prefab = EntityManager.GetComponentData<PrefabRef>(lane).m_Prefab;
            if (!EntityManager.HasComponent<CarLaneData>(prefab)) return false;
            var types = EntityManager.GetComponentData<CarLaneData>(prefab);
            return (types.m_RoadTypes & RoadTypes.Car) != 0 && size <= types.m_MaxSize;
        }
    }

}


