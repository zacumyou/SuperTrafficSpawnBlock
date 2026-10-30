using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SuperTrafficSpawnBlock
{
    // Delay only this mod's prepared cars, immediately before native TripSource removal.
    // No vehicle deletion, teleport, private lane alteration, or new serialized component.
    public partial class SpawnClearanceSystem : GameSystemBase
    {
        private EntityQuery waiting;
        private BlockPrefabSystem blocks;
        private Game.Objects.SearchSystem search;
        private VehicleSpawnSystem native;
        private uint tick;
        private readonly HashSet<Entity> blocked = new HashSet<Entity>();
        private readonly HashSet<Entity> live = new HashSet<Entity>();
        private readonly Dictionary<Entity, uint> arrived = new Dictionary<Entity, uint>();
        private readonly List<Entity> due = new List<Entity>(), remove = new List<Entity>();
        private readonly List<SpawnFootprint> footprint = new List<SpawnFootprint>();
        private sealed class Admission
        {
            internal Entity Car;
            internal uint Until;
            internal SpawnFootprint[] Space;
        }
        private readonly List<Admission> admitted = new List<Admission>();
        internal bool IsBlocked(Entity source) => blocked.Contains(source);
        private struct Nearby : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            internal Bounds3 Bounds;
            internal NativeList<Entity> Entities;
            public bool Intersect(QuadTreeBoundsXZ bounds) => MathUtils.Intersect(bounds.m_Bounds.xz, Bounds.xz);
            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity) { if (Intersect(bounds)) Entities.Add(entity); }
        }
        protected override void OnCreate()
        {
            base.OnCreate();
            blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            search = World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            native = World.GetOrCreateSystemManaged<VehicleSpawnSystem>();
            waiting = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Car>(), ComponentType.ReadWrite<TripSource>(), ComponentType.ReadOnly<Transform>(), ComponentType.ReadOnly<PrefabRef>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>() }
            });
        }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => native.GetUpdateInterval(phase);
        public override int GetUpdateOffset(SystemUpdatePhase phase) => native.GetUpdateOffset(phase);
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            blocked.Clear(); arrived.Clear(); admitted.Clear(); tick = 0;
        }
        private bool Alive(Entity entity) => EntityManager.Exists(entity) && !EntityManager.HasComponent<Deleted>(entity) && !EntityManager.HasComponent<Destroyed>(entity);
        protected override void OnUpdate()
        {
            blocked.Clear(); live.Clear(); due.Clear(); tick++;
            if (!Mod.Active || !blocks.Ready || !native.Enabled) return;
            // Keep reservations through native release and delayed search-tree registration.
            admitted.RemoveAll(a => !Alive(a.Car) || (tick > a.Until && !EntityManager.HasComponent<Unspawned>(a.Car)));
            using (var cars = waiting.ToEntityArray(Allocator.Temp))
                foreach (Entity car in cars)
                {
                    var trip = EntityManager.GetComponentData<TripSource>(car);
                    if (!EntityManager.HasComponent<PrefabRef>(trip.m_Source) || blocks.FactorFor(EntityManager.GetComponentData<PrefabRef>(trip.m_Source).m_Prefab) == 0) continue;
                    if (EntityManager.HasComponent<Controller>(car))
                    {
                        Entity controller = EntityManager.GetComponentData<Controller>(car).m_Controller;
                        if (controller != Entity.Null && controller != car) continue;
                    }
                    live.Add(car);
                    if (!arrived.ContainsKey(car)) arrived[car] = tick;
                    if (trip.m_Timer <= 0) due.Add(car);
                }
            remove.Clear();
            foreach (var item in arrived) if (!live.Contains(item.Key)) remove.Add(item.Key);
            foreach (Entity car in remove) arrived.Remove(car);
            if (due.Count == 0) return;
            due.Sort((a, b) => { int order = arrived[a].CompareTo(arrived[b]); return order != 0 ? order : a.Index.CompareTo(b.Index); });
            var moving = search.GetMovingSearchTree(true, out var movingDependency);
            var stationary = search.GetStaticSearchTree(true, out var staticDependency);
            movingDependency.Complete(); staticDependency.Complete();
            using (var nearby = new NativeList<Entity>(32, Allocator.Temp))
                foreach (Entity car in due)
                {
                    var trip = EntityManager.GetComponentData<TripSource>(car);
                    footprint.Clear();
                    bool clear = BlockServiceSystem.IsOn(EntityManager, trip.m_Source) && AddFootprints(car);
                    if (clear)
                        foreach (var space in footprint)
                        {
                            // Pending vehicles are invisible: reserve admitted space across all source blocks.
                            foreach (var reservation in admitted)
                            {
                                if (reservation.Car == car) continue;
                                foreach (var occupied in reservation.Space)
                                    if (space.Overlaps(occupied)) { clear = false; break; }
                                if (!clear) break;
                            }
                            if (!clear) break;
                            nearby.Clear();
                            var iterator = new Nearby { Bounds = space.SearchBounds(), Entities = nearby };
                            moving.Iterate(ref iterator); stationary.Iterate(ref iterator);
                            foreach (Entity other in nearby)
                            {
                                if (other == car || !Alive(other) || !EntityManager.HasComponent<Vehicle>(other) ||
                                    EntityManager.HasComponent<Temp>(other) || EntityManager.HasComponent<Unspawned>(other) ||
                                    EntityManager.HasComponent<TripSource>(other)) continue;
                                if (TryFootprint(other, false, out var occupied) && space.Overlaps(occupied)) { clear = false; break; }
                            }
                            if (!clear) break;
                        }
                    if (!clear)
                    {
                        // Native grouping tests <= 0 BEFORE subtracting its 16-frame interval.
                        trip.m_Timer = native.GetUpdateInterval(SystemUpdatePhase.GameSimulation);
                        EntityManager.SetComponentData(car, trip);
                        blocked.Add(trip.m_Source);
                    }
                    else admitted.Add(new Admission { Car = car, Until = tick + 2, Space = footprint.ToArray() });
                }
        }
        private bool TryFootprint(Entity car, bool clearance, out SpawnFootprint space)
        {
            space = default;
            if (!EntityManager.HasComponent<Transform>(car) || !EntityManager.HasComponent<PrefabRef>(car)) return false;
            Entity prefab = EntityManager.GetComponentData<PrefabRef>(car).m_Prefab;
            if (!EntityManager.HasComponent<ObjectGeometryData>(prefab)) return false;
            var transform = EntityManager.GetComponentData<Transform>(car);
            space = SpawnFootprint.Create(transform.m_Position, transform.m_Rotation, EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds, clearance);
            return true;
        }
        private bool AddFootprints(Entity car)
        {
            if (EntityManager.HasComponent<Created>(car) || !TryFootprint(car, true, out var space)) return false;
            footprint.Add(space);
            if (EntityManager.HasBuffer<LayoutElement>(car))
                foreach (var part in EntityManager.GetBuffer<LayoutElement>(car, true))
                {
                    if (part.m_Vehicle == car) continue;
                    if (!TryFootprint(part.m_Vehicle, true, out var trailer)) return false;
                    footprint.Add(trailer);
                }
            // Some source paths begin inside the block; also require room at the first road entry.
            if (!EntityManager.HasBuffer<PathElement>(car) || !EntityManager.HasComponent<PathOwner>(car)) return false;
            var owner = EntityManager.GetComponentData<PathOwner>(car);
            if ((owner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Obsolete)) != 0) return false;
            var path = EntityManager.GetBuffer<PathElement>(car, true);
            for (int i = math.max(0, owner.m_ElementIndex); i < math.min(path.Length, math.max(0, owner.m_ElementIndex) + 8); i++)
            {
                var element = path[i];
                if (!EntityManager.HasComponent<Game.Net.CarLane>(element.m_Target) || !EntityManager.HasComponent<Curve>(element.m_Target)) continue;
                var curve = EntityManager.GetComponentData<Curve>(element.m_Target).m_Bezier;
                float3 forward = MathUtils.Tangent(curve, element.m_TargetDelta.x);
                if (element.m_TargetDelta.y < element.m_TargetDelta.x) forward = -forward;
                var geometry = EntityManager.GetComponentData<ObjectGeometryData>(EntityManager.GetComponentData<PrefabRef>(car).m_Prefab);
                footprint.Add(SpawnFootprint.Create(MathUtils.Position(curve, element.m_TargetDelta.x), quaternion.LookRotationSafe(forward, math.up()), geometry.m_Bounds, true));
                return true;
            }
            return false;
        }
    }
}
