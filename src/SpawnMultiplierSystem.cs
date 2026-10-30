using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;

namespace SuperTrafficSpawnBlock
{
    public partial class SpawnMultiplierSystem : GameSystemBase
    {
        private EntityQuery blocks, waiting;
        private BlockPrefabSystem registration;
        private TrafficSpawnerAISystem native;
        private DestinationRoutingSystem routing;
        private SpawnClearanceSystem clearance;
        private UpdateSystem updates;
        private EndFrameBarrier barrier;
        private EntityArchetype completionArchetype, replayArchetype;
        private readonly Dictionary<Entity, int> pending = new Dictionary<Entity, int>();
        private readonly Dictionary<Entity, int> overdue = new Dictionary<Entity, int>();
        private readonly CongestionGate<Entity> congestion = new CongestionGate<Entity>();
        private readonly List<Entity> input = new List<Entity>();
        private readonly HashSet<Entity> seen = new HashSet<Entity>();
        private readonly HashSet<Entity> live = new HashSet<Entity>();
        private Unity.Mathematics.Random random;
        private readonly RateSchedule<Entity> schedule = new RateSchedule<Entity>();

        protected override void OnCreate()
        {
            base.OnCreate();
            random = new Unity.Mathematics.Random(0x397146bu);
            registration = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            native = World.GetOrCreateSystemManaged<TrafficSpawnerAISystem>();
            routing = World.GetOrCreateSystemManaged<DestinationRoutingSystem>();
            clearance = World.GetOrCreateSystemManaged<SpawnClearanceSystem>();
            updates = World.GetOrCreateSystemManaged<UpdateSystem>();
            barrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            completionArchetype = EntityManager.CreateArchetype(typeof(HandleRequest), typeof(Event));
            // No UpdateFrame/RequestGroup: replay is consumed directly, never independently dispatched.
            replayArchetype = EntityManager.CreateArchetype(typeof(ServiceRequest), typeof(RandomTrafficRequest), typeof(PathInformation), typeof(PathElement));
            blocks = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Game.Buildings.TrafficSpawner>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadWrite<ServiceDispatch>() },
                None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>() }
            });
            waiting = GetEntityQuery(ComponentType.ReadOnly<Car>(), ComponentType.ReadOnly<Unspawned>(), ComponentType.ReadOnly<TripSource>());
            RequireForUpdate(blocks);
        }
        protected override void OnGamePreload(Purpose purpose, GameMode mode) { congestion.Clear(); schedule.Clear(); random = new Unity.Mathematics.Random(0x397146bu); }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => native.GetUpdateInterval(phase);
        public override int GetUpdateOffset(SystemUpdatePhase phase) => native.GetUpdateOffset(phase);
        private bool Alive(Entity entity) => EntityManager.Exists(entity) && !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Destroyed>(entity);
        private bool IsSuperBlock(Entity entity) => EntityManager.HasComponent<PrefabRef>(entity) && registration.FactorFor(EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab) > 0;
        private static bool Heavy(RandomTrafficRequest traffic) => (traffic.m_Flags & (RandomTrafficRequestFlags.DeliveryTruck | RandomTrafficRequestFlags.TransportVehicle)) != 0 || traffic.m_SizeClass >= SizeClass.Large;
        private void Complete(EntityCommandBuffer commands, Entity request)
        {
            if (!seen.Add(request)) return;
            Entity completion = commands.CreateEntity(completionArchetype);
            commands.SetComponent(completion, new HandleRequest(request, Entity.Null, completed: true));
        }
        protected override void OnUpdate()
        {
            if (!Mod.Active || Mod.Options == null || !native.Enabled || !registration.Ready) return;
            try
            {
                bool paced = updates.currentPhase == SystemUpdatePhase.GameSimulation;
                seen.Clear(); live.Clear(); pending.Clear(); overdue.Clear();
                var commands = barrier.CreateCommandBuffer();
                using (var cars = waiting.ToComponentDataArray<TripSource>(Allocator.Temp))
                    foreach (var car in cars)
                    {
                        pending.TryGetValue(car.m_Source, out int n);
                        pending[car.m_Source] = n + 1;
                        if (car.m_Timer <= -SpawnPacing.Window)
                        {
                            overdue.TryGetValue(car.m_Source, out int blocked);
                            overdue[car.m_Source] = blocked + 1;
                        }
                    }
                using (var entities = blocks.ToEntityArray(Allocator.Temp))
                {
                    // Snapshot routes and filter native dispatches before any structural changes.
                    foreach (Entity source in entities)
                    {
                        bool super = IsSuperBlock(source);
                        if (super) live.Add(source);
                        var dispatches = EntityManager.GetBuffer<ServiceDispatch>(source);
                        input.Clear();
                        for (int i = 0; i < dispatches.Length; i++)
                        {
                            Entity request = dispatches[i].m_Request;
                            if (!EntityManager.HasComponent<RandomTrafficRequest>(request) || !EntityManager.HasComponent<PathInformation>(request)) { input.Add(request); continue; }
                            var info = EntityManager.GetComponentData<PathInformation>(request);
                            var traffic = EntityManager.GetComponentData<RandomTrafficRequest>(request);
                            if (TrafficPolicy.Reject(super, IsSuperBlock(info.m_Destination), Heavy(traffic), Mod.Options.AllowHeavyVehicles)) { Complete(commands, request); continue; }
                            if (super || routing.IsSink(source) || routing.IsSink(info.m_Destination)) Complete(commands, request);
                            else input.Add(request);
                        }
                        dispatches.ResizeUninitialized(input.Count);
                        for (int i = 0; i < input.Count; i++) dispatches[i] = new ServiceDispatch(input[i]);
                    }
                    congestion.Retain(live);
                    schedule.Retain(live);
                    if (!paced) return;
                    foreach (Entity source in live)
                    {
                        if (!BlockServiceSystem.IsOn(EntityManager, source) || clearance.IsBlocked(source)) { schedule.Reset(source); continue; }
                        var routes = routing.GetRoutes(source);
                        if (routes == null) { schedule.Reset(source); continue; }
                        pending.TryGetValue(source, out int queued);
                        overdue.TryGetValue(source, out int blocked);
                        int factor = registration.RateFor(source);
                        int capacity = factor == RateSchedule<Entity>.Half ? 1 : factor;
                        if (congestion.ShouldPause(source, capacity, queued, blocked)) { schedule.Reset(source); continue; }
                        int count = SpawnPacing.Budget(schedule.Next(source, factor), queued);
                        if (count == 0) continue;
                        var counts = DestinationAllocation.Allocate(count, routes.Count, n => random.NextInt(n));
                        for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
                        {
                        if (counts[routeIndex] == 0) continue;
                        var route = routes[routeIndex];
                        Entity replay = EntityManager.CreateEntity(replayArchetype);
                        EntityManager.SetComponentData(replay, route.Traffic);
                        EntityManager.SetComponentData(replay, route.Info);
                        var replayPath = EntityManager.GetBuffer<PathElement>(replay);
                        for (int p = 0; p < route.Path.Length; p++) replayPath.Add(route.Path[p]);
                        var dispatches = EntityManager.GetBuffer<ServiceDispatch>(source);
                        for (int i = 0; i < counts[routeIndex]; i++) dispatches.Add(new ServiceDispatch(replay));
                        // Also clean up if native vehicle selection fails. Completion plays after native consumption.
                        Complete(commands, replay);
                        }
                    }

                }
            }
            catch (Exception error) { Mod.Fail(error, "Continuous traffic processing stopped / 연속 차량 생성 중단"); }
        }
    }
}





