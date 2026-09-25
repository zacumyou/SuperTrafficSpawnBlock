using System;
using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace SuperTrafficSpawnBlock
{

    [FileLocation(Mod.Id)]
    public sealed class Settings : ModSetting
    {
        public Settings(IMod mod) : base(mod) { }


        [SettingsUISection("Main", "Traffic")]
        public bool AllowHeavyVehicles { get; set; } = false;


        [SettingsUISection("Main", "Traffic")]
        public string Status => Game.SceneFlow.GameManager.instance.localizationManager.activeDictionary.TryGetValue(Mod.Status, out var text) ? text : Mod.Status;

        public override void SetDefaults() { AllowHeavyVehicles = false; }
    }

}
