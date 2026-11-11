using System;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;

namespace BetterJunimos.Utils {
    enum Configure {
        Yes,
        No,
        Rude,
        NotAnswered
    }

    enum Wages {
        Fruit,
        Flowers,
        Forage,
        All,
        None,
        NotAnswered
    }

    enum ShowGMCM {
        Yes,
        No,
        NotAnswered
    }

    public class JunimoProgression {
        private readonly IManifest _manifest;
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;

        internal JunimoProgression(IManifest manifest, IMonitor monitor, IModHelper helper) {
            _manifest = manifest;
            _monitor = monitor;
            _helper = helper;
        }

        public void ShowConfigurationMenu() {
            Game1.playSound("shwip");
            var configMenu = _helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            configMenu?.OpenModMenu(_manifest);
        }

        internal void ListHuts() {
            _monitor.Log("Huts:", LogLevel.Debug);

            foreach (JunimoHut hut in Util.GetAllHuts()) {
                _monitor.Log($"    [{hut.tileX} {hut.tileY}] {Util.GetHutIdFromHut(hut)}", LogLevel.Debug);
            }
        }

        internal void ListAllAvailableActions() {
            foreach (JunimoHut hut in Util.GetAllHuts()) {
                ListAvailableActions(Util.GetHutIdFromHut(hut));
            }
        }

        internal void ListConfigurableAbilities() {
            _monitor.Log("Configurable abilities:", LogLevel.Debug);

            foreach (var (key, value) in BetterJunimos.Config.JunimoAbilities) {
                var status = value ? "enabled" : "disabled by configuration";
                _monitor.Log($"    {key}: {status}", LogLevel.Debug);
            }
        }

        // search for crops + open plantable spots
        internal void ListAvailableActions(Guid id) {
            var hut = Util.GetHutFromId(id);
            if (hut is null) return;
            var radius = Util.CurrentWorkingRadius;
            var farm = hut.GetParentLocation() ?? Game1.getFarm();

            _monitor.Log($"{Get("debug.available-actions-for-hut-at")} [{hut.tileX} {hut.tileY}] ({id}):", LogLevel.Debug);

            var (minX, maxX, minY, maxY) = Util.GetHutRadiusBounds(hut, radius);
            for (var x = minX; x <= maxX; ++x) {
                for (var y = minY; y <= maxY; ++y) {
                    var pos = new Vector2(x, y);

                    var ability = Util.Abilities.IdentifyJunimoAbility(farm, pos, id);
                    if (ability == null) {
                        continue;
                    }

                    var cooldown = JunimoAbilities.ActionCoolingDown(farm, ability, pos) ? Get("debug.in-cooldown") : "";
                    var itemsAvail = JunimoAbilities.ItemInHut(id, ability.RequiredItems()) ? "" : Get("debug.required-item-unavailable");

                    _monitor.Log($"    {farm.Name} [{pos.X} {pos.Y}] {ability.AbilityName()} {cooldown} {itemsAvail}", LogLevel.Debug);
                }
            }

            if (!Util.Greenhouse.HutHasGreenhouse(id)) {
                return;
            }

            _monitor.Log($"{Get("debug.available-actions-for-greenhouse")}:", LogLevel.Debug);

            var gh = Game1.getLocationFromName("Greenhouse");
            for (var x = 0; x < gh.map.Layers[0].LayerWidth; x++) {
                for (var y = 0; y < gh.map.Layers[0].LayerHeight; y++) {
                    var pos = new Vector2(x, y);
                    var ability = Util.Abilities.IdentifyJunimoAbility(gh, pos, id);
                    if (ability == null) {
                        continue;
                    }

                    var cooldown = JunimoAbilities.ActionCoolingDown(gh, ability, pos) ? Get("debug.in-cooldown") : "";
                    var itemsAvail = JunimoAbilities.ItemInHut(id, ability.RequiredItems()) ? "" : Get("debug.required-item-unavailable");

                    _monitor.Log($"    {gh.Name} [{pos.X} {pos.Y}] {ability.AbilityName()} {cooldown} {itemsAvail}", LogLevel.Debug);
                }
            }
        }

        private string Get(string key) {
            return _helper.Translation.Get(key);
        }

        internal void SetupHutsToken() {
            var api = BetterJunimos.ContentPatcherAPI;

            api?.RegisterToken(_manifest, "JunimoHuts", () => {
                if (!Context.IsWorldReady) return null;
                var huts = Game1.getFarm().buildings.Any(b => b is JunimoHut);
                return new[] { huts ? "true" : "false" };
            });
        }

        internal void ConfigureFromWizard(object o, OneSecondUpdateTickedEventArgs e) {
            if (!Context.IsWorldReady) return;
            if (!Context.IsMainPlayer) return;

            if (!Game1.player.eventsSeen.Contains("22210001")) {
                Game1.getFarm().modData.Remove($"{_manifest.UniqueID}/ConfigurationWizardDone");
                return;
            }

            if (Game1.getFarm().modData.ContainsKey($"{_manifest.UniqueID}/ConfigurationWizardDone")) {
                return;
            }

            Game1.getFarm().modData[$"{_manifest.UniqueID}/ConfigurationWizardDone"] = "true";

            Configure c = Configure.NotAnswered;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219010")) c = Configure.Yes;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219011")) c = Configure.No;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219012")) c = Configure.Rude;

            Wages w = Wages.NotAnswered;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219030")) w = Wages.Fruit;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219031")) w = Wages.Flowers;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219032")) w = Wages.Forage;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219033")) w = Wages.All;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219034")) w = Wages.None;

            ShowGMCM s = ShowGMCM.NotAnswered;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219040")) s = ShowGMCM.Yes;
            if (Game1.player.dialogueQuestionsAnswered.Contains("22219041")) s = ShowGMCM.No;

            if (c is Configure.No or Configure.Rude or Configure.NotAnswered) {
                return;
            }

            switch (w) {
                case Wages.Flowers:
                    BetterJunimos.Config.JunimoPayment.WorkForWages = true;
                    BetterJunimos.Config.JunimoPayment.DailyWage = new ModConfig.JunimoPayments.PaymentAmount { Flowers = 1, Fruit = 0, ForagedItems = 0 };
                    break;
                case Wages.Fruit:
                    BetterJunimos.Config.JunimoPayment.WorkForWages = true;
                    BetterJunimos.Config.JunimoPayment.DailyWage = new ModConfig.JunimoPayments.PaymentAmount { Flowers = 0, Fruit = 1, ForagedItems = 0 };
                    break;
                case Wages.Forage:
                    BetterJunimos.Config.JunimoPayment.WorkForWages = true;
                    BetterJunimos.Config.JunimoPayment.DailyWage = new ModConfig.JunimoPayments.PaymentAmount { Flowers = 0, Fruit = 0, ForagedItems = 1 };
                    break;
                case Wages.All:
                    BetterJunimos.Config.JunimoPayment.WorkForWages = true;
                    BetterJunimos.Config.JunimoPayment.DailyWage = new ModConfig.JunimoPayments.PaymentAmount { Flowers = 1, Fruit = 1, ForagedItems = 1 };
                    break;
                case Wages.None:
                    BetterJunimos.Config.JunimoPayment.WorkForWages = false;
                    BetterJunimos.Config.JunimoPayment.DailyWage = new ModConfig.JunimoPayments.PaymentAmount { Flowers = 0, Fruit = 0, ForagedItems = 0 };
                    break;
                case Wages.NotAnswered:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            BetterJunimos.SaveConfig();

            if (s == ShowGMCM.Yes) {
                var configMenu = _helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
                if (configMenu is null) {
                    Util.SendMessage(_helper.Translation.Get("wizard.edit-options"));
                    Util.SendMessage(_helper.Translation.Get("wizard.install-gmcm"));
                    return;
                }

                ShowConfigurationMenu();
            }
        }
    }
}
