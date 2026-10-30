using System;
using System.Collections.Generic;
using Colossal.UI.Binding;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Game.UI.InGame;
using Unity.Collections;
using Unity.Entities;
namespace SuperTrafficSpawnBlock
{
    public partial class DestinationUI : Game.UI.UISystemBase
    {
        private SelectedInfoUISystem selection;
        private Game.UI.NameSystem names;
        private BlockPrefabSystem blocks;
        private DestinationRoutingSystem routing;
        private EntityQuery sinks;
        private Entity lastSource;
        private float nextRefresh;
        private readonly List<string> tokens = new List<string>(), values = new List<string>();
        private ValueBinding<bool> visible;
        private ValueBinding<string> sourceToken, selected, options;
        private ValueBinding<int> rate;
        private ValueBinding<bool> sinkVisible;
        private ValueBinding<int> sinkRange;
        private ValueBinding<string> locationPin;
        private Entity focusedSink, focusSource;
        public override GameMode gameMode => GameMode.Game;
        private static string Token(Entity e) => e == Entity.Null ? "outside" : e.Index + ":" + e.Version;
        protected override void OnCreate()
        {
            base.OnCreate(); names = World.GetOrCreateSystemManaged<Game.UI.NameSystem>(); selection = World.GetOrCreateSystemManaged<SelectedInfoUISystem>(); blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>(); routing = World.GetOrCreateSystemManaged<DestinationRoutingSystem>();
            selection.AddMiddleSection(World.GetOrCreateSystemManaged<DestinationSection>());
            sinks = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadOnly<Game.Buildings.TrafficSpawner>() }, None = new[] { ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>() } });
            AddBinding(visible = new ValueBinding<bool>(Mod.Id, "destinationVisible", false));
            AddBinding(sourceToken = new ValueBinding<string>(Mod.Id, "source", ""));
            AddBinding(selected = new ValueBinding<string>(Mod.Id, "destinations", "outside"));
            AddBinding(options = new ValueBinding<string>(Mod.Id, "sinks", ""));
            AddBinding(rate = new ValueBinding<int>(Mod.Id, "spawnRate", 2));
            AddBinding(sinkVisible = new ValueBinding<bool>(Mod.Id, "sinkVisible", false));
            AddBinding(sinkRange = new ValueBinding<int>(Mod.Id, "sinkRange", 0));
            AddBinding(new TriggerBinding<string,int>(Mod.Id, "setSinkRange", SetSinkRange));
            AddBinding(new TriggerBinding<string,string,bool>(Mod.Id, "toggleDestination", ToggleDestination));
            AddBinding(new TriggerBinding<string,int>(Mod.Id, "setSpawnRate", SetRate));
            AddBinding(locationPin = new ValueBinding<string>(Mod.Id, "locationPin", ""));
            AddBinding(new TriggerBinding<string,string>(Mod.Id, "locateSink", LocateSink));
        }
        private void LocateSink(string source, string target)
        {
            if (!Mod.Active || !IsSource(selection.selectedEntity) || Token(selection.selectedEntity) != source) return;
            using (var entities = sinks.ToEntityArray(Allocator.Temp))
                foreach (var candidate in entities)
                    if (routing.IsSink(candidate) && Token(candidate) == target)
                    {
                        focusSource = selection.selectedEntity; focusedSink = candidate;
                        selection.Focus(candidate);
                        return;
                    }
        }
        private void UpdatePin()
        {
            if (!Mod.Active || selection.selectedEntity != focusSource || !routing.IsSink(focusedSink) ||
                !EntityManager.HasComponent<Game.Objects.Transform>(focusedSink) || UnityEngine.Camera.main == null)
            {
                focusedSink = Entity.Null; locationPin.Update(""); return;
            }
            var position = EntityManager.GetComponentData<Game.Objects.Transform>(focusedSink).m_Position;
            var screen = UnityEngine.Camera.main.WorldToViewportPoint(new UnityEngine.Vector3(position.x, position.y + 4f, position.z));
            if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) { locationPin.Update(""); return; }
            locationPin.Update(screen.x.ToString("F5", System.Globalization.CultureInfo.InvariantCulture) + "," +
                (1f-screen.y).ToString("F5", System.Globalization.CultureInfo.InvariantCulture) + "|" + Uri.EscapeDataString(names.GetRenderedLabelName(focusedSink)));
        }
        private bool IsSource(Entity e) => routing.Alive(e) && blocks.RateFor(e) != 0;
        private void SetSinkRange(string token, int radius)
        {
            Entity e = selection.selectedEntity;
            if (!Mod.Active || !routing.IsSink(e) || Token(e) != token || !SinkRange.Allowed(radius)) return;
            var data = new SinkRange { Radius = radius };
            if (EntityManager.HasComponent<SinkRange>(e)) EntityManager.SetComponentData(e, data);
            else EntityManager.AddComponentData(e, data);
            nextRefresh = 0;
        }
        private void SetRate(string source, int factor)
        {
            nextRefresh = 0;
            Entity e = selection.selectedEntity;
            if (!IsSource(e) || Token(e) != source || !RateSchedule<Entity>.Allowed(factor)) return;
            var data = new SpawnRate { Factor = factor };
            if (EntityManager.HasComponent<SpawnRate>(e)) EntityManager.SetComponentData(e, data);
            else EntityManager.AddComponentData(e, data);
        }
        private void ToggleDestination(string source, string target, bool enabled)
        {
            nextRefresh = 0;
            Entity e = selection.selectedEntity;
            if (!IsSource(e) || Token(e) != source) return;
            Entity sink = Entity.Null;
            if (target != "outside")
            {
                using (var entities = sinks.ToEntityArray(Allocator.Temp))
                    foreach (Entity candidate in entities) if (routing.IsSink(candidate) && Token(candidate) == target) { sink = candidate; break; }
                if (sink == Entity.Null) return;
            }
            routing.EnsureTargets(e);
            var targets = EntityManager.GetBuffer<DestinationTarget>(e);
            for (int i = targets.Length - 1; i >= 0; i--)
            {
                if (targets[i].Sink != sink) continue;
                if (enabled) return;
                targets.RemoveAt(i);
            }
            if (enabled) targets.Add(new DestinationTarget { Sink = sink });
        }
        protected override void OnUpdate()
        {
            UpdatePin();
            Entity source = selection.selectedEntity;
            bool show = Mod.Active && blocks.Ready && IsSource(source);
            bool showSink = Mod.Active && blocks.Ready && routing.IsSink(source);
            sinkVisible.Update(showSink);
            visible.Update(show);
            if (showSink)
            {
                sourceToken.Update(Token(source));
                sinkRange.Update(SinkRange.Get(EntityManager, source));
                lastSource = Entity.Null;
                return;
            }
            if (!show) { lastSource = Entity.Null; return; }
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (source == lastSource && now < nextRefresh) return;
            lastSource = source; nextRefresh = now + 0.25f;
            sourceToken.Update(Token(source));
            rate.Update(blocks.RateFor(source));
            tokens.Clear();
            foreach (Entity e in routing.Choices(source)) tokens.Add(Token(e));
            selected.Update(string.Join(",", tokens));
            values.Clear();
            using (var entities = sinks.ToEntityArray(Allocator.Temp))
                foreach (Entity candidate in entities) if (routing.IsSink(candidate)) values.Add(Token(candidate) + "|" + Uri.EscapeDataString(names.GetRenderedLabelName(candidate)));
            values.Sort(StringComparer.Ordinal);
            options.Update(string.Join(",", values));
        }
    }
}

