using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Notifications;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Transform = Game.Objects.Transform;
using SubLane = Game.Net.SubLane;

namespace SuperTrafficSpawnBlock
{
    // Persist ownership, so a saved fallback is rechecked after loading, not mistaken for a native connection.
    public struct AdjacentRoadConnection : IComponentData, ISerializable
    {
        public Entity Road;
        public void Serialize<T>(T writer) where T : IWriter { writer.Write(Road); }
        public void Deserialize<T>(T reader) where T : IReader { reader.Read(out Road); }
    }

    public partial class AdjacentRoadSystem : GameSystemBase
    {
        private EntityQuery blocks;
        private BlockPrefabSystem registration;
        private Game.Net.SearchSystem search;
        private DestinationRoutingSystem routing;
        private EndFrameBarrier barrier;
        private IconCommandSystem icons;
        private EntityQuery trafficConfig;
        private EntityQuery savedConnections;
        private UpdateSystem updates;
        private readonly Dictionary<Entity, (Transform transform, Entity road, float position)> previous =
            new Dictionary<Entity, (Transform, Entity, float)>();
        private readonly HashSet<Entity> live = new HashSet<Entity>();
        private readonly List<Entity> remove = new List<Entity>();

        private struct Nearby : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public Bounds3 Bounds;
            public NativeList<Entity> Edges;
            public bool Intersect(QuadTreeBoundsXZ bounds) => MathUtils.Intersect(bounds.m_Bounds.xz, Bounds.xz);
            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                if (Intersect(bounds)) Edges.Add(entity);
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            registration = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            routing = World.GetOrCreateSystemManaged<DestinationRoutingSystem>();
            search = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            barrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            icons = World.GetOrCreateSystemManaged<IconCommandSystem>();
            trafficConfig = GetEntityQuery(ComponentType.ReadOnly<TrafficConfigurationData>());
            savedConnections = GetEntityQuery(ComponentType.ReadOnly<AdjacentRoadConnection>(), ComponentType.ReadOnly<Building>(), ComponentType.Exclude<Deleted>());
            updates = World.GetOrCreateSystemManaged<UpdateSystem>();
            blocks = GetEntityQuery(new EntityQueryDesc {
                All = new[] { ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<Transform>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Buildings.TrafficSpawner>() },
                None = new[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>() }
            });
        }
        public override int GetUpdateInterval(SystemUpdatePhase phase) => phase == SystemUpdatePhase.Deserialize ? 1 : 64;
        public override int GetUpdateOffset(SystemUpdatePhase phase) => phase == SystemUpdatePhase.Deserialize ? 0 : 32;
        protected override void OnGamePreload(Purpose purpose, GameMode mode) { previous.Clear(); }
        private bool Alive(Entity entity) => EntityManager.Exists(entity) && !EntityManager.HasComponent<Deleted>(entity) &&
            !EntityManager.HasComponent<Destroyed>(entity) && !EntityManager.HasComponent<Temp>(entity);

        protected override void OnUpdate()
        {
            if (updates.currentPhase == SystemUpdatePhase.Deserialize)
            {
                // Native ConnectedBuildingSystem indexes this buffer without checking it.
                // Usually IEmptySerializable restores it; repair before the native rebuild if absent.
                using (var restored = savedConnections.ToEntityArray(Allocator.Temp))
                    foreach (Entity block in restored)
                    {
                        Entity road = EntityManager.GetComponentData<Building>(block).m_RoadEdge;
                        if (road != Entity.Null && EntityManager.Exists(road) && !EntityManager.HasBuffer<ConnectedBuilding>(road))
                            EntityManager.AddBuffer<ConnectedBuilding>(road);
                    }
                return;
            }
            if (!Mod.Active || !registration.Ready) return;
            var tree = search.GetNetSearchTree(true, out var dependency);
            dependency.Complete();
            live.Clear();
            using (var entities = blocks.ToEntityArray(Allocator.Temp))
            using (var candidates = new NativeList<Entity>(32, Allocator.Temp))
            {
                foreach (Entity block in entities)
                {
                    Entity prefab = EntityManager.GetComponentData<PrefabRef>(block).m_Prefab;
                    if (prefab != registration.SinkPrefab && registration.FactorFor(prefab) == 0) continue;
                    if (!EntityManager.HasComponent<BuildingData>(prefab)) continue;
                    live.Add(block);
                    var building = EntityManager.GetComponentData<Building>(block);
                    var transform = EntityManager.GetComponentData<Transform>(block);
                    bool owned = EntityManager.HasComponent<AdjacentRoadConnection>(block);
                    if (owned && EntityManager.GetComponentData<AdjacentRoadConnection>(block).Road != building.m_RoadEdge)
                    {
                        // A native relocation/reconnection wins. Only replace a now-missing connection.
                        EntityManager.RemoveComponent<AdjacentRoadConnection>(block);
                        owned = false;
                    }
                    if (owned || building.m_RoadEdge == Entity.Null || !Alive(building.m_RoadEdge))
                    {
                        var data = EntityManager.GetComponentData<BuildingData>(prefab);
                        float3 front = BuildingUtils.CalculateFrontPosition(transform, data.m_LotSize.y);
                        candidates.Clear();
                        var iterator = new Nearby { Bounds = new Bounds3(front - 12f, front + 12f), Edges = candidates };
                        tree.Iterate(ref iterator);
                        Entity best = Entity.Null;
                        float bestDistance = float.MaxValue, bestPosition = 0;
                        foreach (Entity edge in candidates)
                        {
                            if (TryRoad(edge, transform, front, out float distance, out float position) && distance < bestDistance)
                            {
                                best = edge; bestDistance = distance; bestPosition = position;
                            }
                        }
                        if (best != building.m_RoadEdge || (best != Entity.Null && math.abs(bestPosition - building.m_CurvePosition) > 0.0001f))
                        {
                            SetConnection(block, ref building, best, bestPosition);
                            Mod.Log.Debug($"Adjacent road: block={block}, road={best}");
                        }
                        if (best != Entity.Null)
                        {
                            EnsureRoadLink(block, best);
                            var connection = new AdjacentRoadConnection { Road = best };
                            if (owned) EntityManager.SetComponentData(block, connection);
                            else EntityManager.AddComponentData(block, connection);
                        }
                        else if (owned) EntityManager.RemoveComponent<AdjacentRoadConnection>(block);
                    }
                    if (!previous.TryGetValue(block, out var old) || old.road != building.m_RoadEdge ||
                        old.position != building.m_CurvePosition || !old.transform.m_Position.Equals(transform.m_Position) ||
                        !old.transform.m_Rotation.Equals(transform.m_Rotation)) routing.InvalidateBlock(block);
                    previous[block] = (transform, building.m_RoadEdge, building.m_CurvePosition);
                }
            }
            remove.Clear();
            foreach (var entry in previous) if (!live.Contains(entry.Key)) remove.Add(entry.Key);
            foreach (var block in remove) previous.Remove(block);
            // Iteration is synchronous; no outstanding tree reader job is retained.
        }

        private bool TryRoad(Entity edge, Transform transform, float3 front, out float distance, out float position)
        {
            distance = float.MaxValue; position = 0;
            if (!Alive(edge) || !EntityManager.HasComponent<Road>(edge) || !EntityManager.HasComponent<Curve>(edge) ||
                !EntityManager.HasComponent<EdgeGeometry>(edge) ||
                !EntityManager.HasBuffer<SubLane>(edge)) return false;
            var curve = EntityManager.GetComponentData<Curve>(edge).m_Bezier;
            MathUtils.Distance(curve.xz, front.xz, out position);
            float3 center = MathUtils.Position(curve, position);
            if (!AdjacentRoadGeometry.FacesRoad(transform.m_Position, center, transform.m_Rotation)) return false;
            var geometry = EntityManager.GetComponentData<EdgeGeometry>(edge);
            AdjacentRoadGeometry.CheckBoundary(geometry.m_Start.m_Left, transform.m_Position, front, center, ref distance);
            AdjacentRoadGeometry.CheckBoundary(geometry.m_Start.m_Right, transform.m_Position, front, center, ref distance);
            AdjacentRoadGeometry.CheckBoundary(geometry.m_End.m_Left, transform.m_Position, front, center, ref distance);
            AdjacentRoadGeometry.CheckBoundary(geometry.m_End.m_Right, transform.m_Position, front, center, ref distance);
            if (distance == float.MaxValue) return false;
            var lanes = EntityManager.GetBuffer<SubLane>(edge, true);
            foreach (var item in lanes)
            {
                Entity lane = item.m_SubLane;
                if (!Alive(lane) || !EntityManager.HasComponent<Game.Net.CarLane>(lane) ||
                    EntityManager.HasComponent<MasterLane>(lane) || !EntityManager.HasComponent<Curve>(lane) ||
                    !EntityManager.HasComponent<Owner>(lane) || EntityManager.GetComponentData<Owner>(lane).m_Owner != edge) continue;
                var laneCurve = EntityManager.GetComponentData<Curve>(lane).m_Bezier;
                MathUtils.Distance(laneCurve.xz, front.xz, out float t);
                if (math.abs(MathUtils.Position(laneCurve, t).y - transform.m_Position.y) <= 3f) return true;
            }
            return false;
        }

        private void SetConnection(Entity block, ref Building building, Entity road, float position)
        {
            Entity old = building.m_RoadEdge;
            if (old != road)
            {
                if (EntityManager.HasBuffer<ConnectedBuilding>(old))
                {
                    var links = EntityManager.GetBuffer<ConnectedBuilding>(old);
                    for (int i = links.Length - 1; i >= 0; i--) if (links[i].m_Building == block) links.RemoveAt(i);
                }
                if (road != Entity.Null) EnsureRoadLink(block, road);
            }
            building.m_RoadEdge = road;
            building.m_CurvePosition = position;
            EntityManager.SetComponentData(block, building);
            if (old != road)
            {
                var commands = barrier.CreateCommandBuffer();
                Entity change = commands.CreateEntity();
                commands.AddComponent<Event>(change);
                commands.AddComponent(change, new RoadConnectionUpdated { m_Building = block, m_Old = old, m_New = road });
                if (!trafficConfig.IsEmptyIgnoreFilter)
                {
                    Entity notification = trafficConfig.GetSingleton<TrafficConfigurationData>().m_RoadConnectionNotification;
                    var iconCommands = icons.CreateCommandBuffer();
                    if (road != Entity.Null) iconCommands.Remove(block, notification);
                    else iconCommands.Add(block, notification, EntityManager.GetComponentData<Transform>(block).m_Position, IconPriority.Warning);
                }
            }
        }

        private void EnsureRoadLink(Entity block, Entity road)
        {
            // Highway RoadPrefab omits this buffer. Add it only on an actually selected road.
            // Do not remove an empty buffer: native connection/deletion and deserialization use it.
            if (!EntityManager.HasBuffer<ConnectedBuilding>(road)) EntityManager.AddBuffer<ConnectedBuilding>(road);
            var links = EntityManager.GetBuffer<ConnectedBuilding>(road);
            foreach (var link in links) if (link.m_Building == block) return;
            links.Add(new ConnectedBuilding(block));
        }
    }
}
