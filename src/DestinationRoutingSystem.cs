using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    public partial class DestinationRoutingSystem : GameSystemBase
    {
        internal sealed class Route
        {
            internal PathInformation Info;
            internal PathElement[] Path;
            internal RandomTrafficRequest Traffic;
        }
        private sealed class State
        {
            internal Entity Choice, Probe;
            internal Route Route;
            internal uint Retry, Refresh, ExitCursor;
            internal string Status = "FindingRoute";
            internal uint ValidatedTick;
            internal bool ValidatedHeavy;
            internal uint Revision, ProbeRevision;
        }
        private readonly Dictionary<(Entity source, Entity choice), State> states = new Dictionary<(Entity source, Entity choice), State>();
        private readonly HashSet<(Entity source, Entity choice)> live = new HashSet<(Entity source, Entity choice)>();
        private readonly List<(Entity source, Entity choice)> remove = new List<(Entity source, Entity choice)>();
        private readonly List<(Entity source, Entity choice)> due = new List<(Entity source, Entity choice)>();
        private readonly List<Entity> validExits = new List<Entity>();
        private readonly HashSet<Entity> owned = new HashSet<Entity>();
        private int requestCursor;
        private EntityQuery sources, outside, probes;
        private BlockPrefabSystem blocks;
        private PathfindSetupSystem setup;
        private uint tick;
        private Unity.Mathematics.Random random;
        protected override void OnCreate()
        {
            base.OnCreate();
            blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            setup = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            sources = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Game.Buildings.TrafficSpawner>(), ComponentType.ReadOnly<PrefabRef>() }, None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>() } });
            outside = GetEntityQuery(ComponentType.ReadOnly<Game.Objects.OutsideConnection>(), ComponentType.ReadOnly<Game.Objects.Transform>(), ComponentType.ReadOnly<PrefabRef>());
            probes = GetEntityQuery(ComponentType.ReadOnly<RouteProbe>(), ComponentType.ReadOnly<PathInformation>());
            random = new Unity.Mathematics.Random(0x61a743u);
        }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 256;
        public override int GetUpdateOffset(SystemUpdatePhase phase) => 32;
        protected override void OnGamePreload(Purpose purpose, GameMode mode) { states.Clear(); tick = 0; requestCursor = 0; }
        internal void InvalidateBlock(Entity block)
        {
            foreach (var pair in states)
            {
                if (pair.Key.source != block && pair.Key.choice != block) continue;
                var state = pair.Value;
                state.Route = null;
                state.Retry = state.Refresh = 0;
                state.Revision++;
                // Leave pending probes alive until their pathfinding job completes.
            }
        }
        internal bool Alive(Entity e) => EntityManager.Exists(e) && !EntityManager.HasComponent<Deleted>(e) && !EntityManager.HasComponent<Destroyed>(e) && !EntityManager.HasComponent<Temp>(e);
        internal bool IsSink(Entity e) => Alive(e) && EntityManager.HasComponent<PrefabRef>(e) && EntityManager.GetComponentData<PrefabRef>(e).m_Prefab == blocks.SinkPrefab;
        internal void EnsureTargets(Entity source)
        {
            if (EntityManager.HasBuffer<DestinationTarget>(source)) return;
            Entity legacy = EntityManager.HasComponent<DestinationChoice>(source) ? EntityManager.GetComponentData<DestinationChoice>(source).Sink : Entity.Null;
            var targets = EntityManager.AddBuffer<DestinationTarget>(source);
            if (IsSink(legacy)) targets.Add(new DestinationTarget { Sink = legacy });
            // Retain the old serialized type for reading previous saves, but migrate each instance once.
            if (EntityManager.HasComponent<DestinationChoice>(source)) EntityManager.RemoveComponent<DestinationChoice>(source);
        }
        internal List<Entity> Choices(Entity source)
        {
            var result = new List<Entity>();
            if (EntityManager.HasBuffer<DestinationTarget>(source))
            {
                var targets = EntityManager.GetBuffer<DestinationTarget>(source, true);
                foreach (var target in targets)
                    if ((target.Sink == Entity.Null || IsSink(target.Sink)) && !result.Contains(target.Sink)) result.Add(target.Sink);
            }
            else if (EntityManager.HasComponent<DestinationChoice>(source))
            {
                var legacy = EntityManager.GetComponentData<DestinationChoice>(source).Sink;
                if (IsSink(legacy)) result.Add(legacy);
            }
            if (result.Count == 0) result.Add(Entity.Null);
            return result;
        }
        internal List<Route> GetRoutes(Entity source)
        {
            var result = new List<Route>();
            foreach (Entity choice in Choices(source))
            {
                if (choice != Entity.Null && !BlockServiceSystem.IsOn(EntityManager, choice)) continue;
                if (!states.TryGetValue((source, choice), out var state) || state.Route == null || !Validate(state, choice)) return null;
                result.Add(state.Route);
            }
            return result.Count == 0 ? null : result; // Keep disabled targets saved; resume their share when enabled.
        }
        private bool Validate(State state, Entity choice)
        {
            if (state.ValidatedTick == tick && state.ValidatedHeavy == Mod.Options.AllowHeavyVehicles) return state.Route != null;
            state.ValidatedTick = tick;
            state.ValidatedHeavy = Mod.Options.AllowHeavyVehicles;
            if (state.Route != null && !Valid(state.Route, choice)) state.Route = null;
            return state.Route != null;
        }
        private bool Valid(Route route, Entity choice)
        {
            if (!DestinationRules.Matches(choice, route.Info.m_Destination, Entity.Null, EntityManager.HasComponent<Game.Objects.OutsideConnection>(route.Info.m_Destination), Alive(route.Info.m_Destination))) return false;
            if (!Mod.Options.AllowHeavyVehicles && route.Traffic.m_SizeClass >= SizeClass.Large) return false;
            foreach (var p in route.Path) if (!Alive(p.m_Target) || EntityManager.HasComponent<Updated>(p.m_Target)) return false;
            return true;
        }
        protected override void OnUpdate()
        {
            if (!Mod.Active || !blocks.Ready) return;
            tick++; live.Clear(); due.Clear(); validExits.Clear();
            using (var all = sources.ToEntityArray(Allocator.Temp))
            using (var exits = outside.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity exit in exits)
                {
                    if (!Alive(exit)) continue;
                    Entity prefab = EntityManager.GetComponentData<PrefabRef>(exit).m_Prefab;
                    if (EntityManager.HasComponent<TrafficSpawnerData>(prefab) && (EntityManager.GetComponentData<TrafficSpawnerData>(prefab).m_RoadType & RoadTypes.Car) != 0) validExits.Add(exit);
                }
                foreach (Entity source in all)
                {
                    if (blocks.FactorFor(EntityManager.GetComponentData<PrefabRef>(source).m_Prefab) == 0) continue;
                    EnsureTargets(source);
                    // Forget demolished targets in the saved buffer; an empty selection means outside.
                    var targets = EntityManager.GetBuffer<DestinationTarget>(source);
                    for (int i = targets.Length - 1; i >= 0; i--)
                        if (targets[i].Sink != Entity.Null && !IsSink(targets[i].Sink)) targets.RemoveAt(i);
                    foreach (Entity choice in Choices(source))
                    {
                    var key = (source, choice);
                    live.Add(key);
                    if (!states.TryGetValue(key, out var state)) states[key] = state = new State { Choice = choice };
                    // Pending jobs own the probe until completion; never destroy it while pathfinding.
                    if (state.Probe != Entity.Null && EntityManager.Exists(state.Probe))
                    {
                        var info = EntityManager.GetComponentData<PathInformation>(state.Probe);
                        if ((info.m_State & PathFlags.Pending) != 0) continue;
                        if (state.ProbeRevision == state.Revision && info.m_Origin == source && (info.m_State & (PathFlags.Failed | PathFlags.Obsolete)) == 0)
                        {
                            var buffer = EntityManager.GetBuffer<PathElement>(state.Probe, true);
                            var path = new PathElement[buffer.Length];
                            for (int i = 0; i < path.Length; i++) path[i] = buffer[i];
                            var route = new Route { Info = info, Path = path, Traffic = EntityManager.GetComponentData<RandomTrafficRequest>(state.Probe) };
                            if (path.Length > 0 && Valid(route, choice)) { state.Route = route; state.Refresh = tick + 16; }
                        }
                        EntityManager.DestroyEntity(state.Probe); state.Probe = Entity.Null;
                        state.Retry = tick + (state.Route == null ? 2u : 1u);
                    }
                    Validate(state, choice);
                    state.Status = state.Route == null ? "NoRoute" : "RouteReady";
                    if (tick < state.Retry || (state.Route != null && tick < state.Refresh)) continue;
                    due.Add(key);
                    }
                }
            }
            // Rotate due requests, with a global per-window and pending-job bound.
            int pendingCount = 0;
            using (var pendingProbes = probes.ToComponentDataArray<PathInformation>(Allocator.Temp))
                foreach (var info in pendingProbes) if ((info.m_State & PathFlags.Pending) != 0) pendingCount++;
            int budget = RouteRequestBudget.Available(pendingCount);
            int visited = 0, started = 0;
            while (visited < due.Count && started < budget)
            {
                var key = due[(requestCursor + visited) % due.Count];
                visited++;
                Entity source = key.source, choice = key.choice;
                var state = states[key];
                    Entity destination = choice;
                    if (destination == Entity.Null)
                    {
                        if (validExits.Count == 0) { state.Status = "NoOutside"; continue; }
                        destination = validExits[(int)(state.ExitCursor++ % (uint)validExits.Count)];
                    }
                    var flags = (RandomTrafficRequestFlags)0;
                    int kind = Mod.Options.AllowHeavyVehicles ? random.NextInt(100) : 99;
                    if (kind < 20) flags = RandomTrafficRequestFlags.DeliveryTruck;
                    else if (kind < 25) flags = RandomTrafficRequestFlags.TransportVehicle;
                    SizeClass size = kind < 25 ? SizeClass.Large : SizeClass.Small;
                    var traffic = new RandomTrafficRequest(source, RoadTypes.Car, TrackTypes.None, EnergyTypes.FuelAndElectricity, size, flags);
                    var methods = PathMethod.Road | (size == SizeClass.Small ? PathMethod.MediumRoad : 0);
                    if ((flags & RandomTrafficRequestFlags.DeliveryTruck) != 0) methods |= PathMethod.CargoLoading;
                    var parameters = new PathfindParameters { m_MaxSpeed = 111.111115f, m_WalkSpeed = 5.555556f, m_Weights = new PathfindWeights(1,1,1,1), m_Methods = methods, m_IgnoredRules = RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles | (size == SizeClass.Small ? RuleFlags.ForbidHeavyTraffic : 0) };
                    var start = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Entity = source, m_Methods = methods, m_RoadTypes = RoadTypes.Car };
                    var end = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Entity = destination, m_Methods = methods, m_RoadTypes = RoadTypes.Car };
                    state.Probe = EntityManager.CreateEntity(typeof(PathInformation), typeof(PathElement), typeof(RandomTrafficRequest), typeof(RouteProbe));
                    state.ProbeRevision = state.Revision;
                    EntityManager.SetComponentData(state.Probe, traffic);
                    EntityManager.SetComponentData(state.Probe, new PathInformation { m_State = PathFlags.Pending });
                    setup.GetQueue(this, 64).Enqueue(new SetupQueueItem(state.Probe, parameters, start, end));
                    state.Status = state.Route == null ? "FindingRoute" : "RouteReady";
                started++;
            }
            if (due.Count > 0) requestCursor = (requestCursor + visited) % due.Count;
            remove.Clear();
            foreach (var pair in states)
            {
                if (live.Contains(pair.Key)) continue;
                var probe = pair.Value.Probe;
                if (EntityManager.Exists(probe))
                {
                    if ((EntityManager.GetComponentData<PathInformation>(probe).m_State & PathFlags.Pending) != 0) continue;
                    EntityManager.DestroyEntity(probe);
                }
                remove.Add(pair.Key);
            }
            foreach (var key in remove) states.Remove(key);
            // Serialized in-flight probes have no managed cache after loading. Reclaim only after pending work ends.
            owned.Clear();
            foreach (var state in states.Values) if (state.Probe != Entity.Null) owned.Add(state.Probe);
            using (var entities = probes.ToEntityArray(Allocator.Temp))
                foreach (Entity probe in entities)
                    if (!owned.Contains(probe) && (EntityManager.GetComponentData<PathInformation>(probe).m_State & PathFlags.Pending) == 0) EntityManager.DestroyEntity(probe);
        }
    }
}








