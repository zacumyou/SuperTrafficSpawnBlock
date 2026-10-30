using Colossal.UI.Binding;
using Game.Prefabs;
using Game.UI.InGame;
namespace SuperTrafficSpawnBlock
{
    public partial class DestinationSection : InfoSectionBase
    {
        protected override string group => "SuperTrafficDestination";
        protected override void Reset() { }
        protected override void OnProcess() { }
        public override void OnWriteProperties(IJsonWriter writer) { }
        protected override void OnUpdate()
        {
            var blocks = World.GetOrCreateSystemManaged<BlockPrefabSystem>();
            visible = Mod.Active && blocks.Ready && EntityManager.HasComponent<PrefabRef>(selectedEntity) &&
                (blocks.FactorFor(EntityManager.GetComponentData<PrefabRef>(selectedEntity).m_Prefab) > 0 ||
                 World.GetOrCreateSystemManaged<DestinationRoutingSystem>().IsSink(selectedEntity));
        }
    }
}
