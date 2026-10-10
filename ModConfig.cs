namespace SmartTool
{
    /// <summary>Simple toggles for Smart Tool. All of these show up in GMCM if you have it.</summary>
    internal class ModConfig
    {
        public bool Enabled { get; set; } = true;

        public StardewModdingAPI.SButton ToggleButton { get; set; } = StardewModdingAPI.SButton.F;

        public bool WaterDryCrops { get; set; } = true;

        public bool UseScytheOnGrass { get; set; } = true;

        public bool ShakeFruitTreesByHand { get; set; } = true;

        public bool SwitchToWeaponInMines { get; set; } = true;

        public bool PlaySoundOnSwitch { get; set; } = false;
    }
}
