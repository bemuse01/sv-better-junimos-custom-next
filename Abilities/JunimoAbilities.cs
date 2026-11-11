using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.Buildings;
using System;
using StardewValley.Characters;
using System.Collections.Generic;
using BetterJunimos.Abilities;
using StardewValley.Objects;
using SObject = StardewValley.Object;
using StardewModdingAPI;

namespace BetterJunimos.Utils {
    public class JunimoAbilities {
        private Dictionary<string, bool> _enabledAbilities;
        private readonly IMonitor _monitor;

        /* ACTION FAILURE COOLDOWNS:
         Intent: don't retry actions that failed recently (in the last game hour)

         Rationale: if IsActionAvailable says yes but PerformAction says no, the Junimo will retry that action all day
         and appear to be stuck on a tile.
         While these two functions should be as consistent as possible, it's impossible for IsActionAvailable to
         predict all the ways that PerformAction could fail.

         Registering a failed action:
         Call ActionFailed. Currently called in the Harmony patch PatchTryToHarvestHere

         Resetting cooldowns:
         Call ResetCooldowns. Currently called by the start-of-day event handler and the hut-menu-closed handler
         (player may have added seeds etc so that failed actions will now succeed)
         */
        private static readonly Dictionary<(GameLocation, Vector2, IJunimoAbility), int> FailureCooldowns = new();


        /*
         * for each hut and map, the last known crop location/work to do
         * because each hut could be servicing several locations (e.g. farm, greenhouse)
         *
         * used in pathFindToNewCrop_doWork
         */
        internal readonly Dictionary<(JunimoHut, GameLocation), Point> lastKnownCropLocations = new();


        private readonly List<IJunimoAbility> _registeredJunimoAbilities = new();

        private static readonly Dictionary<Guid, Dictionary<string, bool>> ItemsInHuts = new();

        private readonly HashSet<string> _requiredItems = new() { SObject.fertilizerCategory.ToString(), SObject.SeedsCategory.ToString() };


        public JunimoAbilities(Dictionary<string, bool> enabledAbilities, IMonitor monitor) {
            _enabledAbilities = enabledAbilities;
            _monitor = monitor;
        }

        // register built in abilities, in order
        internal void RegisterDefaultAbilities() {
            var defaultAbilities = new List<IJunimoAbility> {
                new ClearDeadCropsAbility(),
                new HarvestCropsAbility(),
                new HarvestBushesAbility(),
                new HarvestForageCropsAbility(),
                new FertilizeAbility(),
                new PlantCropsAbility(_monitor),
                new WaterAbility(),
                new VisitGreenhouseAbility(),
            };
            foreach (var junimoAbility in defaultAbilities) {
                RegisterJunimoAbility(junimoAbility);
            }
        }

        /*
         * Add an IJunimoAbility to the list of possible actions
         */
        public void RegisterJunimoAbility(IJunimoAbility junimoAbility) {
            var name = junimoAbility.AbilityName();
            if (!BetterJunimos.Config.JunimoAbilities.ContainsKey(name)) {
                BetterJunimos.Config.JunimoAbilities.Add(name, true);
            }

            if (!_registeredJunimoAbilities.Any(a => a.AbilityName() == name)) {
                _registeredJunimoAbilities.Add(junimoAbility);
            }
            _requiredItems.UnionWith(junimoAbility.RequiredItems());
        }

        // Can the Junimo use a capability/ability here
        public bool IsActionable(GameLocation location, Vector2 pos, Guid id) {
            return IdentifyJunimoAbility(location, pos, id) != null;
        }

        public IJunimoAbility IdentifyJunimoAbility(GameLocation location, Vector2 pos, Guid hutGuid) {
            foreach (var ability in _registeredJunimoAbilities) {
                if (BetterJunimos.Config.JunimoAbilities.TryGetValue(ability.AbilityName(), out bool enabled) && !enabled) {
                    continue;
                }
                if (ActionCoolingDown(location, ability, pos)) continue;
                if (!ItemInHut(hutGuid, ability.RequiredItems())) {
                    continue;
                }

                if (!ability.IsActionAvailable(location, pos, hutGuid)) {
                    continue;
                }

                return ability;
            }

            return null;
        }

        public bool PerformAction(IJunimoAbility ability, Guid id, GameLocation location, Vector2 pos, JunimoHarvester junimo) {
            var hut = Util.GetHutFromId(id);
            var chest = hut.GetOutputChest();

            var success = ability.PerformAction(location, pos, junimo, id);

            var requiredItems = ability.RequiredItems();
            if (requiredItems.Count > 0) {
                UpdateHutContainsItems(id, chest, requiredItems);
            }

            return success;
        }

        public void ActionFailed(GameLocation location, IJunimoAbility ability, Vector2 pos) {
            _monitor.Log($"Action {ability.AbilityName()} at {location.Name} [{pos.X} {pos.Y}] failed", LogLevel.Debug);
            var cd = (location, pos, ability);
            FailureCooldowns[cd] = Game1.timeOfDay;
        }

        public void ListCooldowns() {
            _monitor.Log($"Cooldowns:", LogLevel.Debug);

            foreach (var (key, timeOfDay) in FailureCooldowns) {
                var (gameLocation, (x, y), junimoAbility) = key;
                _monitor.Log($"    {junimoAbility} at {gameLocation} [{x} {y}] since {timeOfDay}", LogLevel.Debug);
            }
        }

        public static void ResetCooldowns() {
            FailureCooldowns.Clear();
            Util.ClearUnreachableTiles();
        }

        public static bool ActionCoolingDown(GameLocation location, IJunimoAbility ability, Vector2 pos) {
            var cd = (location, pos, ability);
            if (!FailureCooldowns.TryGetValue(cd, out var failureTime)) return false;
            if (failureTime <= Game1.timeOfDay - 100) {
                FailureCooldowns.Remove(cd);
                return false;
            }
            return true;
        }

        private static bool ItemInHut(Guid id, string item) {
            // A hut we've never scanned (e.g. added mid-day without a menu close)
            // must not throw here; treat it as having no items until first scan.
            return ItemsInHuts.TryGetValue(id, out var items) && items.TryGetValue(item, out var present) && present;
        }

        public static bool ItemInHut(Guid id, List<string> items) {
            if (items.Count == 0) return true;
            for (int i = 0; i < items.Count; ++i) {
                if (ItemInHut(id, items[i])) return true;
            }
            return false;
        }

        internal void UpdateHutItems(Guid id) {
            if (id == Guid.Empty) return;
            var hut = Util.GetHutFromId(id);
            UpdateHutItems(id, hut);
        }

        internal void UpdateHutItems(Guid id, JunimoHut hut) {
            if (id == Guid.Empty || hut is null) return;
            var chest = hut.GetOutputChest();
            UpdateHutContainsItems(id, chest, _requiredItems);
        }

        private void UpdateHutContainsItems(Guid id, Chest chest, IEnumerable<string> items) {
            if (!ItemsInHuts.TryGetValue(id, out var hutItems)) {
                hutItems = new Dictionary<string, bool>();
                ItemsInHuts[id] = hutItems;
            }

            foreach (var itemId in items) {
                // item categories start with a -
                if (itemId.StartsWith("-"))
                    UpdateHutContainsItemCategory(hutItems, chest, itemId);
                else
                    UpdateHutContainsItemId(hutItems, chest, itemId);
            }
        }

        private static void UpdateHutContainsItemId(Dictionary<string, bool> hutItems, Chest chest, string itemId) {
            bool avoidCoffee = BetterJunimos.Config.JunimoImprovements.AvoidPlantingCoffee;
            bool found = false;
            foreach (var item in chest.Items) {
                if (item != null && item.ItemId == itemId && !(avoidCoffee && item.ItemId == Util.CoffeeItemId)) {
                    found = true;
                    break;
                }
            }
            hutItems[itemId] = found;
        }

        private static void UpdateHutContainsItemCategory(Dictionary<string, bool> hutItems, Chest chest, string itemCategory) {
            string coffeeItemId = Util.CoffeeItemId;
            string seedsCategory = SObject.SeedsCategory.ToString();
            string fertilizerCategory = SObject.fertilizerCategory.ToString();
            bool avoidCoffee = BetterJunimos.Config.JunimoImprovements.AvoidPlantingCoffee;
            bool found = false;

            foreach (var item in chest.Items) {
                if (item == null) continue;
                bool isMatch = item.Category.ToString() == itemCategory;
                if (!isMatch && itemCategory == seedsCategory) {
                    isMatch = item.ItemId == coffeeItemId;
                }
                if (!isMatch) continue;
                if (avoidCoffee && item.ItemId == coffeeItemId) continue;
                if (itemCategory == fertilizerCategory && item.ItemId == "805") continue;

                found = true;
                break;
            }
            hutItems[itemCategory] = found;
        }
    }
}