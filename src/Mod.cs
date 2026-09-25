using System;
using System.IO;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;

namespace SuperTrafficSpawnBlock
{
    public sealed class Mod : IMod
    {
        public const string DisplayName = "Super Traffic Spawn Block";
        internal const string Id = "SuperTrafficSpawnBlock";
        internal static readonly ILog Log = LogManager.GetLogger(Id).SetShowsErrorsInUI(true);
        internal static Settings Options;
        internal static bool Active;
        internal static string Status = "SuperTrafficSpawnBlock.Status.Waiting";
        private Locale en, ko;
        private Colossal.UI.UISystem iconHost;
        internal static bool IconsAvailable;

        public void OnLoad(UpdateSystem updates)
        {
            RegisterIcons();
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings(Id, Options, new Settings(this));
            en = new Locale("en-US");
            ko = new Locale("ko-KR");
            GameManager.instance.localizationManager.AddSource("en-US", en);
            GameManager.instance.localizationManager.AddSource("ko-KR", ko);
            Options.RegisterInOptionsUI();
            Active = true;
            updates.UpdateBefore<BlockPrefabSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updates.UpdateBefore<DestinationRoutingSystem, SpawnMultiplierSystem>(SystemUpdatePhase.GameSimulation);
            updates.UpdateBefore<SpawnMultiplierSystem, TrafficSpawnerAISystem>(SystemUpdatePhase.GameSimulation);
            updates.UpdateBefore<GeneratedTrafficSystem, Game.Vehicles.InitializeSystem>(SystemUpdatePhase.Modification5);
            updates.UpdateAt<TrafficSinkSystem>(SystemUpdatePhase.GameSimulation);
            updates.UpdateAt<DestinationUI>(SystemUpdatePhase.UIUpdate);
            updates.UpdateBefore<SpawnMultiplierSystem, TrafficSpawnerAISystem>(SystemUpdatePhase.LoadSimulation);
            updates.UpdateBefore<DepartureTimingSystem, Game.Vehicles.InitializeSystem>(SystemUpdatePhase.Modification5);
            updates.UpdateBefore<DepartureLaneSystem, Game.Vehicles.InitializeSystem>(SystemUpdatePhase.Modification5);
            Log.Info(DisplayName + " 0.4.0 loaded. Unified source with per-block rate and multiple destinations.");
        }

        private void RegisterIcons()
        {
            IconsAvailable = false;
            try
            {
                // CS2 loads assemblies from bytes; Assembly.Location can be empty.
                if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset) || asset == null || string.IsNullOrWhiteSpace(asset.path))
                    throw new InvalidOperationException("Executable asset path is unavailable.");
                string directory = Path.Combine(Path.GetDirectoryName(asset.path), "icons");
                foreach (string icon in new[] { "source.svg", "sink.svg" })
                    if (!File.Exists(Path.Combine(directory, icon)))
                        throw new FileNotFoundException("Missing block thumbnail", icon);
                var ui = Colossal.UI.UIManager.defaultUISystem;
                if (ui == null) throw new InvalidOperationException("UI system is unavailable.");
                ui.AddHostLocation("supertrafficspawnblock", directory, false);
                iconHost = ui;
                IconsAvailable = true;
                Log.Info("Thumbnail host registered: " + directory);
            }
            catch (Exception error)
            {
                // Cosmetic failures must not prevent options, systems or save-referenced prefabs loading.
                Log.Warn("Custom thumbnails unavailable; retaining native icons. " + error.Message);
            }
        }

        internal static void Fail(Exception error, string message)
        {
            Active = false;
            Status = "SuperTrafficSpawnBlock.Status.Inactive";
            Log.Error(error, message);
        }

        public void OnDispose()
        {
            Active = false;
            IconsAvailable = false;
            iconHost?.RemoveHostLocation("supertrafficspawnblock");
            iconHost = null;
            Options?.UnregisterInOptionsUI();
            Options = null;
            if (en != null) GameManager.instance.localizationManager.RemoveSource("en-US", en);
            if (ko != null) GameManager.instance.localizationManager.RemoveSource("ko-KR", ko);
            // Registered prefabs may still be referenced by placed blocks. The world owns their lifetime.
        }
    }
}













