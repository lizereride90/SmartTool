using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
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

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Water dry crops",
                tooltip: () => "Grab the watering can when you tap a thirsty crop.",
                getValue: () => this.config.WaterDryCrops,
                setValue: value => this.config.WaterDryCrops = value
            );

            gmcm.AddBoolOption(
                mod: this.ModManifest,
                name: () => "Scythe for weeds",
                tooltip: () => "Grab the scythe when you tap grass or weeds.",
                getValue: () => this.config.UseScytheOnGrass,
                setValue: value => this.config.UseScytheOnGrass = value
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
            if (this.config == null || !this.config.Enabled)
                return;

            if (!Context.IsWorldReady || !Context.IsPlayerFree)
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

            string wanted = this.FigureOutTool(loc, tile, who);
            if (wanted == null)
                return;

            // already holding it, nothing to do
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

        private string FigureOutTool(GameLocation loc, Vector2 tile, Farmer who)
        {
            // mines: if something is close, sword first
            if (this.config.SwitchToWeaponInMines && loc is MineShaft)
            {
                foreach (var npc in loc.characters)
                {
                    if (npc is StardewValley.Monsters.Monster monster && !monster.IsInvisible)
                    {
                        float dist = Vector2.Distance(monster.Tile, tile);
                        if (dist < 2.5f)
                            return "Weapon";
                    }
                }
            }

            // dropped stuff on the ground (twig, stone, weed)
            if (loc.Objects.TryGetValue(tile, out var obj))
            {
                string name = obj.Name ?? "";

                if (name.Contains("Stone", StringComparison.OrdinalIgnoreCase))
                    return "Pickaxe";

                if (name.Contains("Twig", StringComparison.OrdinalIgnoreCase))
                    return "Axe";

                if (name.Contains("Weed", StringComparison.OrdinalIgnoreCase))
                {
                    if (this.config.UseScytheOnGrass)
                        return "Scythe";
                    return null;
                }

                // quartz, gems, etc lying around
                if (name.Contains("Quartz", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Gem", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Ore", StringComparison.OrdinalIgnoreCase))
                    return "Pickaxe";

                // a growing crop in a pot or on the ground, leave hands free
                if (obj is IndoorPot)
                    return null;
            }

            // dirt, trees, grass
            if (loc.terrainFeatures.TryGetValue(tile, out var feature))
            {
                if (feature is HoeDirt dirt)
                {
                    if (dirt.crop != null)
                    {
                        // ready to pick, don't cover their hands with a tool
                        if (dirt.crop.readyForHarvest.Value)
                            return null;

                        if (this.config.WaterDryCrops && dirt.needsWatering())
                            return "WateringCan";

                        return null;
                    }

                    return "Hoe";
                }

                if (feature is Grass)
                {
                    if (this.config.UseScytheOnGrass)
                        return "Scythe";
                    return null;
                }

                if (feature is Tree || feature is FruitTree || feature is GiantCrop)
                    return "Axe";
            }

            // big stumps, logs, boulders, meteorites
            foreach (var clump in loc.resourceClumps)
            {
                if (clump == null)
                    continue;

                Vector2 pos = clump.Tile;
                if ((int)pos.X == (int)tile.X && (int)pos.Y == (int)tile.Y)
                {
                    int id = clump.parentSheetIndex.Value;
                    if (id == 622 || id == 672 || id == 756 || id == 758)
                        return "Pickaxe";
                    return "Axe";
                }
            }

            // tillable soil with nothing on it
            if (loc.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") != null)
                return "Hoe";

            return null;
        }

        private bool Holding(Farmer who, string wanted)
        {
            var current = who.CurrentTool;
            if (current == null)
                return false;

            return this.Matches(current, wanted);
        }

        private int FindBestSlot(Farmer who, string wanted)
        {
            int bestSlot = -1;
            int bestLevel = -1;

            for (int i = 0; i < who.Items.Count; i++)
            {
                var item = who.Items[i];
                if (item is not Item tool)
                    continue;

                if (!this.Matches(tool, wanted))
                    continue;

                int level = 0;
                if (tool is Tool t)
                    level = t.upgradeLevel.Value;

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
                    // scythe is a melee weapon with its own id, but name check is fine
                    return name.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) >= 0;

                case "Weapon":
                    return item is MeleeWeapon && name.IndexOf("Scythe", StringComparison.OrdinalIgnoreCase) < 0;

                default:
                    return false;
            }
        }
    }
}
