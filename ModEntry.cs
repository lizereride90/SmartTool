using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System;

namespace SmartTool
{
    internal class ModEntry : Mod
    {
        private ModConfig config;

        public override void Entry(IModHelper helper)
        {
            this.config = helper.ReadConfig<ModConfig>();

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;

            helper.ConsoleCommands.Add("smarttool_toggle", "Turns Smart Tool on or off.", this.OnToggleCommand);
        }

        private void OnToggleCommand(string cmd, string[] args)
        {
            this.config.Enabled = !this.config.Enabled;
            this.Helper.WriteConfig(this.config);

            string state = this.config.Enabled ? "on" : "off";
            this.Monitor.Log($"Smart Tool is now {state}.", LogLevel.Info);
            if (Context.IsWorldReady)
                Game1.addHUDMessage(new HUDMessage($"Smart Tool {state}", 2));
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            gmcm.Register(
                mod: this.ModManifest,
                reset: () => this.config = new ModConfig(),
                save: () => this.Helper.WriteConfig(this.config)
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Enable Smart Tool",
                tooltip: () => "Turn the auto tool switching on or off.",
                getValue: () => this.config.Enabled,
                setValue: value => this.config.Enabled = value
            );

            gmcm.AddKeybind(
                mod: this.ModManifest,
                name: () => "Toggle key",
                tooltip: () => "Press this to turn Smart Tool on or off. Console command smarttool_toggle does the same.",
                getValue: () => this.config.ToggleButton,
                setValue: value => this.config.ToggleButton = value
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Water dry crops",
                tooltip: () => "Grab the watering can when you tap a thirsty crop, including garden pots.",
                getValue: () => this.config.WaterDryCrops,
                setValue: value => this.config.WaterDryCrops = value
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Scythe for weeds",
                tooltip: () => "Grab the scythe for grass, weeds and dead crops.",
                getValue: () => this.config.UseScytheOnGrass,
                setValue: value => this.config.UseScytheOnGrass = value
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Shake fruit trees by hand",
                tooltip: () => "Leaves your hands free on fruit trees so you shake instead of chop.",
                getValue: () => this.config.ShakeFruitTreesByHand,
                setValue: value => this.config.ShakeFruitTreesByHand = value
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Weapon in mines",
                tooltip: () => "Grab your sword when something is coming at you underground.",
                getValue: () => this.config.SwitchToWeaponInMines,
                setValue: value => this.config.SwitchToWeaponInMines = value
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Click sound",
                tooltip: () => "Play a little sound when the tool changes.",
                getValue: () => this.config.PlaySoundOnSwitch,
                setValue: value => this.config.PlaySoundOnSwitch = value
            );
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (this.config == null)
                return;

            if (!Context.IsWorldReady || !Context.IsPlayerFree)
                return;

            // toggle key works even when the mod is off
            if (e.Button == this.config.ToggleButton)
            {
                this.OnToggleCommand("smarttool_toggle", Array.Empty<string>());
                return;
            }

            if (!this.config.Enabled)
                return;

            // tool use comes through as left click on touch/mouse, X on controller
            if (e.Button != SButton.MouseLeft && e.Button != SButton.ControllerX && e.Button != SButton.ControllerA)
                return;

            Farmer who = Game1.player;
            if (who == null || who.CurrentItem is not Tool)
                return;

            Vector2 tile = this.Helper.Input.GetCursorPosition().GrabTile;
            GameLocation loc = Game1.currentLocation;
            if (loc == null)
                return;

            string wanted = this.FigureOutTool(loc, tile);
            if (wanted == null)
                return;

            if (this.Holding(who, wanted))
                return;

            int slot = this.FindBestSlot(who, wanted);
            if (slot < 0)
                return;

            who.CurrentToolIndex = slot;

            if (this.config.PlaySoundOnSwitch)
            {
                try { Game1.playSound("smallSelect"); } catch { }
            }
        }

        private string FigureOutTool(GameLocation loc, Vector2 tile)
        {
            // mines: if something is close, sword first
            if (this.config.SwitchToWeaponInMines && loc is MineShaft)
            {
                foreach (var npc in loc.characters)
                {
                    if (npc is Monster monster && !monster.IsInvisible)
                    {
                        float dist = Vector2.Distance(monster.Tile, tile);
                        if (dist < 2.5f)
                            return "Weapon";
                    }
                }
            }

            if (loc.Objects.TryGetValue(tile, out var obj))
            {
                // chests, machines, furniture, crab pots: hands off, let them interact
                if (obj is Chest || obj.bigCraftable.Value || obj is Furniture || obj is CrabPot || obj is Cask)
                    return null;

                if (obj is IndoorPot pot)
                    return this.PotTool(pot);

                string name = obj.Name ?? "";

                if (name.Contains("Stone", StringComparison.OrdinalIgnoreCase))
                    return "Pickaxe";

                if (name.Contains("Twig", StringComparison.OrdinalIgnoreCase))
                    return "Axe";

                if (name.Contains("Weed", StringComparison.OrdinalIgnoreCase))
                    return this.config.UseScytheOnGrass ? "Scythe" : null;

                if (name.Contains("Quartz", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Gem", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Ore", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Meteorite", StringComparison.OrdinalIgnoreCase))
                    return "Pickaxe";
            }

            if (loc.terrainFeatures.TryGetValue(tile, out var feature))
            {
                if (feature is HoeDirt dirt)
                {
                    if (dirt.crop != null)
                    {
                        // dead crop clears with a scythe
                        if (dirt.crop.dead.Value)
                            return this.config.UseScytheOnGrass ? "Scythe" : null;

                        // ready to pick, keep hands empty
                        if (dirt.readyForHarvest())
                            return null;

                        if (this.config.WaterDryCrops && dirt.needsWatering())
                            return "WateringCan";

                        return null;
                    }

                    return "Hoe";
                }

                if (feature is Grass)
                    return this.config.UseScytheOnGrass ? "Scythe" : null;

                if (feature is GiantCrop)
                    return "Axe";

                if (feature is FruitTree)
                    return this.config.ShakeFruitTreesByHand ? null : "Axe";

                if (feature is Tree)
                    return "Axe";
            }

            // big stumps, logs, boulders, meteorites are 2x2, so check the whole footprint
            foreach (var clump in loc.resourceClumps)
            {
                if (clump == null)
                    continue;

                var box = new Rectangle((int)clump.Tile.X, (int)clump.Tile.Y, clump.width.Value, clump.height.Value);
                if (!box.Contains((int)tile.X, (int)tile.Y))
                    continue;

                int id = clump.parentSheetIndex.Value;
                if (id == 622 || id == 672 || id == 756 || id == 758)
                    return "Pickaxe";
                return "Axe";
            }

            // bare tillable soil
            if (loc.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") != null)
                return "Hoe";

            return null;
        }

        private string PotTool(IndoorPot pot)
        {
            try
            {
                var dirt = pot.hoeDirt.Value;
                if (dirt?.crop != null)
                {
                    if (dirt.readyForHarvest())
                        return null;
                    if (this.config.WaterDryCrops && dirt.needsWatering())
                        return "WateringCan";
                    return null;
                }
            }
            catch { }

            return null;
        }

        private bool Holding(Farmer who, string wanted)
        {
            return who.CurrentTool != null && this.Matches(who.CurrentTool, wanted);
        }

        private int FindBestSlot(Farmer who, string wanted)
        {
            int bestSlot = -1;
            int bestLevel = -1;

            for (int i = 0; i < who.Items.Count; i++)
            {
                var item = who.Items[i];
                if (item == null || !this.Matches(item, wanted))
                    continue;

                int level = item is Tool tool ? tool.UpgradeLevel : 0;
                if (level > bestLevel)
                {
                    bestLevel = level;
                    bestSlot = i;
                }
            }

            return bestSlot;
        }

        private bool Matches(Item item, string wanted)
        {
            string name = item.Name ?? "";

            switch (wanted)
            {
                case "Axe":
                    return item is Axe;

                case "Pickaxe":
                    return item is Pickaxe;

                case "Hoe":
                    return item is Hoe;

                case "WateringCan":
                    return item is WateringCan;

                case "Scythe":
                    return name.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) >= 0;

                case "Weapon":
                    return item is MeleeWeapon && name.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) < 0;

                default:
                    return false;
            }
        }
    }
}
