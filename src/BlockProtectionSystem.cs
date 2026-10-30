using System;
using Game;
using Game.Common;
using Game.Events;
using Game.Objects;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Event = Game.Common.Event;

namespace SuperTrafficSpawnBlock
{
    // Filter native disaster events before they alter buildings. Never intercept Deleted or tool actions.
    public partial class BlockProtectionSystem : GameSystemBase
    {
        private BlockPrefabSystem blocks;
        private EntityQuery damage, destroy, ignite, weather, submerge;
        protected override void OnCreate()
        {
            base.OnCreate();
            blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            damage = GetEntityQuery(ComponentType.ReadOnly<Event>(), ComponentType.ReadOnly<Damage>());
            destroy = GetEntityQuery(ComponentType.ReadOnly<Event>(), ComponentType.ReadOnly<Destroy>());
            ignite = GetEntityQuery(ComponentType.ReadOnly<Event>(), ComponentType.ReadOnly<Ignite>());
            weather = GetEntityQuery(ComponentType.ReadOnly<Event>(), ComponentType.ReadOnly<FaceWeather>());
            submerge = GetEntityQuery(ComponentType.ReadOnly<Event>(), ComponentType.ReadOnly<Submerge>());
        }
        private bool Protected(Entity target)
        {
            if (!EntityManager.HasComponent<PrefabRef>(target) || EntityManager.HasComponent<Deleted>(target)) return false;
            var prefab = EntityManager.GetComponentData<PrefabRef>(target).m_Prefab;
            return prefab == blocks.SinkPrefab || blocks.FactorFor(prefab) > 0;
        }
        private void Filter<T>(EntityQuery query, Func<T, Entity> target) where T : unmanaged, IComponentData
        {
            using (var events = query.ToEntityArray(Allocator.Temp))
                foreach (var e in events)
                    if (Protected(target(EntityManager.GetComponentData<T>(e))))
                        EntityManager.RemoveComponent<T>(e);
        }
        protected override void OnUpdate()
        {
            if (!Mod.Active || !blocks.Ready) return;
            Filter<Damage>(damage, e => e.m_Object);
            Filter<Destroy>(destroy, e => e.m_Object);
            Filter<Ignite>(ignite, e => e.m_Target);
            Filter<FaceWeather>(weather, e => e.m_Target);
            Filter<Submerge>(submerge, e => e.m_Target);
        }
    }
}
