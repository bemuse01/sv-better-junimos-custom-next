using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.Characters;
using HarmonyLib;
using BetterJunimos.Utils;
using System;
using System.Linq;
using Netcode;
using BetterJunimos.Abilities;
using StardewModdingAPI;
using StardewValley.Buildings;
using StardewValley.Pathfinding;
using SObject = StardewValley.Object;

namespace BetterJunimos.Patches {
    /* foundCropEndFunction
     *
     * Is there an action to perform at the end of this pathfind?
     * Completely replace
     */
    public class PatchFindingCropEnd {
        public static bool Prefix(JunimoHarvester __instance, ref PathNode currentNode, ref NetGuid ___netHome, ref bool __result) {
            var hut = Util.GetHutFromId(___netHome.Value);
            var radius = Util.CurrentWorkingRadius;
            if (hut != null && !__instance.currentLocation.IsGreenhouse && !Util.IsWithinHutRadius(hut, currentNode.x, currentNode.y, radius)) {
                __result = false;
                return false;
            }

            if (Util.IsTileClaimedByOtherJunimo(__instance.currentLocation, currentNode.x, currentNode.y, __instance)) {
                __result = false;
                return false;
            }

            __result = Util.Abilities.IsActionable(__instance.currentLocation, new Vector2(currentNode.x, currentNode.y), ___netHome.Value);
            return false;
        }
    }

    /* tryToHarvestHere
     *
     * Try to perform ability
     * Except harvest
     * Completely replace
     *
     */
    public class PatchTryToHarvestHere {
        public static bool Prefix(JunimoHarvester __instance, ref int ___harvestTimer, ref NetGuid ___netHome) {
            if (!Context.IsMainPlayer) return true;
            var hut = Util.GetHutFromId(__instance.HomeId);
            // If the hut can't be found (e.g. it was demolished), fall back to the
            // vanilla behaviour instead of freezing the junimo in place.
            if (hut is null) return true;
            var id = __instance.HomeId;
            var pos = __instance.Tile;
            var radius = Util.CurrentWorkingRadius;

            if (!__instance.currentLocation.IsGreenhouse && !Util.IsWithinHutRadius(hut, pos, radius)) {
                ___harvestTimer = 0;
                Util.ClearTarget(__instance);
                __instance.pathfindToNewCrop();
                return false;
            }

            if (Util.IsTileClaimedByOtherJunimo(__instance.currentLocation, (int)pos.X, (int)pos.Y, __instance)) {
                ___harvestTimer = 0;
                Util.ClearTarget(__instance);
                __instance.pathfindToNewCrop();
                return false;
            }

            var junimoAbility = Util.Abilities.IdentifyJunimoAbility(__instance.currentLocation, pos, id);
            if (junimoAbility != null) {
                Util.AssignTarget(__instance, __instance.currentLocation, __instance.TilePoint);
                if (junimoAbility is HarvestBushesAbility) {
                    // Use the update() harvesting
                    ___harvestTimer = 2000;
                } else if (!Util.Abilities.PerformAction(junimoAbility, id, __instance.currentLocation, pos, __instance)) {
                    // didn't succeed, add failed action to ability cooldowns and move on immediately
                    Util.Abilities.ActionFailed(__instance.currentLocation, junimoAbility, pos);
                    ___harvestTimer = 0;
                    Util.ClearTarget(__instance);
                    __instance.pathfindToNewCrop();
                } else {
                    // succeeded, invalidate hut scan cache and shake
                    PatchSearchAroundHut.InvalidateCache();
                    ___harvestTimer = junimoAbility is HarvestCropsAbility ? 2000 : 300;
                }
            } else {
                // nothing to do on this tile, immediately find next work
                ___harvestTimer = 0;
                Util.ClearTarget(__instance);
                __instance.pathfindToNewCrop();
            }

            return false;
        }
    }

    // update
    // Animate & handle action timer 
    public class PatchJunimoShake {
        public static void Postfix(JunimoHarvester __instance, ref int ___harvestTimer, bool ___destroy) {
            if (!Context.IsMainPlayer) return;

            if (___harvestTimer is > 300 and < 1000) {
                // Crop/bush harvest just occurred at the 1000ms mark in vanilla update();
                // shorten the post-harvest wait while keeping ___harvestTimer > 0 so vanilla calls pokeToHarvest().
                PatchSearchAroundHut.InvalidateCache();
                ___harvestTimer = 100;
            } else if (___harvestTimer > 5) {
                __instance.shake(50);
            } else if (!___destroy && ___harvestTimer <= 0 && __instance.controller == null) {
                // Recover automatically if PathFindController timed out or was cleared
                if (!(BetterJunimos.Config.JunimoPayment.WorkForWages && !Util.Payments.WereJunimosPaidToday)) {
                    if (Game1.ticks % 30 == 0) {
                        __instance.pathfindToNewCrop();
                    }
                }
            }
        }
    }

    /* tryToAddItemToHut
     *
     * Botanist profession: forage (e.g. wild seed crops) harvested by Junimos
     * should be iridium quality, matching what the player would get harvesting
     * them personally. This runs before the item is placed in the hut chest, so
     * the quality is applied to the item as stored (and to raisin double-harvest
     * copies, which copy this item's quality).
     *
     * Bush yields are not affected, matching vanilla: the game only lets Junimos
     * harvest bushes by creating item 815 (tea leaves), and bush berries (296
     * salmonberry, 410 blackberry) never get quality from Botanist.
     */
    public class PatchJunimoHarvesterAddItemToHut {
        public static void Prefix(Item i) {
            if (!Game1.player.professions.Contains(16)) return;
            if (i is SObject obj && obj.Category == SObject.GreensCategory && obj.ItemId is not ("815" or "296" or "410") && obj.Quality < 4) {
                obj.Quality = 4;
            }
        }
    }

    // pathfindToRandomSpotAroundHut
    // Expand radius of random pathfinding and strictly stay within symmetric hut bounds
    public class PatchPathfindToRandomSpotAroundHut {
        public static bool Prefix(JunimoHarvester __instance) {
            if (!Context.IsMainPlayer) return true;
            var hut = Util.GetHutFromId(__instance.HomeId);
            if (hut is null) return false;

            Util.ClearTarget(__instance);
            var radius = Util.CurrentWorkingRadius;
            var retry = 0;
            do {
                var endPoint = __instance.currentLocation.IsGreenhouse
                    ? EndPointInGreenhouse(__instance)
                    : EndPointInFarm(hut, radius);

                __instance.controller = new PathFindController(__instance, __instance.currentLocation, endPoint, -1, __instance.reachFirstDestinationFromHut, 1000);
                retry++;
            } while (retry <= 5 && __instance.controller?.pathToEndPoint == null);

            return false;
        }

        private static Point EndPointInGreenhouse(JunimoHarvester jh) {
            var gw = jh.currentLocation.map.Layers[0].LayerWidth;
            var gh = jh.currentLocation.map.Layers[0].LayerHeight;
            return new Vector2(gw / 2 + Game1.random.Next(-(gw / 2 + 2), gh / 2 - 2), gh / 2 + Game1.random.Next(-(gw / 2 + 2), gh / 2 - 2)).ToPoint();
        }

        private static Point EndPointInFarm(JunimoHut hut, int radius) {
            var (minX, maxX, minY, maxY) = Util.GetHutRadiusBounds(hut, radius);
            for (int i = 0; i < 10; ++i) {
                int x = Game1.random.Next(minX, maxX + 1);
                int y = Game1.random.Next(minY, maxY + 1);
                if (!hut.occupiesTile(new Vector2(x, y))) {
                    return new Point(x, y);
                }
            }
            return new Point(hut.tileX.Value + 1, hut.tileY.Value + 2);
        }
    }

    [HarmonyPriority(Priority.Low)]
    public class PatchGet_home {
        public static void Postfix(ref JunimoHut __result, ref NetGuid ___netHome) {
            __result = Util.GetHutFromId(___netHome.Value);
        }
    }

    [HarmonyPriority(Priority.Low)]
    public class PatchSet_home {
        public static void Postfix(JunimoHut value, ref NetGuid ___netHome) {
            ___netHome.Value = Util.GetHutIdFromHut(value);
        }
    }

    // pathfindToNewCrop - completely replace 
    [HarmonyPriority(Priority.Low)]
    public class PatchPathfindDoWork {
        public static bool Prefix(JunimoHarvester __instance, ref NetEvent1Field<int, NetInt> ___netAnimationEvent) {
            if (!Context.IsMainPlayer) return true;
            var hut = Util.GetHutFromId(__instance.HomeId);
            if (hut is null) return true;

            Util.ClearTarget(__instance);

            var quittingTime = BetterJunimos.Config.JunimoImprovements.CanWorkInEvenings ? 2400 : 1900;

            if (Game1.timeOfDay > quittingTime) {
                // bedtime, all Junimos return to huts and/or despawn
                if (__instance.controller != null) return false;
                SendJunimoHome(__instance, hut);
                return false;
            }

            // Prevent working when not paid
            if (BetterJunimos.Config.JunimoPayment.WorkForWages && !Util.Payments.WereJunimosPaidToday) {
                if (Game1.random.NextDouble() < 0.02) {
                    __instance.pathfindToRandomSpotAroundHut();
                } else {
                    // go on strike
                    ___netAnimationEvent.Fire(7);
                }
                return false;
            }

            if (hut.noHarvest.Value) {
                __instance.pathfindToRandomSpotAroundHut();
                return false;
            }

            if (Util.TryAssignWorkPath(__instance, hut, __instance.currentLocation)) {
                // Junimo has a reachable, unclaimed work target within radius
                ___netAnimationEvent.Fire(0);
            } else {
                // No unclaimed reachable work remains; return to hut
                ___netAnimationEvent.Fire(0);
                SendJunimoHome(__instance, hut);
            }

            return false;
        }

        private static void SendJunimoHome(JunimoHarvester junimo, JunimoHut hut) {
            if (junimo.currentLocation.NameOrUniqueName == hut.GetParentLocation().NameOrUniqueName) {
                junimo.returnToJunimoHut(junimo.currentLocation);
            } else if (junimo.currentLocation.IsGreenhouse) {
                returnToGreenhouseDoor(junimo, junimo.currentLocation);
            } else {
                junimo.junimoReachedHut(junimo, junimo.currentLocation);
            }
        }

        private static void returnToGreenhouseDoor(JunimoHarvester junimo, GameLocation location) {
            if (Utility.isOnScreen(Utility.Vector2ToPoint(junimo.position.Value / 64f), 64, junimo.currentLocation)) junimo.jump();
            junimo.collidesWithOtherCharacters.Value = false;

            if (Game1.IsMasterGame) {
                var door = GreenhouseDoor(junimo, location);
                if (door == Point.Zero) {
                    junimo.junimoReachedHut(junimo, junimo.currentLocation);
                    return;
                }

                junimo.controller = new PathFindController(junimo, location, door, 1, junimo.junimoReachedHut);
                if (junimo.controller.pathToEndPoint == null || junimo.controller.pathToEndPoint.Count == 0) {
                    junimo.junimoReachedHut(junimo, junimo.currentLocation);
                    return;
                }
            }

            if (!Utility.isOnScreen(Utility.Vector2ToPoint(junimo.position.Value / 64f), 64, junimo.currentLocation)) return;
            location.playSound("junimoMeep1");
        }

        public static Point GreenhouseDoor(JunimoHarvester junimo, GameLocation location) {
            //TryFind warp to hutlocation
            var warp = location.warps.FirstOrDefault(warp => warp.TargetName == junimo.home.GetParentLocation().NameOrUniqueName);
            if (warp != null) {
                return new Point(warp.X, warp.Y - 1);
            }

            return Point.Zero;
        }
    }

    // pokeToHarvest
    // Completely replace vanilla pokeToHarvest so it never drops 30% of calls
    // and performs consecutive actions on the same tile before moving on.
    public class PatchPokeToHarvest {
        public static bool Prefix(JunimoHarvester __instance, ref bool ___destroy, ref int ___harvestTimer) {
            if (!Context.IsMainPlayer) return true;
            if (___destroy) return false;

            var hut = __instance.home;
            if (hut is null) return false;

            if (!hut.isTilePassable(__instance.Tile)) {
                ___destroy = true;
                Util.ClearTarget(__instance);
                return false;
            }

            if (___harvestTimer <= 0) {
                // If already walking to an assigned work target, let it continue
                if (__instance.controller?.pathToEndPoint is { Count: > 0 } && Util.HasAssignedTarget(__instance)) {
                    return false;
                }

                if (__instance.controller == null) {
                    var id = __instance.HomeId;
                    var pos = __instance.Tile;
                    var radius = Util.CurrentWorkingRadius;
                    bool inBounds = __instance.currentLocation.IsGreenhouse || Util.IsWithinHutRadius(hut, pos, radius);
                    if (inBounds &&
                        !Util.IsTileClaimedByOtherJunimo(__instance.currentLocation, (int)pos.X, (int)pos.Y, __instance) &&
                        Util.Abilities.IdentifyJunimoAbility(__instance.currentLocation, pos, id) != null) {
                        __instance.tryToHarvestHere();
                        return false;
                    }
                }

                __instance.pathfindToNewCrop();
            }

            return false;
        }
    }
}