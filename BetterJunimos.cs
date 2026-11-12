using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Buildings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BetterJunimos.Patches;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using BetterJunimos.Utils;
using StardewValley.Objects;

namespace BetterJunimos {
    public class BetterJunimos : Mod {
        internal static ModConfig Config;
        internal static IMonitor SMonitor;
        internal static IModHelper SHelper;
        internal static Maps CropMaps;

        private IGenericModConfigMenuApi configMenu;

        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper) {
            SMonitor = Monitor;
            SHelper = helper;

            Config = helper.ReadConfig<ModConfig>();
            SaveConfig();

            Util.Reflection = helper.Reflection;

            Util.Abilities = new JunimoAbilities(Config.JunimoAbilities, Monitor);
            Util.Progression = new JunimoProgression(ModManifest, Monitor, Helper);

            helper.Events.Content.AssetRequested += JunimoEditor.OnAssetRequested;

            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.World.BuildingListChanged += OnBuildingListChanged;

            DoHarmonyRegistration();

            helper.ConsoleCommands.Add("bj_list_huts", "List available huts", ListHuts);
            helper.ConsoleCommands.Add("bj_list_actions", "List available actions", ListActions);
            helper.ConsoleCommands.Add("bj_list_abilities", "List configurable abilities", ListConfigurableAbilities);
            helper.ConsoleCommands.Add("bj_list_cooldowns", "List cooldowns", ListCooldowns);
            helper.ConsoleCommands.Add("bj_reset_cooldowns", "Reset cooldowns", ResetCooldowns);
        }

        private void ListHuts(string command, string[] args) {
            Util.Progression.ListHuts();
        }

        private void ListActions(string command, string[] args) {
            Util.Progression.ListAllAvailableActions();
        }

        private void ListConfigurableAbilities(string command, string[] args) {
            Util.Progression.ListConfigurableAbilities();
        }

        private void ListCooldowns(string command, string[] args) {
            Util.Abilities.ListCooldowns();
        }

        private void ResetCooldowns(string command, string[] args) {
            JunimoAbilities.ResetCooldowns();
        }

        private void DoHarmonyRegistration() {
            var harmony = new Harmony(ModManifest.UniqueID);
            // Thank you to Cat (danvolchek) for this harmony setup implementation
            // https://github.com/danvolchek/StardewMods/blob/master/BetterGardenPots/BetterGardenPots/BetterGardenPotsMod.cs#L29
            IList<Tuple<string, Type, Type>> replacements = new List<Tuple<string, Type, Type>>();

            // Junimo Harvester patches
            var junimoType = typeof(JunimoHarvester);
            replacements.Add("foundCropEndFunction", junimoType, typeof(PatchFindingCropEnd));
            replacements.Add("tryToHarvestHere", junimoType, typeof(PatchTryToHarvestHere));
            replacements.Add("update", junimoType, typeof(PatchJunimoShake));
            replacements.Add("pokeToHarvest", junimoType, typeof(PatchPokeToHarvest));
            replacements.Add("tryToAddItemToHut", junimoType, typeof(PatchJunimoHarvesterAddItemToHut));
            replacements.Add("get_home", junimoType, typeof(PatchGet_home));
            replacements.Add("set_home", junimoType, typeof(PatchSet_home));

            // improve pathfinding
            replacements.Add("pathfindToRandomSpotAroundHut", junimoType, typeof(PatchPathfindToRandomSpotAroundHut));
            replacements.Add("pathfindToNewCrop", junimoType, typeof(PatchPathfindDoWork));

            // Junimo Hut patches
            var junimoHutType = typeof(JunimoHut);
            replacements.Add("areThereMatureCropsWithinRadius", junimoHutType, typeof(PatchSearchAroundHut));

            // replacements for hardcoded max junimos
            replacements.Add("dayUpdate", junimoHutType, typeof(ReplaceJunimoHutdayUpdate));
            replacements.Add("Update", junimoHutType, typeof(ReplaceJunimoHutUpdate));
            replacements.Add("updateWhenFarmNotCurrentLocation", junimoHutType, typeof(ReplaceJunimoHutupdateWhenFarmNotCurrentLocation));
            replacements.Add("getUnusedJunimoNumber", junimoHutType, typeof(ReplaceJunimoHutNumber));
            replacements.Add("performTenMinuteAction", junimoHutType, typeof(ReplaceJunimoTimerNumber));

            foreach (Tuple<string, Type, Type> replacement in replacements) {
                MethodInfo original = replacement.Item2.GetMethods(BindingFlags.Instance | BindingFlags.Public).ToList()
                    .Find(m => m.Name == replacement.Item1);
                MethodInfo prefix = replacement.Item3.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(item => item.Name == "Prefix");
                MethodInfo postfix = replacement.Item3.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(item => item.Name == "Postfix");

                if (original == null) {
                    Monitor.Log($"Missing method {replacement.Item1}", LogLevel.Error);
                    continue;
                }

                harmony.Patch(original, prefix == null ? null : new HarmonyMethod(prefix),
                    postfix == null ? null : new HarmonyMethod(postfix));
            }
        }

        /// <summary>Raised after a the game is saved</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSaving(object sender, SavingEventArgs e) {
            if (!Context.IsMainPlayer) return;
            Helper.Data.WriteSaveData(ModDataKeys.CropMapsSaveKey, CropMaps);
            SaveConfig();
        }

        /// <summary>Raised after a game menu is opened, closed, or replaced.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnMenuChanged(object sender, MenuChangedEventArgs e) {
            // check that e.NewMenu is null because this event also fires when items are added to the chest
            // caution: this runs after any chest is closed, not just Junimo huts
            if (e.OldMenu is not ItemGrabMenu menu || e.NewMenu is not null) return;
            if (menu.context is not (JunimoHut or Chest)) return;
            if (menu.context is Chest chest) {
                string chestKey = ModDataKeys.GetJunimoChestKey(ModManifest.UniqueID);
                if (!chest.modData.ContainsKey(chestKey)) {
                    if (Util.GetAllHuts().Any(h => ReferenceEquals(h.GetOutputChest(), chest))) {
                        chest.modData[chestKey] = "true";
                    } else {
                        return;
                    }
                }
            }

            RefreshHutsAndRaisins(showRaisinMessage: true);
            JunimoAbilities.ResetCooldowns();
            // chest contents changed, the cached work search may be out of date
            Patches.PatchSearchAroundHut.InvalidateCache();
        }

        /// <summary>Raised after the game begins a new day (including when the player loads a save).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        void OnDayStarted(object sender, DayStartedEventArgs e) {
            var huts = Util.GetAllHuts();

            // tag each hut chest so later we can tell whether a GrabMenu close is for a Junimo chest or some other chest 
            foreach (var hut in huts) {
                hut.GetOutputChest().modData[ModDataKeys.GetJunimoChestKey(ModManifest.UniqueID)] = "true";
            }

            if (huts.Any()) {
                RefreshHutsAndRaisins(showRaisinMessage: false);
                if (huts.Any(h => h.raisinDays.Value > 0)) {
                    Util.SendMessage(Helper.Translation.Get("msg.happy-with-raisins"));
                }
                JunimoAbilities.ResetCooldowns();
            }

            // forget stale scan results and hut warnings from the previous session
            Patches.PatchSearchAroundHut.InvalidateCache();
            Util.ResetMissingHutWarnings();

            foreach (var location in Game1.locations) {
                var toRemove = location.characters.Where(npc => npc is JunimoHarvester).ToList();
                if (toRemove.Count > 0) {
                    Monitor.Log($"{location.Name} has {toRemove.Count} Junimos", LogLevel.Trace);
                }

                foreach (var npc in toRemove) {
                    var junimo = (JunimoHarvester)npc;
                    Monitor.Log($"    Removing Junimo {junimo.whichJunimoFromThisHut} from {location.Name}", LogLevel.Trace);
                    location.characters.Remove(npc);
                }
            }

            // reset for rainy days, winter, or Generic Mod Config Menu options change
            SaveConfig();
        }

        private static void RefreshHutsAndRaisins(bool showRaisinMessage) {
            var junimoHuts = Util.GetAllHuts();
            if (!junimoHuts.Any()) return;

            bool consumedAnyRaisins = false;
            foreach (var hut in junimoHuts) {
                var hutId = Util.GetHutIdFromHut(hut);
                if (Util.TryConsumeRaisins(hut, showMessage: false)) {
                    consumedAnyRaisins = true;
                }
                Util.Abilities.UpdateHutItems(hutId, hut);
            }

            if (showRaisinMessage && consumedAnyRaisins) {
                Util.SendMessage(SHelper.Translation.Get("msg.happy-with-raisins"));
            }
        }

        /// <summary>Raised after a building is added</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnBuildingListChanged(object sender, BuildingListChangedEventArgs e) {
            foreach (Building building in e.Added) {
                if (building is JunimoHut hut) {
                    hut.GetOutputChest().modData[ModDataKeys.GetJunimoChestKey(ModManifest.UniqueID)] = "true";
                    Util.Abilities.UpdateHutItems(Util.GetHutIdFromHut(hut), hut);
                }
            }

            if (e.Added.Any() || e.Removed.Any()) {
                Patches.PatchSearchAroundHut.InvalidateCache();
            }
        }

        /// <summary>Raised after the player loads a save slot and the world is initialised.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSaveLoaded(object sender, EventArgs e) {
            // rebuild the GMCM menu now we know who's host/farmhand
            SetupGenericModConfigMenu();

            if (!Context.IsMainPlayer) return;

            // load crop maps from save (TODO: not MP-safe)
            CropMaps = Helper.Data.ReadSaveData<Maps>(ModDataKeys.CropMapsSaveKey) ?? new Maps();
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e) {
            // only register after the game is launched so we can query the object registry
            Util.Abilities.RegisterDefaultAbilities();

            SetupGenericModConfigMenu();

            // write the config file again to populate JunimoAbilities with registered abilities
            SaveConfig();
        }

        private static bool GetAbilityEnabled(string abilityName) {
            return !Config.JunimoAbilities.TryGetValue(abilityName, out bool enabled) || enabled;
        }

        private static void SetAbilityEnabled(string abilityName, bool enabled) {
            Config.JunimoAbilities[abilityName] = enabled;
        }

        private void SetupGenericModConfigMenu() {
            configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Unregister(ModManifest);
            configMenu.Register(ModManifest, () => Config = new ModConfig(), SaveConfig);
            configMenu.SetTitleScreenOnlyForNextOptions(ModManifest, false);

            // 1. Hut Settings
            configMenu.AddSectionTitle(ModManifest,
                () => Helper.Translation.Get("cfg.hut-settings"));

            AddHostNumberOption(
                () => Config.JunimoHuts.MaxJunimos,
                val => Config.JunimoHuts.MaxJunimos = val,
                "cfg.max-junimos",
                1, 12
            );
            AddHostNumberOption(
                () => Config.JunimoHuts.MaxRadius,
                val => Config.JunimoHuts.MaxRadius = val,
                "cfg.max-radius",
                1, 20
            );

            // 2. Work Conditions & Abilities
            configMenu.AddSectionTitle(ModManifest,
                () => Helper.Translation.Get("cfg.improvements"),
                () => ""
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.CanWorkInRain,
                val => Config.JunimoImprovements.CanWorkInRain = val,
                "cfg.can-work-in-rain"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.CanWorkInWinter,
                val => Config.JunimoImprovements.CanWorkInWinter = val,
                "cfg.can-work-in-winter"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.CanWorkInEvenings,
                val => Config.JunimoImprovements.CanWorkInEvenings = val,
                "cfg.can-work-in-evenings"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("HarvestCrops"),
                val => SetAbilityEnabled("HarvestCrops", val),
                "cfg.ability.harvest-crops",
                "cfg.ability.harvest-crops.tooltip"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("PlantCrops"),
                val => SetAbilityEnabled("PlantCrops", val),
                "cfg.ability.plant-crops",
                "cfg.ability.plant-crops.tooltip"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("Fertilize"),
                val => SetAbilityEnabled("Fertilize", val),
                "cfg.ability.fertilize",
                "cfg.ability.fertilize.tooltip"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("Water"),
                val => SetAbilityEnabled("Water", val),
                "cfg.ability.water",
                "cfg.ability.water.tooltip"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("ClearDeadCrops"),
                val => SetAbilityEnabled("ClearDeadCrops", val),
                "cfg.ability.clear-dead-crops",
                "cfg.ability.clear-dead-crops.tooltip"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("HarvestBushes"),
                val => SetAbilityEnabled("HarvestBushes", val),
                "cfg.ability.harvest-bushes",
                "cfg.ability.harvest-bushes.tooltip"
            );
            AddHostBoolOption(
                () => GetAbilityEnabled("HarvestForageCrops"),
                val => SetAbilityEnabled("HarvestForageCrops", val),
                "cfg.ability.harvest-forage-crops",
                "cfg.ability.harvest-forage-crops.tooltip"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.GiveExperience,
                val => Config.JunimoImprovements.GiveExperience = val,
                "cfg.give-experience",
                "cfg.give-experience.tooltip"
            );

            // 3. Harvest & Planting Detailed Options (Separated Category)
            configMenu.AddSectionTitle(ModManifest,
                () => Helper.Translation.Get("cfg.harvest-planting-options"),
                () => ""
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.AvoidHarvestingFlowers,
                val => Config.JunimoImprovements.AvoidHarvestingFlowers = val,
                "cfg.avoid-harvesting-flowers"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.AvoidHarvestingGiants,
                val => Config.JunimoImprovements.AvoidHarvestingGiants = val,
                "cfg.avoid-harvesting-giant-crops",
                "cfg.avoid-harvesting-giant-crops.tooltip"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.HarvestEverythingOn28th,
                val => Config.JunimoImprovements.HarvestEverythingOn28th = val,
                "cfg.harvest-everything-on-28th",
                "cfg.harvest-everything-on-28th.tooltip"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.AvoidPlantingCoffee,
                val => Config.JunimoImprovements.AvoidPlantingCoffee = val,
                "cfg.avoid-planting-coffee"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.AvoidPlantingOutOfSeason,
                val => Config.JunimoImprovements.AvoidPlantingOutOfSeason = val,
                "cfg.avoid-planting-out-of-season"
            );
            AddHostBoolOption(
                () => Config.JunimoImprovements.PlantMixedSeeds,
                val => Config.JunimoImprovements.PlantMixedSeeds = val,
                "cfg.plant-mixed-seeds",
                "cfg.plant-mixed-seeds.tooltip"
            );

            // 4. Other
            configMenu.AddSectionTitle(ModManifest,
                () => Helper.Translation.Get("cfg.other"),
                () => "");
            configMenu.AddNumberOption(ModManifest,
                () => Config.FunChanges.RainyJunimoSpiritFactor,
                val => Config.FunChanges.RainyJunimoSpiritFactor = val,
                () => Helper.Translation.Get("cfg.rainy-spirit-factor"),
                () => Helper.Translation.Get("cfg.rainy-spirit-factor.tooltip"),
                0.0f,
                1.0f,
                0.05f);
            AddBoolOption(
                () => Config.FunChanges.JunimosAlwaysHaveLeafUmbrellas,
                val => Config.FunChanges.JunimosAlwaysHaveLeafUmbrellas = val,
                "cfg.always-have-umbrellas",
                "cfg.always-have-umbrellas.tooltip"
            );
            AddBoolOption(
                () => Config.FunChanges.MoreColorfulLeafUmbrellas,
                val => Config.FunChanges.MoreColorfulLeafUmbrellas = val,
                "cfg.more-colorful-umbrellas",
                "cfg.more-colorful-umbrellas.tooltip"
            );
            AddBoolOption(
                () => Config.Other.ReceiveMessages,
                val => Config.Other.ReceiveMessages = val,
                "cfg.receive-messages"
            );
        }

        private void AddHostBoolOption(Func<bool> getValue, Action<bool> setValue, string translationKey, string tooltipKey = null) {
            if (!Context.IsMainPlayer) return;
            AddBoolOption(getValue, setValue, translationKey, tooltipKey);
        }

        private void AddHostNumberOption(Func<int> getValue, Action<int> setValue, string translationKey, int min, int max) {
            if (!Context.IsMainPlayer) return;
            AddNumberOption(getValue, setValue, translationKey, min, max);
        }

        private void AddBoolOption(Func<bool> getValue, Action<bool> setValue, string translationKey, string tooltipKey = null) {
            configMenu.AddBoolOption(ModManifest, getValue, setValue, () => Helper.Translation.Get(translationKey),
                () => tooltipKey != null ? Helper.Translation.Get(tooltipKey) : "");
        }

        private void AddNumberOption(Func<int> getValue, Action<int> setValue, string translationKey, int min, int max) {
            configMenu.AddNumberOption(ModManifest, getValue, setValue, () => Helper.Translation.Get(translationKey), () => "", min, max);
        }

        // save config.json and invalidate caches
        internal static void SaveConfig() {
            Config.JunimoHuts.MaxJunimos = Math.Clamp(Config.JunimoHuts.MaxJunimos, 1, 12);
            Config.JunimoHuts.MaxRadius = Math.Clamp(Config.JunimoHuts.MaxRadius, 1, 20);
            Config.JunimoAbilities.Remove("VisitGreenhouse");
            SHelper.WriteConfig(Config);
            SHelper.GameContent.InvalidateCache("Characters/Junimo");
            SHelper.GameContent.InvalidateCache($"Characters/Junimo.{SHelper.Translation.Locale}");
            if (Context.IsWorldReady) {
                JunimoAbilities.ResetCooldowns();
                PatchSearchAroundHut.InvalidateCache();
            }
        }

        public override object GetApi() {
            return new BetterJunimosApi();
        }
    }
}
