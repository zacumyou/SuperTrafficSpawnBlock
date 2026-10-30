using Game;
using Game.Rendering;
using Game.UI.InGame;
using Unity.Mathematics;
using UnityEngine;

namespace SuperTrafficSpawnBlock
{
    public partial class SinkRangeOverlaySystem : GameSystemBase
    {
        private SelectedInfoUISystem selection;
        private DestinationRoutingSystem routing;
        private OverlayRenderSystem overlay;
        protected override void OnCreate()
        {
            base.OnCreate();
            selection = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            routing = World.GetOrCreateSystemManaged<DestinationRoutingSystem>();
            overlay = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
        }
        protected override void OnUpdate()
        {
            var sink = selection.selectedEntity;
            if (!Mod.Active || !routing.IsSink(sink) || !BlockServiceSystem.IsOn(EntityManager, sink) || !EntityManager.HasComponent<Game.Objects.Transform>(sink)) return;
            int radius = SinkRange.Get(EntityManager, sink);
            if (radius == 0) return; // Native arrival has no distance-based radius.
            float3 center = EntityManager.GetComponentData<Game.Objects.Transform>(sink).m_Position;
            var buffer = overlay.GetBuffer(out var writers);
            writers.Complete();
            // Native projected circles form a dotted ring; no persistent scene objects to clean up.
            int count = (int)math.ceil(2f * math.PI * radius / 1.4f);
            var color = new Color(0.2f, 0.7f, 1f, 0.5f);
            for (int i = 0; i < count; i++)
            {
                float angle = i * (2f * math.PI / count);
                float3 point = center + new float3(math.cos(angle) * radius, 0, math.sin(angle) * radius);
                buffer.DrawCircle(color, point, 0.55f);
            }
        }
    }
}
