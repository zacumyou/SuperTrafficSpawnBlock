using System;
using System.Collections.Generic;
using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace SuperTrafficSpawnBlock
{
    public partial class BlockPrefabSystem : GameSystemBase
    {
        private EntityQuery candidates, categories;
        private PrefabSystem prefabs;
        private readonly Dictionary<Entity, int> factors = new Dictionary<Entity, int>();
        internal const string SinkName = "Super Traffic Sink Block";
        internal Entity SinkPrefab { get; private set; }
        private bool registered;
        private EntityQuery visualCandidates, newVisualCandidates;
        private BuildingPrefab inputVisual, outputVisual;
        private bool searchedVisuals, refreshEntities;
        private readonly Dictionary<BuildingPrefab, int> blocks = new Dictionary<BuildingPrefab, int>();
        private readonly HashSet<BuildingPrefab> skinned = new HashSet<BuildingPrefab>();
        internal bool Ready => registered;
        internal int RateFor(Entity source)
        {
            if (!EntityManager.HasComponent<PrefabRef>(source)) return 0;
            int legacy = FactorFor(EntityManager.GetComponentData<PrefabRef>(source).m_Prefab);
            if (legacy == 0) return 0;
            int saved = EntityManager.HasComponent<SpawnRate>(source) ? EntityManager.GetComponentData<SpawnRate>(source).Factor : legacy;
            return RateSchedule<Entity>.Allowed(saved) ? saved : 2;
        }
        internal int FactorFor(Entity prefab) => factors.TryGetValue(prefab, out int factor) ? factor : 0;
        protected override void OnCreate()
        {
            base.OnCreate();
            prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            candidates = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<TrafficSpawnerData>(), ComponentType.Exclude<Deleted>());
            categories = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<UIAssetCategoryData>());
            visualCandidates = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<BuildingData>(), ComponentType.Exclude<Deleted>());
            newVisualCandidates = GetEntityQuery(ComponentType.ReadOnly<PrefabData>(), ComponentType.ReadOnly<BuildingData>(), ComponentType.ReadOnly<Created>(), ComponentType.Exclude<Deleted>());
            RequireForUpdate(candidates);
        }
        protected override void OnUpdate()
        {
            if (!Mod.Active) return;
            try
            {
                if (refreshEntities)
                {
                    // UpdatePrefab replaces the prefab entity; refresh our lookup after that pass.
                    factors.Clear();
                    foreach (var entry in blocks)
                        if (entry.Value == 0) SinkPrefab = prefabs.GetEntity(entry.Key);
                        else factors[prefabs.GetEntity(entry.Key)] = entry.Value;
                    refreshEntities = false;
                }
                FindVisuals();
                if (registered)
                {
                    foreach (var entry in blocks)
                        if (ApplyVisual(entry.Key, entry.Value))
                        {
                            prefabs.UpdatePrefab(entry.Key);
                            refreshEntities = true;
                        }
                    if (!refreshEntities && skinned.Count == blocks.Count) Enabled = false;
                    return;
                }
                BuildingPrefab source = null;
                using (var entities = candidates.ToEntityArray(Allocator.Temp))
                    foreach (Entity entity in entities)
                    {
                        // Native TryGetPrefab<T> may return true with a null typed prefab.
                        if (!prefabs.TryGetPrefab<BuildingPrefab>(entity, out var candidate) || candidate == null) continue;
                        if (candidate.name != "Traffic Spawner" || !candidate.isBuiltin) continue;
                        if (!candidate.TryGet<Game.Prefabs.TrafficSpawner>(out var traffic) || traffic == null || traffic.m_RoadType != RoadTypes.Car || traffic.m_TrackType != TrackTypes.None) continue;
                        source = candidate;
                        break;
                    }
                if (source == null) return;
                UIAssetCategoryPrefab maintenance = null;
                using (var entities = categories.ToEntityArray(Allocator.Temp))
                    foreach (Entity entity in entities)
                    {
                        if (!prefabs.TryGetPrefab<UIAssetCategoryPrefab>(entity, out var category) || category == null || category.m_Menu == null) continue;
                        if (category.name.Equals("RoadsServices", StringComparison.Ordinal) &&
                            category.m_Menu.name.Equals("Roads", StringComparison.OrdinalIgnoreCase))
                        { maintenance = category; break; }
                    }
                if (maintenance == null) return; // Wait until the native Roads category is initialized.
                foreach (int factor in new[] { 1, 2, 5, 10, 20, 50, 0 })
                {
                    string name = factor == 0 ? SinkName : BlockVariants.PrefabName(factor);
                    if (prefabs.TryGetPrefab(new PrefabID(nameof(BuildingPrefab), name), out var existing) && existing != null)
                    {
                        if (existing is BuildingPrefab building) blocks[building] = factor;
                        if (factor == 0) SinkPrefab = prefabs.GetEntity(existing);
                        else factors[prefabs.GetEntity(existing)] = factor;
                        continue;
                    }
                    var clone = (BuildingPrefab)source.Clone(name);
                    clone.asset = null;
                    clone.Remove<ObsoleteIdentifiers>();
                    clone.Remove<DestructibleObject>();
                    clone.GetComponent<Game.Prefabs.TrafficSpawner>().m_SpawnRate = 0;
                    // Native building lots use 8 m cells; keep the 4 m model at its imported scale.
                    clone.m_LotWidth = 1;
                    clone.m_LotDepth = 1;
                    var ui = clone.AddOrGetComponent<UIObject>();
                    ui.m_IsDebugObject = factor != 0 && factor != 2;
                    ui.m_Group = factor == 0 || factor == 2 ? maintenance : null;
                    ui.m_Priority = factor == 0 ? 9001 : 9000;
                    if (Mod.IconsAvailable) ui.m_Icon = "coui://supertrafficspawnblock/" + (factor == 0 ? "sink.svg" : "source.svg");
                    ApplyVisual(clone, factor);
                    if (!prefabs.AddPrefab(clone)) throw new InvalidOperationException("Could not register " + name);
                    blocks[clone] = factor;
                    if (factor == 0) SinkPrefab = prefabs.GetEntity(clone);
                    else factors[prefabs.GetEntity(clone)] = factor;
                    Mod.Log.Info("Registered " + clone.GetPrefabID() + "; fixed factor=" + factor);
                }
                registered = true;
                Mod.Status = "SuperTrafficSpawnBlock.Status.Ready";
            }
            catch (Exception error) { Mod.Fail(error, "Block registration failed / 블록 등록 실패"); }
        }

        private void FindVisuals()
        {
            if (inputVisual != null && outputVisual != null) return;
            // Scan once, then only newly loaded building prefabs. Custom assets may load later.
            using (var entities = (searchedVisuals ? newVisualCandidates : visualCandidates).ToEntityArray(Allocator.Temp))
                foreach (var entity in entities)
                {
                    if (!prefabs.TryGetPrefab<BuildingPrefab>(entity, out var visual) || visual == null ||
                        visual.m_Meshes == null || visual.m_Meshes.Length == 0) continue;
                    if (visual.name == "CSSuperTrafficSpawnBlockIn") inputVisual = visual;
                    else if (visual.name == "CSSuperTrafficSpawnBlockOut") outputVisual = visual;
                }
            searchedVisuals = true;
        }

        private bool ApplyVisual(BuildingPrefab block, int factor)
        {
            var visual = factor == 0 ? inputVisual : outputVisual;
            if (visual == null || skinned.Contains(block)) return false;
            var meshes = new ObjectMeshInfo[visual.m_Meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = visual.m_Meshes[i];
                if (mesh == null || mesh.m_Mesh == null) return false;
                meshes[i] = new ObjectMeshInfo { m_Mesh = mesh.m_Mesh, m_Position = mesh.m_Position,
                    m_Rotation = mesh.m_Rotation, m_RequireState = mesh.m_RequireState };
            }
            block.m_Meshes = meshes;
            block.m_LotWidth = 1;
            block.m_LotDepth = 1;
            if (visual.TryGet<UIObject>(out var icon) && icon != null && !string.IsNullOrEmpty(icon.m_Icon))
                block.AddOrGetComponent<UIObject>().m_Icon = icon.m_Icon;
            skinned.Add(block);
            Mod.Log.Info("Custom appearance: " + block.name + " <- " + visual.name);
            return true;
        }
    }
}






