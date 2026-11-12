using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

namespace BetterJunimos.Utils {
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
        }

        private string Get(string key) {
            return _helper.Translation.Get(key);
        }
    }
}
