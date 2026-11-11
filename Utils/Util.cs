using System;
using System.Collections.Generic;
using System.Linq;
using BetterJunimos.Patches;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Pathfinding;
using SObject = StardewValley.Object;

namespace BetterJunimos.Utils {
    public class Util {
        private const int UnreachableCooldownTicks = 300;
        private const int PathfindNodeLimit = 2500;
        private const int MaxPathfindAttempts = 10;

        public const int CoffeeId = 433;
        public const string CoffeeItemId = "433";

        private const int GemCategory = -2;
        private const int MineralCategory = -12;

        internal static IReflectionHelper Reflection;
        internal static JunimoAbilities Abilities;
        internal static JunimoProgression Progression;
        internal static JunimoGreenhouse Greenhouse;

        // Set of hut ids that we've already warned about, so orphaned junimos (e.g.
        // from a hut that was demolished or a save from an older version) don't spam
        // the log on every `home` lookup. Reset each day.
        private static readonly HashSet<Guid> MissingHutWarnings = new();

        // Tracks the active work tile assigned to each JunimoHarvester (both while moving and while working).
        private static readonly Dictionary<JunimoHarvester, (GameLocation location, Point target)> AssignedTargets = new();

        // Temporarily records tiles that A* could not reach due to obstacles (sprinklers, fences, etc.)
        // so Junimos don't repeatedly run full A* searches on walled-off tiles every frame.
        private static readonly Dictionary<(GameLocation location, Point tile), int> UnreachableTiles = new();

        public static IList<GameLocation> GetAllFarms() {
            // Note: this is the whole location list (the mod works on modded farms,
            // Ginger Island, etc), not just farms. Avoid allocating a copy; it's on a
            // hot path (every JunimoHarvester.home lookup).
            return Game1.locations;
        }

        public static int CurrentWorkingRadius => BetterJunimos.Config.JunimoHuts.MaxRadius;

        public static bool TryConsumeRaisins(JunimoHut hut, bool showMessage = true) {
            if (hut is null || hut.raisinDays.Value > 0) return false;
            Chest chest = hut.GetOutputChest();
            if (chest != null && chest.Items.CountId("(O)Raisins") > 0) {
                hut.raisinDays.Value += 7;
                chest.Items.ReduceId("(O)Raisins", 1);
                if (showMessage) {
                    SendMessage(BetterJunimos.SHelper.Translation.Get("msg.happy-with-raisins"));
                }
                return true;
            }
            return false;
        }

        public static List<JunimoHut> GetAllHuts() {
            return GetAllFarms().SelectMany(farm => farm.buildings.OfType<JunimoHut>().ToList()).ToList();
        }

        public static Guid GetHutIdFromHut(JunimoHut hut) {
            if (hut is null) return Guid.Empty;
            foreach (var farm in GetAllFarms()) {
                var id = farm.buildings.GuidOf(hut);
                if (id != Guid.Empty) return id;
            }

            return Guid.Empty;
        }

        public static JunimoHut GetHutFromId(Guid id) {
            foreach (var farm in GetAllFarms()) {
                if (farm.buildings.TryGetValue(id, out var hut)) {
                    return hut as JunimoHut;
                }
            }

            if (id != Guid.Empty && MissingHutWarnings.Add(id)) {
                BetterJunimos.SMonitor.Log($"Could not find hut with id {id}", LogLevel.Warn);
            }
            return null;
        }

        internal static void ResetMissingHutWarnings() {
            MissingHutWarnings.Clear();
            AssignedTargets.Clear();
            UnreachableTiles.Clear();
        }

        internal static void ClearUnreachableTiles() {
            UnreachableTiles.Clear();
        }

        /// <summary>
        /// Returns the symmetric tile bounds around a 3x2 JunimoHut so that
        /// up, down, left, and right all extend by exactly <paramref name="radius"/> tiles.
        /// </summary>
        public static (int minX, int maxX, int minY, int maxY) GetHutRadiusBounds(JunimoHut hut, int radius) {
            int minX = hut.tileX.Value - radius;
            int maxX = hut.tileX.Value + 2 + radius;
            int minY = hut.tileY.Value - radius;
            int maxY = hut.tileY.Value + 1 + radius;
            return (minX, maxX, minY, maxY);
        }

        public static bool IsWithinHutRadius(JunimoHut hut, int x, int y, int radius) {
            if (hut is null) return false;
            var (minX, maxX, minY, maxY) = GetHutRadiusBounds(hut, radius);
            return x >= minX && x <= maxX && y >= minY && y <= maxY;
        }

        public static bool IsWithinHutRadius(JunimoHut hut, Vector2 pos, int radius) {
            return IsWithinHutRadius(hut, (int)pos.X, (int)pos.Y, radius);
        }

        /// <summary>
        /// Calculates the Chebyshev (radius ring) distance and Manhattan distance from the outer 3x2 boundary of a JunimoHut.
        /// </summary>
        public static (int radiusDist, int manhattanDist) GetDistanceFromHut(JunimoHut hut, int x, int y) {
            int hutMinX = hut.tileX.Value;
            int hutMaxX = hutMinX + 2;
            int hutMinY = hut.tileY.Value;
            int hutMaxY = hutMinY + 1;

            int dx = x < hutMinX ? hutMinX - x : (x > hutMaxX ? x - hutMaxX : 0);
            int dy = y < hutMinY ? hutMinY - y : (y > hutMaxY ? y - hutMaxY : 0);
            return (Math.Max(dx, dy), dx + dy);
        }

        public static void AssignTarget(JunimoHarvester junimo, GameLocation location, Point target) {
            if (junimo is null) return;
            AssignedTargets[junimo] = (location, target);
        }

        public static void ClearTarget(JunimoHarvester junimo) {
            if (junimo is null) return;
            AssignedTargets.Remove(junimo);
        }

        public static bool HasAssignedTarget(JunimoHarvester junimo) {
            return junimo is not null && AssignedTargets.ContainsKey(junimo);
        }

        public static bool IsTileUnreachable(GameLocation location, Point tile) {
            if (!UnreachableTiles.TryGetValue((location, tile), out int failedTick)) return false;
            if (Game1.ticks - failedTick < UnreachableCooldownTicks) return true;
            UnreachableTiles.Remove((location, tile));
            return false;
        }

        public static void MarkTileUnreachable(GameLocation location, Point tile) {
            UnreachableTiles[(location, tile)] = Game1.ticks;
        }

        public static bool IsTileClaimedByOtherJunimo(GameLocation location, int x, int y, JunimoHarvester me = null) {
            if (location is null) return false;

            foreach (var character in location.characters) {
                if (character is not JunimoHarvester other || other == me) continue;
                if (other.currentLocation != location) continue;

                // Check explicitly assigned target if the other Junimo is still moving to it or standing on it working
                if (AssignedTargets.TryGetValue(other, out var assigned) && assigned.location == location) {
                    bool stillActiveOnTarget = other.controller != null ||
                                               (other.TilePoint.X == assigned.target.X && other.TilePoint.Y == assigned.target.Y);
                    if (stillActiveOnTarget) {
                        if (assigned.target.X == x && assigned.target.Y == y) return true;
                    } else {
                        AssignedTargets.Remove(other);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the closest unclaimed, reachable workable tile within the hut's radius (or greenhouse)
        /// and assigns an A* PathFindController to the Junimo.
        /// </summary>
        public static bool TryAssignWorkPath(JunimoHarvester me, JunimoHut hut, GameLocation location) {
            if (me is null || hut is null || location is null) return false;
            ClearTarget(me);

            Guid id = GetHutIdFromHut(hut);
            int radius = CurrentWorkingRadius;
            Point myTile = me.TilePoint;

            var candidates = new List<(Point pt, int priority, int hutRadiusDist, int hutManhattanDist)>();
            int width = location.map.Layers[0].LayerWidth;
            int height = location.map.Layers[0].LayerHeight;

            if (location.IsGreenhouse) {
                for (int x = 0; x < width; ++x) {
                    for (int y = 0; y < height; ++y) {
                        var pt = new Point(x, y);
                        if (IsTileUnreachable(location, pt)) continue;
                        if (Abilities.IdentifyJunimoAbility(location, new Vector2(x, y), id) == null) continue;
                        if (IsTileClaimedByOtherJunimo(location, x, y, me)) continue;
                        candidates.Add((pt, 0, 0, 0));
                    }
                }
            } else {
                List<JunimoHut> otherHuts = null;
                foreach (var building in location.buildings) {
                    if (building is JunimoHut otherHut && !ReferenceEquals(otherHut, hut)) {
                        otherHuts ??= new List<JunimoHut>();
                        otherHuts.Add(otherHut);
                    }
                }

                var (minX, maxX, minY, maxY) = GetHutRadiusBounds(hut, radius);
                minX = Math.Max(0, minX);
                minY = Math.Max(0, minY);
                maxX = Math.Min(width - 1, maxX);
                maxY = Math.Min(height - 1, maxY);

                for (int x = minX; x <= maxX; ++x) {
                    for (int y = minY; y <= maxY; ++y) {
                        var pt = new Point(x, y);
                        if (IsTileUnreachable(location, pt)) continue;
                        if (Abilities.IdentifyJunimoAbility(location, new Vector2(x, y), id) == null) continue;
                        if (IsTileClaimedByOtherJunimo(location, x, y, me)) continue;

                        var (myRadiusDist, myManhattanDist) = GetDistanceFromHut(hut, x, y);
                        int priority = 0; // 0: Exclusive, 1: Shared but closer to my hut (my plot), 2: Shared and closer to another hut (neighbor's plot)

                        if (otherHuts != null) {
                            for (int i = 0; i < otherHuts.Count; ++i) {
                                var (otherRadiusDist, otherManhattanDist) = GetDistanceFromHut(otherHuts[i], x, y);
                                if (otherRadiusDist <= radius) {
                                    if (otherRadiusDist < myRadiusDist ||
                                        (otherRadiusDist == myRadiusDist && otherManhattanDist < myManhattanDist)) {
                                        priority = 2;
                                        break;
                                    }
                                    priority = 1;
                                }
                            }
                        }

                        candidates.Add((pt, priority, myRadiusDist, myManhattanDist));
                    }
                }
            }

            if (candidates.Count == 0) return false;

            // Priority 0 (Exclusive) -> Priority 1 (Own plot in overlap) -> Priority 2 (Neighbor's plot in overlap).
            // Within own plot (0 & 1), sort by Manhattan distance from the Junimo's current position.
            // Within neighbor's plot (2), sort by distance from this hut first so it doesn't wander deep into a neighbor's plot.
            int junimoOffset = Math.Abs(me.whichJunimoFromThisHut);
            candidates.Sort((a, b) => {
                if (a.priority != b.priority) return a.priority.CompareTo(b.priority);
                if (a.priority == 2) {
                    if (a.hutRadiusDist != b.hutRadiusDist) return a.hutRadiusDist.CompareTo(b.hutRadiusDist);
                    if (a.hutManhattanDist != b.hutManhattanDist) return a.hutManhattanDist.CompareTo(b.hutManhattanDist);
                }
                int distA = Math.Abs(a.pt.X - myTile.X) + Math.Abs(a.pt.Y - myTile.Y);
                int distB = Math.Abs(b.pt.X - myTile.X) + Math.Abs(b.pt.Y - myTile.Y);
                if (distA != distB) return distA.CompareTo(distB);
                if (a.hutManhattanDist != b.hutManhattanDist) return a.hutManhattanDist.CompareTo(b.hutManhattanDist);
                int tieA = Math.Abs((a.pt.X * 31 + a.pt.Y * 17 + junimoOffset) % 7);
                int tieB = Math.Abs((b.pt.X * 31 + b.pt.Y * 17 + junimoOffset) % 7);
                return tieA.CompareTo(tieB);
            });

            int attempts = 0;
            foreach (var (target, _, _, _) in candidates) {
                var controller = new PathFindController(me, location, target, -1, me.reachFirstDestinationFromHut, PathfindNodeLimit);
                if (controller.pathToEndPoint is { Count: > 0 }) {
                    AssignTarget(me, location, target);
                    me.controller = controller;
                    PatchSearchAroundHut.InvalidateCache();
                    return true;
                }

                // Could not reach this tile (blocked by sprinklers/scarecrows/fences); mark unreachable and try next
                MarkTileUnreachable(location, target);
                attempts++;
                if (attempts >= MaxPathfindAttempts) break;
            }

            return false;
        }

        public static void AddItemToChest(GameLocation farm, Chest chest, SObject item) {
            Item obj = chest.addItem(item);
            if (obj == null) return;
            Vector2 pos = chest.TileLocation;
            for (int index = 0; index < obj.Stack; ++index) Game1.createObjectDebris(item.ItemId, (int)pos.X + 1, (int)pos.Y + 1, -1, item.Quality, 1f, farm);
        }

        public static void RemoveItemFromChest(Chest chest, Item item, int count = 1) {
            item.Stack -= count;
            if (item.Stack <= 0) {
                int idx = chest.Items.IndexOf(item);
                if (idx >= 0) {
                    chest.Items[idx] = null;
                }
                chest.Items.Remove(item);
                chest.clearNulls();
            }
        }

        public static void SpawnJunimoAtHut(JunimoHut hut) {
            var pos = new Vector2((float)hut.tileX.Value + 1, (float)hut.tileY.Value + 1) * 64f + new Vector2(0.0f, 32f);
            SpawnJunimoAtPosition(hut.GetParentLocation(), pos, hut, hut.getUnusedJunimoNumber());
        }

        public static void SpawnJunimoAtPosition(GameLocation location, Vector2 pos, JunimoHut hut, int junimoNumber) {
            if (hut == null) {
                return;
            }

            /*
             * Added by Mizzion. This will set the color of the junimos based on what gem is inside the hut.
             */
            var isPrismatic = false;
            var gemColor = GetGemColor(ref isPrismatic, hut);
            /*
             * End added By Mizzion
             */

            var junimoHarvester = new JunimoHarvester(location, pos, hut, junimoNumber, gemColor);

            // Vanilla's JunimoHarvester constructor only handles junimoNumber 0..2 in its switch statement;
            // for junimoNumber >= 3 (4th+ Junimo) or non-Farm locations, it can leave destroy = true.
            // Always clear destroy and ensure location/position are set before assigning work.
            Reflection.GetField<bool>(junimoHarvester, "destroy").SetValue(false);
            if (!location.Equals(Game1.getFarm())) {
                junimoHarvester.currentLocation = location;
                junimoHarvester.Position = pos;
            }

            junimoHarvester.isPrismatic.Value = isPrismatic;
            location.characters.Add(junimoHarvester);
            hut.myJunimos.Add(junimoHarvester);
            junimoHarvester.HomeId = Util.GetHutIdFromHut(hut);

            // Immediately direct the newly spawned Junimo to its closest unclaimed work tile (or back home if none)
            junimoHarvester.pathfindToNewCrop();

            if (Game1.isRaining) {
                var alpha = Reflection.GetField<float>(junimoHarvester, "alpha");
                alpha.SetValue(BetterJunimos.Config.FunChanges.RainyJunimoSpiritFactor);
            }

            if (!Utility.isOnScreen(Utility.Vector2ToPoint(pos), 64, location)) return;
            location.playSound("junimoMeep1");
        }

/*
 * Added by Mizzion. This method is used to get the gem color, so the junimos can be colored
 * I ripped this from SDV and edited it to work with this mod.
 */
        private static Color? GetGemColor(ref bool isPrismatic, JunimoHut hut) {
            var colorList = new List<Color>();
            var chest = hut.GetOutputChest();
            foreach (Item dyeObject in chest.Items) {
                if (dyeObject != null && (dyeObject.Category == MineralCategory || dyeObject.Category == GemCategory)) {
                    Color? dyeColor = TailoringMenu.GetDyeColor(dyeObject);
                    if (dyeObject.Name == "Prismatic Shard") isPrismatic = true;
                    if (dyeColor.HasValue) colorList.Add(dyeColor.Value);
                }
            }

            if (colorList.Count > 0) return colorList[Game1.random.Next(colorList.Count)];
            return new Color?();
        }

        public static void SendMessage(string msg) {
            if (!BetterJunimos.Config.Other.ReceiveMessages) return;

            Game1.addHUDMessage(new HUDMessage(msg, 3) {
                noIcon = true,
                timeLeft = HUDMessage.defaultTime
            });
        }

        public static void SpawnParticles(Vector2 pos) {
            Game1.Multiplayer.broadcastSprites(Game1.currentLocation,
                new TemporaryAnimatedSprite(17, new Vector2(pos.X * 64f, pos.Y * 64f), Color.White, 7, Game1.random.NextDouble() < 0.5, 125f));
            Game1.Multiplayer.broadcastSprites(Game1.currentLocation,
                new TemporaryAnimatedSprite(14, new Vector2(pos.X * 64f, pos.Y * 64f), Color.White, 7, Game1.random.NextDouble() < 0.5, 50f));
        }

        internal static int ExperienceForCrop(Crop crop) {
            if (crop == null || crop.forageCrop.Value) {
                return 3;
            }

            string ioh = crop.indexOfHarvest.Value?.ToString();
            if (ioh != null && Game1.objectData.TryGetValue(ioh, out var oi)) {
                double num = Math.Round(16.0 * Math.Log(0.018 * (double)oi.Price + 1.0));
                return Convert.ToInt32(num);
            }

            return 0;
        }
    }
}