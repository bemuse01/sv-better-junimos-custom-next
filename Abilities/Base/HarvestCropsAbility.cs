using System;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using BetterJunimos.Utils;
using StardewModdingAPI;

namespace BetterJunimos.Abilities {
    public class HarvestCropsAbility : IJunimoAbility {
        // Cauliflower ("190"), Melon ("254"), Pumpkin ("276"), Qi Fruit ("889"), Powdermelon ("Powdermelon")
        private static readonly HashSet<string> GiantCrops = new() { "190", "254", "276", "889", "Powdermelon" };
        private const string SunflowerId = "421";

        internal HarvestCropsAbility() { }

        public string AbilityName() {
            return "HarvestCrops";
        }

        public bool IsActionAvailable(GameLocation location, Vector2 pos, Guid guid) {
            if (!location.terrainFeatures.ContainsKey(pos) || location.terrainFeatures[pos] is not HoeDirt hd) return false;
            if (hd.crop is null) return false;
            if (!hd.readyForHarvest()) return false;
            return !ShouldAvoidHarvesting(location, hd);
        }

        public bool PerformAction(GameLocation location, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            // calculate the experience from this harvest
            if (BetterJunimos.Config.JunimoImprovements.GiveExperience) {
                try {
                    if (location.terrainFeatures.ContainsKey(pos) && location.terrainFeatures[pos] is HoeDirt { crop: { } } hd) {
                        Game1.player.gainExperience(0, Util.ExperienceForCrop(hd.crop));
                    }
                } catch (Exception e) {
                    // modded skill frameworks can throw when given experience; don't
                    // let that break the harvest
                    BetterJunimos.SMonitor.Log($"Could not grant farming experience: {e.Message}", LogLevel.Trace);
                }
            }

            // Don't do anything, as the base junimo handles this already (see PatchTryToHarvestHere)
            return true;
        }

        private static readonly List<string> EmptyItems = new();

        public List<string> RequiredItems() {
            return EmptyItems;
        }

        private bool ShouldAvoidHarvesting(GameLocation location, HoeDirt hd) {
            string harvestId = hd.crop.indexOfHarvest.Value;
            if (string.IsNullOrEmpty(harvestId)) return false;

            bool avoidGiants = BetterJunimos.Config.JunimoImprovements.AvoidHarvestingGiants && GiantCrops.Contains(harvestId);
            bool avoidFlowers = BetterJunimos.Config.JunimoImprovements.AvoidHarvestingFlowers &&
                                Game1.objectData.TryGetValue(harvestId, out var objData) &&
                                objData.Category == StardewValley.Object.flowersCategory;

            if (!avoidGiants && !avoidFlowers) {
                return false;
            }

            if (Game1.dayOfMonth >= 28 && BetterJunimos.Config.JunimoImprovements.HarvestEverythingOn28th && !location.SeedsIgnoreSeasonsHere()) {
                if (WillCropDieTomorrow(location, hd.crop, harvestId)) {
                    return false;
                }
            }

            return true;
        }

        private static bool WillCropDieTomorrow(GameLocation location, Crop crop, string harvestId) {
            Season currentSeason = location.GetSeason();
            Season nextSeason = currentSeason switch {
                Season.Spring => Season.Summer,
                Season.Summer => Season.Fall,
                Season.Fall => Season.Winter,
                _ => Season.Spring
            };

            var cropData = crop.GetData();
            if (cropData?.Seasons is { Count: > 0 } seasons) {
                return !seasons.Contains(nextSeason);
            }

            if (harvestId == SunflowerId) {
                return currentSeason == Season.Fall;
            }

            return true;
        }
    }
}