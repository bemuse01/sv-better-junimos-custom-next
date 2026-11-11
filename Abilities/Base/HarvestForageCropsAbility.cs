using System;
using BetterJunimos.Utils;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using System.Collections.Generic;
using StardewModdingAPI;

namespace BetterJunimos.Abilities {
    public class HarvestForageCropsAbility : IJunimoAbility {
        public string AbilityName() {
            return "HarvestForageCrops";
        }

        public bool IsActionAvailable(GameLocation location, Vector2 pos, Guid guid) {
            if (location.objects.TryGetValue(pos, out var tileObj) && tileObj != null && !tileObj.isPassable()) {
                return false;
            }

            var hut = Util.GetHutFromId(guid);
            var radius = Util.CurrentWorkingRadius;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (hut != null && !location.IsGreenhouse && !Util.IsWithinHutRadius(hut, nextPos, radius)) {
                    continue;
                }
                if (location.objects.ContainsKey(nextPos) && location.objects[nextPos].isForage()) {
                    Vector2[] forageNeighbors = {
                        new Vector2(nextPos.X, nextPos.Y + 1),
                        new Vector2(nextPos.X + 1, nextPos.Y),
                        new Vector2(nextPos.X, nextPos.Y - 1),
                        new Vector2(nextPos.X - 1, nextPos.Y)
                    };
                    bool alreadyTargeted = false;
                    foreach (Vector2 neighbor in forageNeighbors) {
                        if (neighbor != pos && Util.IsTileClaimedByOtherJunimo(location, (int)neighbor.X, (int)neighbor.Y)) {
                            alreadyTargeted = true;
                            break;
                        }
                    }
                    if (!alreadyTargeted) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool PerformAction(GameLocation location, Vector2 pos, JunimoHarvester junimo, Guid guid) {
            var hut = Util.GetHutFromId(guid);
            Chest chest = hut.GetOutputChest();
            var radius = Util.CurrentWorkingRadius;

            Vector2 up = new Vector2(pos.X, pos.Y + 1);
            Vector2 right = new Vector2(pos.X + 1, pos.Y);
            Vector2 down = new Vector2(pos.X, pos.Y - 1);
            Vector2 left = new Vector2(pos.X - 1, pos.Y);

            int direction = 0;
            Vector2[] positions = { up, right, down, left };
            foreach (Vector2 nextPos in positions) {
                if (hut != null && !location.IsGreenhouse && !Util.IsWithinHutRadius(hut, nextPos, radius)) {
                    direction++;
                    continue;
                }
                if (location.objects.ContainsKey(nextPos) && location.objects[nextPos].isForage()) {
                    junimo.faceDirection(direction);
                    SetForageQuality(location, nextPos);

                    StardewValley.Object item = location.objects[nextPos];
                    Util.AddItemToChest(location, chest, item);

                    Util.SpawnParticles(nextPos);
                    location.objects.Remove(nextPos);

                    // calculate the forage experience from this harvest
                    if (BetterJunimos.Config.JunimoImprovements.GiveExperience) {
                        try {
                            Game1.player.gainExperience(2, 7);
                        } catch (Exception e) {
                            // modded skill frameworks can throw when given experience; don't
                            // let that break the harvest
                            BetterJunimos.SMonitor.Log($"Could not grant foraging experience: {e.Message}", LogLevel.Trace);
                        }
                    }

                    return true;
                }

                direction++;
            }

            return false;
        }

        private static readonly List<string> EmptyItems = new();

        public List<string> RequiredItems() {
            return EmptyItems;
        }

        // adapted from GameLocation.checkAction
        private void SetForageQuality(GameLocation location, Vector2 pos) {
            var quality = location.objects[pos].Quality;
            var random = new Random((int)Game1.uniqueIDForThisGame / 2 + (int)Game1.stats.DaysPlayed + (int)pos.X + (int)pos.Y * 777);

            foreach (var farmer in Game1.getOnlineFarmers()) {
                var f = farmer.Stamina;
                var maxQuality = quality;
                if (farmer.professions.Contains(16))
                    maxQuality = 4;
                else if (random.NextDouble() < farmer.ForagingLevel / 30.0)
                    maxQuality = 2;
                else if (random.NextDouble() < farmer.ForagingLevel / 15.0)
                    maxQuality = 1;
                if (maxQuality > quality)
                    quality = maxQuality;
            }

            location.objects[pos].Quality = quality;
        }
    }
}