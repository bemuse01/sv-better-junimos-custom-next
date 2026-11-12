using System;
using System.Linq;
using BetterJunimos.Utils;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley.Buildings;
using SObject = StardewValley.Object;

namespace BetterJunimos.Abilities {
    public class PlantCropsAbility : IJunimoAbility {
        private readonly IMonitor Monitor;

        private const string SunflowerSeeds = "431";
        private const string TeaSapling = "251";
        static Dictionary<string, Dictionary<string, bool>> cropSeasons = new();
        private static readonly Dictionary<string, bool> IsTrellisCache = new();
        private static readonly Dictionary<string, bool> IsCropSeedCache = new();

        internal PlantCropsAbility(IMonitor Monitor) {
            this.Monitor = Monitor;
            var seasons = new List<string> { "spring", "summer", "fall", "winter" };
            foreach (string season in seasons) {
                cropSeasons[season] = new Dictionary<string, bool>();
            }
        }

        public string AbilityName() {
            return "PlantCrops";
        }

        public bool IsActionAvailable(GameLocation location, Vector2 pos, Guid guid) {
            var plantable = location.terrainFeatures.ContainsKey(pos) && location.terrainFeatures[pos] is HoeDirt { crop: null } && !location.objects.ContainsKey(pos);
            if (!plantable) return false;

            // todo: this section is potentially slow and might be refined
            JunimoHut hut = Util.GetHutFromId(guid);
            Chest chest = hut.GetOutputChest();
            string cropType = BetterJunimos.CropMaps.GetCropForPos(hut, pos);
            Item foundItem = PlantableSeed(location, chest, cropType);

            return foundItem is not null;
        }

        public bool PerformAction(GameLocation location, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            JunimoHut hut = Util.GetHutFromId(guid);
            Chest chest = hut.GetOutputChest();
            string cropType = BetterJunimos.CropMaps.GetCropForPos(hut, pos);
            Item foundItem = PlantableSeed(location, chest, cropType);
            if (foundItem is null) {
                Monitor.Log($"No seed to plant at {location.Name} [{pos.X} {pos.Y}]", LogLevel.Warn);
                return false;
            }

            if (Plant(location, pos, foundItem.ItemId)) {
                Util.RemoveItemFromChest(chest, foundItem);
                return true;
            }

            BetterJunimos.SMonitor.Log($"PerformAction did not plant", LogLevel.Warn);
            return false;
        }

        /// <summary>Get an item from the chest that is a crop seed, plantable in this season</summary>
        private Item PlantableSeed(GameLocation location, Chest chest, string cropType = null) {
            bool avoidCoffee = BetterJunimos.Config.JunimoImprovements.AvoidPlantingCoffee;
            bool avoidOutOfSeason = BetterJunimos.Config.JunimoImprovements.AvoidPlantingOutOfSeason;

            foreach (var item in chest.Items) {
                if (item == null) continue;
                if (avoidCoffee && item.ItemId == Util.CoffeeItemId) continue;
                if (!IsCrop(item, location)) continue;

                if (cropType == CropTypes.Trellis && !IsTrellisCrop(item, location)) continue;
                if (cropType == CropTypes.Ground && IsTrellisCrop(item, location)) continue;

                string resolvedSeedId = Crop.ResolveSeedId(item.ItemId, location);
                if (!Game1.cropData.ContainsKey(resolvedSeedId)) continue;

                if (!avoidOutOfSeason) {
                    return item;
                }

                if (item.ItemId == SunflowerSeeds && Game1.IsFall && Game1.dayOfMonth >= 25) {
                    // there is no way that a sunflower planted on Fall 25 will grow to harvest
                    continue;
                }

                if (!cropSeasons.TryGetValue(Game1.currentSeason, out var seasonDict)) {
                    seasonDict = new Dictionary<string, bool>();
                    cropSeasons[Game1.currentSeason] = seasonDict;
                }

                if (!seasonDict.TryGetValue(resolvedSeedId, out bool inSeason)) {
                    var crop = new Crop(resolvedSeedId, 0, 0, location);
                    inSeason = crop.IsInSeason(location);
                    seasonDict[resolvedSeedId] = inSeason;
                }

                if (inSeason) {
                    return item;
                }
            }

            return null;
        }

        private bool IsTrellisCrop(Item item, GameLocation location) {
            if (IsTrellisCache.TryGetValue(item.ItemId, out bool isTrellis)) {
                return isTrellis;
            }
            if (Game1.cropData.TryGetValue(item.ItemId, out var data)) {
                isTrellis = data.IsRaised;
            } else {
                Crop crop = new Crop(item.ItemId, 0, 0, location);
                isTrellis = crop.raisedSeeds.Value;
            }
            IsTrellisCache[item.ItemId] = isTrellis;
            return isTrellis;
        }

        //Verify if the item is a crop seed
        private bool IsCrop(Item item, GameLocation location) {
            if (item.Category != -74 && item.ItemId != Util.CoffeeItemId) return false;

            // Mixed Seeds / Mixed Flower Seeds are only planted when enabled (they're
            // excluded by default, matching the original behaviour)
            if (item.ItemId is "770" or "MixedFlowerSeeds") {
                return BetterJunimos.Config.JunimoImprovements.PlantMixedSeeds;
            }

            // Tea Saplings are Seeds (-74) but plant a Bush, not a Crop.
            if (item.ItemId == TeaSapling) return false;

            if (IsCropSeedCache.TryGetValue(item.ItemId, out bool isCropSeed)) {
                return isCropSeed;
            }

            isCropSeed = !Tree.GetWildTreeSeedLookup().Keys.Contains(item.ItemId) && !Game1.fruitTreeData.Keys.Contains(item.ItemId);
            IsCropSeedCache[item.ItemId] = isCropSeed;
            return isCropSeed;
        }

        private static readonly List<string> RequiredSeedItems = new() { SObject.SeedsCategory.ToString() };

        public List<string> RequiredItems() {
            return RequiredSeedItems;
        }

        private bool Plant(GameLocation location, Vector2 pos, string index) {
            string resolvedIndex = Crop.ResolveSeedId(index, location);
            if (!Game1.cropData.ContainsKey(resolvedIndex)) return false;

            Crop crop = new Crop(resolvedIndex, (int)pos.X, (int)pos.Y, location);

            if (!crop.IsInSeason(location) && BetterJunimos.Config.JunimoImprovements.AvoidPlantingOutOfSeason) {
                Monitor.Log($"Crop {crop} ({index}) cannot be planted in {Game1.currentSeason}", LogLevel.Warn);
                return false;
            }

            if (!location.terrainFeatures.TryGetValue(pos, out var tf) || tf is not HoeDirt hd || hd.crop != null) return false;
            hd.crop = crop;
            applySpeedIncreases(hd);
            ApplyPaddy(hd, location);

            if (Utility.isOnScreen(Utility.Vector2ToPoint(pos), 64, location)) {
                if (crop.raisedSeeds.Value) location.playSound("stoneStep");
                location.playSound("dirtyHit");
            }

            ++Game1.stats.SeedsSown;
            return true;
        }

        private void ApplyPaddy(HoeDirt hd, GameLocation location) {
            hd.nearWaterForPaddy.Value = -1;
            if (!hd.hasPaddyCrop()) return;
            if (!hd.paddyWaterCheck()) return;
            hd.state.Value = 1;
            hd.updateNeighbors();
        }

        private void applySpeedIncreases(HoeDirt hd) {
            var who = Game1.player;

            if (hd.crop == null) return;

            var paddyWaterCheck = hd.paddyWaterCheck();
            var fertilizerSpeedBoost = hd.GetFertilizerSpeedBoost();
            var agriculturalist = who.professions.Contains(5);

            if (fertilizerSpeedBoost <= 0 && !agriculturalist && !paddyWaterCheck) return;

            hd.crop.ResetPhaseDays();
            var num1 = 0;
            for (var index = 0; index < hd.crop.phaseDays.Count - 1; ++index) num1 += hd.crop.phaseDays[index];
            var num2 = fertilizerSpeedBoost;

            if (paddyWaterCheck) num2 += 0.25f;
            if (agriculturalist) num2 += 0.1f;
            var num3 = (int)Math.Ceiling((double)num1 * num2);
            for (var index1 = 0; num3 > 0 && index1 < 3; ++index1) {
                for (var index2 = 0; index2 < hd.crop.phaseDays.Count; ++index2) {
                    if ((index2 > 0 || hd.crop.phaseDays[index2] > 1) && hd.crop.phaseDays[index2] != 99999) {
                        hd.crop.phaseDays[index2]--;
                        --num3;
                    }

                    if (num3 <= 0) break;
                }
            }
        }
    }
}
